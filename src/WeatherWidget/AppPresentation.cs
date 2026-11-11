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
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) Weather?.ScheduleRefresh(); }
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
        widget?.UpdateWeatherPresentation();
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
        Settings.Save(); PresentationChanged();
        if (widget != null)
        {
            widget.ContextMenu=BuildWidgetMenu();
            if (host) widget.ApplyDesktopMode();
            widget.RecoverOnScreen();
        }
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
        menu.Items.Add(Command(Text("Promijeni grad…","Change city…"),ChooseCity));
        MenuItem Options(string label, IEnumerable<(string Label,bool Selected,Action Set)> options)
        {
            var parent=new MenuItem { Header=label };
            foreach (var option in options) parent.Items.Add(Command(option.Label,option.Set,option.Selected));
            menu.Items.Add(parent); return parent;
        }
        Options(Text("Izgled","Theme"), new[] {
            ("Modern", Settings.Theme=="modern", (Action)(()=>{ Settings.Theme="modern"; PreferenceChanged(); })),
            ("Classic", Settings.Theme=="classic", (Action)(()=>{ Settings.Theme="classic"; PreferenceChanged(); })) });
        Options(Text("Veličina","Scale"), new[] {.75,1,1.25,1.5,2}.Select(scale => (
            (scale*100).ToString("0")+"%", Settings.Scale==scale, (Action)(()=>{ Settings.Scale=scale; PreferenceChanged(); }))));
        menu.Items.Add(Command(Text("Zaključaj položaj","Lock position"),()=>{ Settings.Locked=!Settings.Locked; PreferenceChanged(); },Settings.Locked));
        menu.Items.Add(Command(Text("Uvijek na vrhu","Always on top"),()=>{ Settings.AlwaysOnTop=!Settings.AlwaysOnTop; PreferenceChanged(true); },Settings.AlwaysOnTop));
        Options(Text("Temperatura","Temperature"),new[] {("°C","celsius"),("°F","fahrenheit")}.Select(pair=>(
            pair.Item1,Settings.TemperatureUnit==pair.Item2,(Action)(()=>{Settings.TemperatureUnit=pair.Item2;PreferenceChanged();}))));
        Options(Text("Vjetar","Wind"),new[] {("km/h","kmh"),("m/s","ms"),("mph","mph")}.Select(pair=>(
            pair.Item1,Settings.WindUnit==pair.Item2,(Action)(()=>{Settings.WindUnit=pair.Item2;PreferenceChanged();}))));
        Options(Text("Jezik","Language"),new[] {("Hrvatski","hr"),("English","en"),(Text("Prema sustavu Windows","Windows default"),"system")}.Select(pair=>(
            pair.Item1,Settings.Language==pair.Item2,(Action)(()=>{Settings.Language=pair.Item2;PreferenceChanged();}))));
        menu.Items.Add(Command(Text("Pokreni sa sustavom Windows","Start with Windows"),ToggleStartup,Settings.StartWithWindows));
        menu.Items.Add(new Separator());
        menu.Items.Add(Command("Status",ShowStatus));
        menu.Items.Add(Command(Text("Izlaz","Exit"),ExitWidget));
        return menu;
    }
    private async void ChooseCity()
    {
        CloseForecast();
        var picker=new CityPicker(Weather.Client,Settings.Language,Settings.CityName);
        if (picker.ShowDialog()!=true || picker.SelectedLocation is not { } city) return;
        Settings.CityName=city.Name; Settings.Latitude=city.Latitude; Settings.Longitude=city.Longitude;
        Settings.Save();
        await RefreshWeather(true);
    }
    private void ToggleStartup()
    {
        try
        {
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (Settings.StartWithWindows) key.DeleteValue("WeatherWidget",false);
            else
            {
                var executable=Environment.ProcessPath ?? throw new IOException("Executable path unavailable.");
                var command="\""+executable+"\"";
                if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
                    command+=" \""+Path.Combine(AppContext.BaseDirectory,"WeatherWidget.dll")+"\"";
                key.SetValue("WeatherWidget",command);
            }
            Settings.StartWithWindows=!Settings.StartWithWindows; PreferenceChanged();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            AppSettings.Log("Startup change failed: "+ex.Message);
            System.Windows.MessageBox.Show(Text("Promjena automatskog pokretanja nije uspjela: ","Could not change startup: ")+ex.Message,"WeatherWidget");
        }
    }
}

