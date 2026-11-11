using Microsoft.Win32;
using System.Windows;

namespace WeatherWidget;
public sealed partial class App
{
    private SettingsWindow? settingsWindow;
    private void ShowSettings()
    {
        CloseForecast();
        if(settingsWindow!=null)
        {
            if(settingsWindow.WindowState==WindowState.Minimized) settingsWindow.WindowState=WindowState.Normal;
            settingsWindow.Activate();return;
        }
        var window=new SettingsWindow(Settings.CreateDraft(),Weather.Client,ApplySettings,ShowStatus);
        settingsWindow=window;
        window.Closed+=(_,_)=>{if(ReferenceEquals(settingsWindow,window)) settingsWindow=null;};
        window.Show();
    }
    private bool ApplySettings(AppSettings draft)
    {
        bool host=Settings.AlwaysOnTop!=draft.AlwaysOnTop;
        bool city=Settings.Latitude!=draft.Latitude || Settings.Longitude!=draft.Longitude || Settings.CityName!=draft.CityName;
        bool notifications=Settings.WeatherNotifications!=draft.WeatherNotifications;
        if(!SettingsChanges.Commit(Settings,draft,ConfigureStartup,out var error))
        {
            AppSettings.Log("Settings were not applied: "+error);
            MessageBox.Show(settingsWindow!,Text("Postavke nisu spremljene. ","Settings were not saved. ")+error,
                Text("Postavke","Settings"),MessageBoxButton.OK,MessageBoxImage.Error);
            return false;
        }
        if(city) {CloseForecast();_ = RefreshWeather(true);}
        else if(notifications) lastNotificationSnapshot=null;
        PreferenceChanged(host);
        return true;
    }
    private static void ConfigureStartup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if(!enabled) {key.DeleteValue("WeatherWidget",false);return;}
        var executable=Environment.ProcessPath ?? throw new IOException("Executable path unavailable.");
        var command="\""+executable+"\"";
        if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
            command+=" \""+Path.Combine(AppContext.BaseDirectory,"WeatherWidget.dll")+"\"";
        key.SetValue("WeatherWidget",command);
    }
}
