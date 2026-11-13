using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;

namespace WeatherWidget;
public sealed partial class App
{
    private ForecastWindow? forecast;
    private readonly System.Windows.Threading.DispatcherTimer ageTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private void StartLifecycle()
    {
        NetworkChange.NetworkAvailabilityChanged += NetworkChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        ageTimer.Tick += (_, _) => PresentationChanged();
        ageTimer.Start();
    }
    private void StopLifecycle()
    {
        NetworkChange.NetworkAvailabilityChanged -= NetworkChanged;
        SystemEvents.PowerModeChanged -= PowerChanged;
        ageTimer.Stop();
    }
    private void NetworkChanged(object? sender, NetworkAvailabilityEventArgs e) { if (e.IsAvailable) Weather?.ScheduleRefresh(); }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) { Weather?.ScheduleRefresh(); Dispatcher.BeginInvoke(() => { if (!exiting) widget?.SchedulePlacementRecovery(); }); } }
    private async Task RefreshWeather(bool changedCity=false)
    {
        try { await Weather.RefreshAsync(changedCity); }
        catch (Exception ex)
        {
            AppSettings.Log("Refresh failed: " + ex);
            if (!exiting) System.Windows.MessageBox.Show(Text("Podaci nisu dostupni. ", "Weather unavailable. ") + ex.Message, "WeatherWidget");
        }
    }
    private void PresentationChanged()
    {
        if (exiting) return;
        UpdateExtras();
        if(widget?.IsNativeWindowAlive==true) widget.UpdateWeatherPresentation();
        if (forecast != null && Weather.Snapshot is { } data)
            forecast.Update(data, Settings.Language, Settings.TemperatureUnit == "fahrenheit" ? "F":"C",
                Settings.WindUnit switch { "ms"=>"m/s", "mph"=>"mph", _=>"km/h" }, Weather.Offline, Weather.Error);
    }
    private void CloseForecast()
    {
        var panel = forecast;
        forecast = null;
        panel?.RequestClose();
    }
    private void ToggleForecast()
    {
        if (forecast != null) { CloseForecast(); return; }
        if (Weather.Snapshot == null) { ShowStatus(); return; }
        var panel = new ForecastWindow(Settings);
        forecast = panel;
        panel.Closed += (_, _) => { if (ReferenceEquals(forecast, panel)) forecast = null; };
        PresentationChanged();
        panel.Loaded += (_, _) => PositionForecast();
        panel.Show();
    }
    private void PositionForecast()
    {
        if (forecast is null || widget is null) return;
        var panel=new WindowInteropHelper(forecast).Handle;
        var card=new WindowInteropHelper(widget).Handle;
        if (!Native.GetWindowRect(card, out var anchor)) return;
        Native.GetWindowRect(panel, out var size);
        var area=System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(anchor.Left,anchor.Top,anchor.Width,anchor.Height)).WorkingArea;
        int width=Math.Min(size.Width, area.Width), height=Math.Min(size.Height,area.Height);
        int x=anchor.Right+10;
        if (x+width > area.Right) x=anchor.Left-width-10;
        x=Math.Clamp(x,area.Left,Math.Max(area.Left,area.Right-width));
        int y=Math.Clamp(anchor.Top,area.Top,Math.Max(area.Top,area.Bottom-height));
        Native.SetWindowPos(panel, 0, x,y,width,height,0x14);
    }
    private void PreferenceChanged(bool host=false)
    {
        PresentationChanged();
        if (widget?.IsNativeWindowAlive==true)
        {
            widget.ContextMenu=BuildWidgetMenu();
            if (host) widget.ApplyDesktopMode();
            widget.RecoverOnScreen();
            Settings.Save();
        }
        else {Settings.Save();Recover(false);}
    }
    public ContextMenu BuildWidgetMenu()
    {
        var menu=new ContextMenu();
        MenuItem Command(string label, Action action, bool? check=null)
        {
            var item=new MenuItem { Header=label, IsCheckable=check.HasValue, IsChecked=check ?? false };
            item.Click += (_, _) => action();
            return item;
        }
        menu.Items.Add(Command(Text("Prikaži / vrati na zaslon","Show / recover on screen"),()=>Recover(true)));
        menu.Items.Add(Command(Text("Osvježi","Refresh"),()=>_ = RefreshWeather()));
        menu.Items.Add(Command(Text("Prikaži prognozu","Show forecast"),()=>{ if(forecast==null) ToggleForecast(); else forecast.Activate(); }));
        menu.Items.Add(Command(Text("Zaključaj položaj","Lock position"),()=>{Settings.Locked=!Settings.Locked;PreferenceChanged();},Settings.Locked));
        menu.Items.Add(new Separator());
        menu.Items.Add(Command(Text("Postavke…","Settings…"),ShowSettings));
        menu.Items.Add(Command(Text("Izlaz","Exit"),ExitWidget));
        return menu;
    }
}
