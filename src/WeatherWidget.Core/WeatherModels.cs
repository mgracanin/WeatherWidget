namespace WeatherWidget.Core;

public sealed record WeatherLocation(string Name, double Latitude, double Longitude, string? Region = null, string? Country = null);

public sealed record WeatherSnapshot(
    WeatherLocation Location,
    DateTimeOffset FetchedAtUtc,
    string TimeZoneId,
    CurrentConditions Current,
    IReadOnlyList<HourlyForecast> Hourly,
    IReadOnlyList<DailyForecast> Daily);

public sealed record CurrentConditions(DateTimeOffset TimeUtc, double TemperatureC, double FeelsLikeC, int Humidity, double WindKmh, int WeatherCode, bool IsDay);

public sealed record HourlyForecast(DateTimeOffset TimeUtc, double TemperatureC, int PrecipitationProbability, int WeatherCode, bool IsDay);

public sealed record DailyForecast(DateOnly Date, double HighC, double LowC, int WeatherCode, int PrecipitationProbability);

public enum TemperatureUnit { Celsius, Fahrenheit }

public static class TemperatureFormatter
{
    public static double Convert(double celsius, TemperatureUnit unit) => unit switch
    {
        TemperatureUnit.Celsius => celsius,
        TemperatureUnit.Fahrenheit => celsius * 9d / 5d + 32d,
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    public static string Format(double celsius, TemperatureUnit unit, string format = "0") =>
        $"{Convert(celsius, unit).ToString(format, System.Globalization.CultureInfo.CurrentCulture)}°{(unit == TemperatureUnit.Celsius ? "C" : "F")}";
}

public enum WeatherFreshness { Current, Stale, Expired }

public static class WeatherFreshnessPolicy
{
    public static WeatherFreshness Get(DateTimeOffset fetchedAtUtc, DateTimeOffset nowUtc)
    {
        var age = nowUtc.ToUniversalTime() - fetchedAtUtc.ToUniversalTime();
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age >= TimeSpan.FromHours(6)) return WeatherFreshness.Expired;
        if (age >= TimeSpan.FromHours(1)) return WeatherFreshness.Stale;
        return WeatherFreshness.Current;
    }
}
