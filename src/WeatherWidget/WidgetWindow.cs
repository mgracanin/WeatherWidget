using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
namespace WeatherWidget;

public sealed partial class WidgetWindow : Window
{
    private readonly App app;
    private static readonly uint TaskbarCreatedMessage=Native.RegisterWindowMessage("TaskbarCreated");
    private nint handle, desktopHost;
    private uint hostProcess;
    private readonly System.Windows.Threading.DispatcherTimer recoveryLift = new() { Interval=TimeSpan.FromSeconds(5) };
    private readonly System.Windows.Threading.DispatcherTimer placementRecovery = new() { Interval=TimeSpan.FromSeconds(2) };
    private bool restoringPlacement;
    private TextBlock status = new();
    private Native.Point? dragOrigin;
    private Native.Rect dragBounds;
    private bool moved;
    private string lastStatus = "";
    public string DesktopStatus { get; private set; } = "Starting desktop host";
    public bool IsNativeWindowAlive => handle != 0 && Native.IsWindow(handle);
    public event EventHandler? CardClicked;
    public WidgetWindow(App app)
    {
        this.app = app;
        Title = "WeatherWidget";
        Width = 220; Height = 110;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = app.InspectionMode; ShowActivated = app.InspectionMode;
        Background = Brushes.Transparent;
        AllowsTransparency = true;
        InitializeCard();
        ContextMenu = CreateContextMenu();
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            // Tool window keeps Alt+Tab/taskbar recovery in the tray.
            if (!app.InspectionMode) Native.SetWindowLongPtr(handle, Native.ExStyle, (nint)(Native.GetWindowLongPtr(handle, Native.ExStyle).ToInt64() | 0x80));
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
        };
        Loaded += (_, _) => { RestorePosition(); ApplyDesktopMode(); };
        recoveryLift.Tick += (_, _) =>
        {
            recoveryLift.Stop();
            if (!IsNativeWindowAlive || app.Settings.AlwaysOnTop || app.InspectionMode) return;
            Native.SetWindowPos(handle, 1, 0, 0, 0, 0, Native.NoActivate|Native.NoMove|Native.NoSize);
            AppSettings.Log("Recovery visibility lift ended; restored desktop Z order.");
        };
        placementRecovery.Tick += (_, _) => { placementRecovery.Stop(); CheckDesktopHost(); RecoverOnScreen(); };
        Closed += (_, _) => { recoveryLift.Stop(); placementRecovery.Stop(); };
        Deactivated += (_, _) =>
        {
            if (IsNativeWindowAlive && !recoveryLift.IsEnabled && !app.Settings.AlwaysOnTop && !app.InspectionMode)
                Native.SetWindowPos(handle, 1, 0, 0, 0, 0, Native.NoActivate|Native.NoMove|Native.NoSize);
        };
        PreviewMouseLeftButtonDown += BeginDrag;
        PreviewMouseMove += Drag;
        PreviewMouseLeftButtonUp += EndDrag;
        LostMouseCapture += (_, _) => { if (dragOrigin != null) { dragOrigin=null; if (moved) SavePosition(); } };
    }
    public ContextMenu CreateContextMenu() => app.BuildWidgetMenu();
    private nint WindowMessage(nint hwnd, int message, nint wp, nint lp, ref bool handled)
    {
        if (message == TaskbarCreatedMessage || message == 0x007E)
            SchedulePlacementRecovery();
        else if (message == 0x0047 && app.Settings.Locked && !restoringPlacement && !IsAtPreferredPosition())
            SchedulePlacementRecovery();
        return 0;
    }
    private bool IsAtPreferredPosition()
    {
        if (!Native.GetWindowRect(handle, out var rect)) return true;
        var desired=DesiredPosition(app.Settings,rect.Left,rect.Top);
        var work=Forms.Screen.FromRectangle(new System.Drawing.Rectangle(desired.X,desired.Y,rect.Width,rect.Height)).WorkingArea;
        var fitted=FitPosition(desired,rect.Width,rect.Height,work);
        return rect.Left==fitted.X && rect.Top==fitted.Y;
    }
    internal void SchedulePlacementRecovery()
    {
        if (!IsNativeWindowAlive) return;
        placementRecovery.Stop(); placementRecovery.Start();
    }
    public void CheckDesktopHost()
    {
        if (!IsNativeWindowAlive || app.Settings.AlwaysOnTop || app.InspectionMode) return;
        Native.GetWindowThreadProcessId(desktopHost, out uint process);
        if (desktopHost == 0 || !Native.IsWindow(desktopHost) || process != hostProcess || Native.GetWindow(handle, 4) != desktopHost)
            ApplyDesktopMode();
        // Shell hide/show and virtual desktop transitions may hide owned popups.
        // This recovery does not prove that Explorer pins us to all desktops.
        if (!Native.IsWindowVisible(handle) || Native.IsIconic(handle))
        {
            Native.ShowWindow(handle, 4);
            AppSettings.Log("Recovered hidden desktop-owned popup.");
        }
    }
    public void ApplyDesktopMode()
    {
        if (!IsNativeWindowAlive) return;
        Native.GetWindowRect(handle, out var bounds);
        Detach();
        if (app.Settings.AlwaysOnTop)
        {
            Native.SetWindowPos(handle, -1, bounds.Left, bounds.Top, 0, 0, Native.NoActivate|Native.NoSize|Native.Show);
            SetStatus(app.Text("Uvijek na vrhu", "Always on top"));
            return;
        }
        if (app.InspectionMode)
        {
            Native.SetWindowPos(handle, 0, bounds.Left, bounds.Top, 0, 0, Native.NoSize|Native.Show);
            SetStatus("Inspection window — desktop host disabled for UI testing");
            return;
        }
        LogShellTree();
        // Keep WPF's layered HWND top-level: a wallpaper child can be composed
        // underneath Explorer's wallpaper despite valid parent/visible flags.
        // Ownership uses a documented API; Progman's virtual-desktop behavior is
        // undocumented shell compatibility behavior requiring actual UI acceptance.
        var shell=Native.GetShellWindow();
        if (shell == 0) shell=Native.FindWindow("Progman",null);
        if (shell != 0 && Native.IsWindow(shell))
        {
            Native.SetWindowLongPtr(handle, Native.Owner, shell);
            if (Native.GetWindow(handle,4)==shell)
            {
                desktopHost=shell;
                Native.GetWindowThreadProcessId(shell,out hostProcess);
                Native.SetWindowPos(handle,1,bounds.Left,bounds.Top,0,0,Native.NoActivate|Native.NoSize|Native.FrameChanged|Native.Show);
                Native.ShowWindow(handle,4);
                SetStatus(app.Text("Vlasnik: radna površina · provjera kompatibilnosti", "Desktop-owned popup · compatibility validation"));
                LogWindow("Attached popup",handle);
                return;
            }
            AppSettings.Log("Shell ownership failed: "+System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
        Native.SetWindowPos(handle,0,bounds.Left,bounds.Top,0,0,Native.NoActivate|Native.NoSize|Native.Show);
        SetStatus(app.Text("Vidljivi rezervni prozor · otvori Status", "Visible recovery window · open Status"));
        LogWindow("Visible fallback",handle);
    }
    private void SetStatus(string text)
    {
        DesktopStatus=text;
        if (lastStatus!=text) { AppSettings.Log(text);lastStatus=text; }
    }
    private void Detach()
    {
        // SetParent is only needed to recover an HWND created by the old child-host
        // implementation. New windows never acquire WS_CHILD or a foreign parent.
        var style=Native.GetWindowLongPtr(handle,Native.Style).ToInt64();
        if ((style & Native.Child)!=0) Native.SetParent(handle,0);
        Native.SetWindowLongPtr(handle,Native.Style,(nint)((style & ~Native.Child)|Native.Popup));
        Native.SetWindowLongPtr(handle,Native.Owner,0);
        Native.SetWindowPos(handle,-2,0,0,0,0,Native.NoActivate|Native.NoMove|Native.NoSize|Native.FrameChanged);
        desktopHost=0;
    }
    private static void LogWindow(string label,nint window)
    {
        var cls=new System.Text.StringBuilder(128);
        Native.GetClassName(window,cls,cls.Capacity);
        Native.GetWindowRect(window,out var rect);
        Native.GetWindowThreadProcessId(window,out uint pid);
        AppSettings.Log($"{label}: hwnd={window} class={cls} pid={pid} owner={Native.GetWindow(window,4)} parentOrOwner={Native.GetParent(window)} visible={Native.IsWindowVisible(window)} rect={rect.Left},{rect.Top},{rect.Width},{rect.Height} style=0x{Native.GetWindowLongPtr(window,Native.Style).ToInt64():X} ex=0x{Native.GetWindowLongPtr(window,Native.ExStyle).ToInt64():X}");
    }
    private static void LogShellTree()
    {
        Native.EnumWindows((window,_) =>
        {
            var cls=new System.Text.StringBuilder(128);
            Native.GetClassName(window,cls,cls.Capacity);
            if (cls.ToString() is not ("Progman" or "WorkerW")) return true;
            LogWindow("Shell root",window);
            Native.EnumChildWindows(window,(child,_)=> { LogWindow("Shell descendant",child);return true; },0);
            return true;
        },0);
    }    private void RestorePosition()
    {
        var work=Forms.Screen.PrimaryScreen!.WorkingArea;
        if (app.Settings.Left is null || app.Settings.Top is null)
        {
            app.Settings.Left ??= work.Right-260; app.Settings.Top ??= work.Top+48;
            app.Settings.Save();
        }
        MoveScreen((int)app.Settings.Left.Value, (int)app.Settings.Top.Value);
        RecoverOnScreen();
    }
    public void RecoverOnScreen()
    {
        if (!IsNativeWindowAlive) return;
        if (!Native.GetWindowRect(handle, out var rect)) return;
        var desired = DesiredPosition(app.Settings, rect.Left, rect.Top);
        var target = new System.Drawing.Rectangle(desired.X, desired.Y, rect.Width, rect.Height);
        var screen=Forms.Screen.FromRectangle(target).WorkingArea;
        var position = FitPosition(desired, rect.Width, rect.Height, screen);
        restoringPlacement = true;
        try
        {
            if (rect.Left != position.X || rect.Top != position.Y) MoveScreen(position.X, position.Y);
            Native.ShowWindow(handle, 4);
        }
        finally { restoringPlacement = false; }
        // A missing monitor may only be temporary. Never replace the user's anchor
        // with a fallback position chosen while displays are reconnecting.
    }
    internal static System.Drawing.Point DesiredPosition(AppSettings settings, int currentX, int currentY) =>
        new((int)(settings.Left ?? currentX), (int)(settings.Top ?? currentY));
    internal static System.Drawing.Point FitPosition(System.Drawing.Point desired, int width, int height, System.Drawing.Rectangle work) =>
        new(Math.Clamp(desired.X, work.Left, Math.Max(work.Left, work.Right-width)),
            Math.Clamp(desired.Y, work.Top, Math.Max(work.Top, work.Bottom-height)));

    public void RevealOnScreen()
    {
        RecoverOnScreen();
        if (!IsNativeWindowAlive) return;
        if (!app.Settings.AlwaysOnTop && !app.InspectionMode)
        {
            Native.SetWindowPos(handle,-1,0,0,0,0,Native.NoActivate|Native.NoMove|Native.NoSize|Native.Show);
            recoveryLift.Stop(); recoveryLift.Start();
            AppSettings.Log("Requested recovery: five-second visibility lift.");
        }
        LogWindow("Explicit reveal",handle);
    }
    private void MoveScreen(int x, int y)
    {
        var point = new Native.Point(x,y);
        var parent=Native.GetParent(handle);
        if (parent != 0 && (Native.GetWindowLongPtr(handle,Native.Style).ToInt64() & Native.Child)!=0) Native.ScreenToClient(parent, ref point);
        Native.SetWindowPos(handle, 0, point.X, point.Y, 0, 0, Native.NoActivate|Native.NoSize|0x4);
    }
    public void SavePosition()
    {
        if (app.Settings.Locked || !IsNativeWindowAlive || !Native.GetWindowRect(handle, out var rect)) return;
        app.Settings.Left=rect.Left; app.Settings.Top=rect.Top; app.Settings.Save();
    }
    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        if (UiChrome.IsInteractiveSource(e.OriginalSource as DependencyObject)) return;
        Native.GetCursorPos(out var point); dragOrigin=point;
        Native.GetWindowRect(handle, out dragBounds); moved=false;
        CaptureMouse(); e.Handled=true;
    }
    private void Drag(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (dragOrigin is not { } origin || e.LeftButton != MouseButtonState.Pressed) return;
        Native.GetCursorPos(out var point);
        if (Math.Abs(point.X-origin.X)+Math.Abs(point.Y-origin.Y) > 5) moved=true;
        if (moved && !app.Settings.Locked) MoveScreen(dragBounds.Left+point.X-origin.X, dragBounds.Top+point.Y-origin.Y);
    }
    private void EndDrag(object sender, MouseButtonEventArgs e)
    {
        if (dragOrigin is null) return;
        dragOrigin=null; ReleaseMouseCapture();
        if (moved && !app.Settings.Locked)
        {
            if (app.Settings.SnapToEdges && !app.Settings.Locked && Native.GetWindowRect(handle,out var bounds))
            {
                var area=Forms.Screen.FromRectangle(new System.Drawing.Rectangle(bounds.Left,bounds.Top,bounds.Width,bounds.Height)).WorkingArea;
                int distance=(int)Math.Round(12*System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX);
                var snapped=EdgeSnap.Position(bounds.Left,bounds.Top,bounds.Width,bounds.Height,area.Left,area.Top,area.Right,area.Bottom,distance);
                MoveScreen(snapped.X,snapped.Y);
            }
            SavePosition();
            RecoverOnScreen();
        }
        else if (!moved) CardClicked?.Invoke(this, EventArgs.Empty);
        e.Handled=true;
    }
}
