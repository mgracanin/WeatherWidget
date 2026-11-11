using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace WeatherWidget;
public sealed partial class App : System.Windows.Application
{
    private Mutex? instanceMutex;
    private EventWaitHandle? showEvent, exitEvent;
    private RegisteredWaitHandle? showWait, exitWait;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private WidgetWindow? widget;
    private readonly DispatcherTimer recoveryTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool exiting;
    public bool InspectionMode { get; private set; }
    public AppSettings Settings { get; private set; } = new();
    internal WeatherController Weather { get; private set; } = null!;
    public string Text(string hr, string en) => UiText.T(Settings.Language, hr, en);
    [STAThread]
    public static void Main() => new App { ShutdownMode = ShutdownMode.OnExplicitShutdown }.Run();
    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
        InspectionMode = e.Args.Contains("--inspect");
#endif
        if (e.Args.Contains("--exit"))
        {
            try { using var signal=EventWaitHandle.OpenExisting(@"Local\WeatherWidget.Exit"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            Shutdown(); return;
        }
        instanceMutex = new Mutex(true, @"Local\WeatherWidget.Instance", out bool created);
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\WeatherWidget.Show");
        if (!created) { showEvent.Set(); Shutdown(); return; }
        exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\WeatherWidget.Exit");
        DispatcherUnhandledException += (_, args) => AppSettings.Log("Unhandled dispatcher exception: " + args.Exception);
        Settings = AppSettings.Load();
        Weather = new WeatherController(Settings, Dispatcher);
        Weather.Changed += PresentationChanged;
        AppSettings.Log("Starting WeatherWidget; OS " + Environment.OSVersion + "; inspect=" + InspectionMode);
        showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => Dispatcher.BeginInvoke(() => Recover(true)), null, Timeout.Infinite, false);
        exitWait = ThreadPool.RegisterWaitForSingleObject(exitEvent, (_, _) => Dispatcher.BeginInvoke(ExitWidget), null, Timeout.Infinite, false);
        trayIcon = LoadTrayIcon();
        tray = new Forms.NotifyIcon { Icon = trayIcon ?? System.Drawing.SystemIcons.Application, Text = "WeatherWidget", Visible = true, ContextMenuStrip = BuildTrayMenu() };
        tray.DoubleClick += (_, _) => Recover(true);
        Recover(false);
        recoveryTimer.Tick += (_, _) => Recover(false);
        recoveryTimer.Start();
        StartLifecycle();
        _ = RefreshWeather();
        base.OnStartup(e);
    }
    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            foreach (Forms.ToolStripItem item in menu.Items.Cast<Forms.ToolStripItem>().ToArray()) item.Dispose();
            menu.Items.Clear();
            foreach (var item in BuildWidgetMenu().Items)
                menu.Items.Add(ConvertMenu(item));
        };
        return menu;
    }
    private static Forms.ToolStripItem ConvertMenu(object item)
    {
        if (item is not System.Windows.Controls.MenuItem source) return new Forms.ToolStripSeparator();
        var result = new Forms.ToolStripMenuItem(source.Header?.ToString()) { Checked=source.IsChecked, Enabled=source.IsEnabled };
        if (source.HasItems)
            foreach (var child in source.Items) result.DropDownItems.Add(ConvertMenu(child));
        else result.Click += (_, _) => source.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent, source));
        return result;
    }
    public void ShowStatus()
    {
        var data=Weather?.Snapshot;
        var fetched=data is null ? Text("Još nema vremenskih podataka.", "No weather data yet.") :
            Text("Dohvaćeno: ", "Fetched: ") + data.FetchedAtUtc.ToLocalTime().ToString("g", UiText.Culture(Settings.Language));
        System.Windows.MessageBox.Show((widget?.DesktopStatus ?? "Window unavailable.") + "\n\n" + fetched +
            "\n" + (Weather?.Error ?? "") + "\n\nOpen-Meteo · CC BY 4.0\n" + AppSettings.DataDirectory,
            "WeatherWidget", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void Recover(bool requested)
    {
        if (exiting) return;
        if (widget is null || !widget.IsNativeWindowAlive)
        {
            widget = new WidgetWindow(this);
            widget.CardClicked += (_, _) => ToggleForecast();
            MainWindow = widget;
            widget.Show();
            widget.UpdateWeatherPresentation();
            AppSettings.Log("Created widget window.");
        }
        widget.CheckDesktopHost();
        if (requested) widget.RevealOnScreen();
    }
    public void ExitWidget() { exiting = true; widget?.SavePosition(); Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        exiting = true;
        recoveryTimer.Stop();
        StopLifecycle();
        forecast?.Close();
        Weather?.Dispose();
        showWait?.Unregister(null); exitWait?.Unregister(null);
        tray?.Dispose();
        trayIcon?.Dispose();
        showEvent?.Dispose(); exitEvent?.Dispose();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
    private static System.Drawing.Icon? LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "weather-widget.ico");
        try { return new System.Drawing.Icon(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            AppSettings.Log("WeatherWidget tray icon unavailable; using system fallback: " + ex.Message);
            return null;
        }
    }
}


