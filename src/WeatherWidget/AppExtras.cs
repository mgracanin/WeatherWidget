using System.Globalization;
using WeatherWidget.Core;
namespace WeatherWidget;

public sealed partial class App
{
    private WeatherSnapshot? lastNotificationSnapshot;

    private void UpdateExtras()
    {
        var data=Weather.Snapshot;
        var now=DateTimeOffset.UtcNow;
        if (tray == null) return;
        tray.Text=BuildTrayText(Settings,data,Weather.Offline,now);
        // Never alert from cached/offline data or start a new timer for notifications.
        if (data == null || Weather.Offline || Weather.IsLoading || ReferenceEquals(lastNotificationSnapshot,data)) return;
        lastNotificationSnapshot=data;
        var due=WeatherExtras.TakeNotifications(Settings,data,now,DateTimeOffset.Now);
        if (due.Count==0) return;
        var message=string.Join("\n",due.Select(n=>n.Message));
        var title="WeatherWidget · "+Settings.CityName;
        tray.ShowBalloonTip(8000,title[..Math.Min(63,title.Length)],message[..Math.Min(255,message.Length)],System.Windows.Forms.ToolTipIcon.Info);
        Settings.Save();
    }

    internal static string BuildTrayText(AppSettings settings,WeatherSnapshot? data,bool offline,DateTimeOffset now)
    {
        string city=settings.CityName[..Math.Min(18,settings.CityName.Length)];
        if (data == null) return "WeatherWidget · "+city;
        bool fahrenheit=settings.TemperatureUnit=="fahrenheit";
        string Temp(double c)=>(fahrenheit?c*9/5+32:c).ToString("0",UiText.Culture(settings.Language))+"°"+(fahrenheit?"F":"C");
        string state=offline ? UiText.T(settings.Language,"Izvan mreže","Offline") : !WeatherExtras.IsFresh(data,now) ? UiText.T(settings.Language,"Zastarjelo","Stale") : WeatherExtras.RainSoon(data,now,settings.Language);
        string arrow=offline ? "" : TemperatureTrend.Get(data,now) switch {TemperatureTrendDirection.Rising=>" ↑",TemperatureTrendDirection.Falling=>" ↓",TemperatureTrendDirection.Steady=>" →",_=>""};
        string text=$"{city} · {Temp(data.Current.TemperatureC)}{arrow}\n"+UiText.T(settings.Language,"Osjet ","Feels ")+Temp(data.Current.FeelsLikeC)+(state.Length>0?"\n"+state:"");
        // The conservative legacy shell limit also works on modern Windows.
        return text[..Math.Min(63,text.Length)];
    }
}
