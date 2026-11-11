using WeatherWidget.Core;
namespace WeatherWidget;

internal sealed record WeatherNotice(string Kind, string Message);

internal static class WeatherExtras
{
    public static bool IsFresh(WeatherSnapshot data, DateTimeOffset now) =>
        now - data.FetchedAtUtc < TimeSpan.FromHours(1) && now - data.Current.TimeUtc < TimeSpan.FromHours(1)
        && data.FetchedAtUtc <= now.AddMinutes(5) && data.Current.TimeUtc <= now.AddMinutes(5);

    // Hourly forecasts cannot promise minute-by-minute rain onset.
    public static string RainSoon(WeatherSnapshot data, DateTimeOffset now, string language)
    {
        if (!IsFresh(data, now)) return "";
        var hour = data.Hourly.FirstOrDefault(h => h.TimeUtc > now.AddHours(-1) && h.TimeUtc <= now.AddHours(3)
            && h.PrecipitationProbability >= 60 && IsRain(h.WeatherCode));
        if (hour == null) return "";
        int hours = Math.Max(0, (int)Math.Ceiling((hour.TimeUtc - now).TotalHours));
        return hours == 0 ? UiText.T(language,"Kiša vjerojatna ovaj sat","Rain likely this hour")
            : UiText.T(language,$"Kiša vjerojatna za ≤{hours} h",$"Rain likely within {hours} h");
    }
    private static bool IsRain(int code) => code is 51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 or 95 or 96 or 99;

    public static bool IsQuiet(DateTimeOffset localNow, int start, int end) =>
        start == end || (start < end ? localNow.Hour >= start && localNow.Hour < end : localNow.Hour >= start || localNow.Hour < end);

    public static IReadOnlyList<WeatherNotice> Notices(WeatherSnapshot data, DateTimeOffset now, string language)
    {
        if (!IsFresh(data, now)) return Array.Empty<WeatherNotice>();
        var notices = new List<WeatherNotice>(3);
        string rain = RainSoon(data, now, language);
        if (rain.Length > 0) notices.Add(new("rain", rain + UiText.T(language," · satna prognoza, vjerojatnost ≥60%"," · hourly forecast, probability ≥60%")));
        if (data.Current.TemperatureC <= 0) notices.Add(new("frost", UiText.T(language,"Temperatura je 0 °C ili niža. Moguć mraz.","Temperature is at or below 0°C. Frost is possible.")));
        if (data.Current.WindKmh >= 50) notices.Add(new("wind", UiText.T(language,"Jak vjetar: najmanje 50 km/h.","Strong wind: at least 50 km/h.")));
        return notices;
    }

    public static IReadOnlyList<WeatherNotice> TakeNotifications(AppSettings settings, WeatherSnapshot data, DateTimeOffset now, DateTimeOffset localNow)
    {
        if (!settings.WeatherNotifications || settings.QuietHours && IsQuiet(localNow,settings.QuietStart,settings.QuietEnd))
            return Array.Empty<WeatherNotice>();
        var notices = Notices(data, now, settings.Language);
        if (notices.Count == 0) return notices;
        var location = FormattableString.Invariant($"{data.Location.Latitude:F5},{data.Location.Longitude:F5}");
        if (settings.AlertLocation != location) { settings.AlertLocation = location; settings.AlertHistory.Clear(); }
        var due = notices.Where(n => !settings.AlertHistory.TryGetValue(n.Kind,out var sent) || now - sent >= TimeSpan.FromHours(6)).ToArray();
        foreach (var notice in due) settings.AlertHistory[notice.Kind] = now;
        return due;
    }
}

internal static class EdgeSnap
{
    public static (int X,int Y) Position(int x,int y,int width,int height,int left,int top,int right,int bottom,int distance)
    {
        // Keep oversized cards inside the usable area as far as possible.
        int maxX=Math.Max(left,right-width),maxY=Math.Max(top,bottom-height);
        x=Math.Clamp(x,left,maxX); y=Math.Clamp(y,top,maxY);
        if (x-left <= distance) x=left; else if (maxX-x <= distance) x=maxX;
        if (y-top <= distance) y=top; else if (maxY-y <= distance) y=maxY;
        return (x,y);
    }
}
