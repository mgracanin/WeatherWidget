using System.IO;
using WeatherWidget;
using WeatherWidget.Core;

internal static class ExtraChecks
{
    private static readonly DateTimeOffset Now=new(2026,11,11,12,30,0,TimeSpan.Zero);
    private static WeatherSnapshot Sample()=>new(new("Zagreb",45.8,16),Now,"Europe/Zagreb",new(Now,8,6,80,10,3,true),
        new[]{new HourlyForecast(Now.AddMinutes(30),9,70,61,true)},Array.Empty<DailyForecast>());
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    public static Task Rain()
    {
        var data=Sample();
        Check(WeatherExtras.RainSoon(data,Now,"en").Contains("1 h"),"Rain hour missing.");
        Check(WeatherExtras.RainSoon(data,Now,"hr").StartsWith("Kiša"),"Croatian rain text missing.");
        Check(WeatherExtras.RainSoon(data,Now.AddHours(2),"en")=="","Stale data must not predict imminent rain.");
        Check(WeatherExtras.RainSoon(data with {Hourly=new[]{data.Hourly[0] with {PrecipitationProbability=59}}},Now,"en")=="","Rain threshold failed.");
        Check(WeatherExtras.RainSoon(data with {Hourly=new[]{data.Hourly[0] with {WeatherCode=71}}},Now,"en")=="","Snow is not rain.");
        Check(WeatherExtras.RainSoon(data with {Hourly=new[]{data.Hourly[0] with {TimeUtc=Now.AddHours(4)}}},Now,"en")=="","Distant rain should not be imminent.");
        Check(WeatherExtras.RainSoon(data with {Hourly=new[]{data.Hourly[0] with {TimeUtc=Now.AddMinutes(-30)}}},Now,"en").Contains("this hour"),"Current hourly period missing.");
        return Task.CompletedTask;
    }
    public static Task Notifications()
    {
        var data=Sample() with {Current=Sample().Current with {TemperatureC=-1,WindKmh=50}};
        var settings=new AppSettings {Language="en"};
        Check(WeatherExtras.TakeNotifications(settings,data,Now,Now).Count==0,"Notifications must default off.");
        settings.WeatherNotifications=true;
        var quiet=Now.AddHours(10); // 22:30 on the computer clock.
        Check(WeatherExtras.TakeNotifications(settings,data,Now,quiet).Count==0 && settings.AlertHistory.Count==0,"Quiet hours consumed alert history.");
        Check(WeatherExtras.IsQuiet(quiet,22,7)&&WeatherExtras.IsQuiet(Now.AddHours(-6),22,7)&&!WeatherExtras.IsQuiet(Now.AddHours(-5),22,7),"Overnight quiet boundaries failed.");
        Check(WeatherExtras.TakeNotifications(settings,data,Now,Now).Count==3,"Expected rain, frost and wind reminders.");
        Check(WeatherExtras.TakeNotifications(settings,data,Now.AddMinutes(15),Now).Count==0,"Repeated refresh must not repeat alerts.");
        var path=Path.Combine(Path.GetTempPath(),"weather-alerts-"+Guid.NewGuid()+".json");
        try
        {
            settings.Save(path,_=>{});settings=AppSettings.Load(path,_=>{});
            Check(WeatherExtras.TakeNotifications(settings,data,Now.AddMinutes(15),Now).Count==0,"Restart lost cooldown.");
            var later=Now.AddHours(6);
            var fresh=data with {FetchedAtUtc=later,Current=data.Current with {TimeUtc=later},Hourly=new[]{data.Hourly[0] with {TimeUtc=later.AddMinutes(30)}}};
            Check(WeatherExtras.TakeNotifications(settings,fresh,later,later).Count==3,"Six-hour cooldown did not expire.");
            Check(WeatherExtras.TakeNotifications(settings,fresh with {Location=new("Split",43.5,16.4)},later,later).Count==3,"Location change was suppressed.");
            Check(settings.AlertHistory.Count==3,"History is not bounded.");
            settings.QuietStart=99;settings.QuietEnd=-1;settings.AlertHistory["unknown"]=Now;settings.Save(path,_=>{});
            var loaded=AppSettings.Load(path,_=>{});
            Check(loaded.QuietStart==22&&loaded.QuietEnd==7&&!loaded.AlertHistory.ContainsKey("unknown"),"Malformed notification preferences not normalized.");
        }
        finally {File.Delete(path);}
        return Task.CompletedTask;
    }
    public static Task Snapping()
    {
        Check(EdgeSnap.Position(-1075,-95,130,67,-1080,-100,0,980,12)==(-1080,-100),"Negative monitor origin snapping failed.");
        Check(EdgeSnap.Position(-139,904,130,67,-1080,-100,0,980,12)==(-130,913),"Right/bottom snapping failed.");
        Check(EdgeSnap.Position(100,100,130,67,0,0,1920,1080,12)==(100,100),"Distant window unexpectedly snapped.");
        Check(EdgeSnap.Position(500,500,2000,1200,0,0,1920,1080,12)==(0,0),"Oversized window failed.");
        return Task.CompletedTask;
    }
    public static Task TrayTooltip()
    {
        var settings=new AppSettings {CityName=new string('x',120),Language="en"};
        var text=App.BuildTrayText(settings,Sample(),false,Now);
        Check(text.Length<=63&&text.Contains("8°C")&&text.Contains("Feels 6°C"),"Tray tooltip length/temperature failed.");
        settings.TemperatureUnit="fahrenheit";
        Check(App.BuildTrayText(settings,Sample(),false,Now).Contains("46°F"),"Tray units failed.");
        Check(App.BuildTrayText(settings,Sample(),true,Now).Contains("Offline"),"Offline marker missing.");
        Check(App.BuildTrayText(settings,null,false,Now).Length<=63,"No-data tooltip overflow.");
        return Task.CompletedTask;
    }
}
