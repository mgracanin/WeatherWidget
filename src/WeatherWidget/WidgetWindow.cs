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
    private nint handle, desktopHost;
    private uint hostProcess;
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
        PreviewMouseLeftButtonDown += BeginDrag;
        PreviewMouseMove += Drag;
        PreviewMouseLeftButtonUp += EndDrag;
        LostMouseCapture += (_, _) => { if (dragOrigin != null) { dragOrigin=null; SavePosition(); } };
    }
    public ContextMenu CreateContextMenu() => app.BuildWidgetMenu();
    private nint WindowMessage(nint hwnd, int message, nint wp, nint lp, ref bool handled)
    {
        if (message == Native.RegisterWindowMessage("TaskbarCreated") || message == 0x007E)
            Dispatcher.BeginInvoke(() => { CheckDesktopHost(); RecoverOnScreen(); });
        return 0;
    }
    public void CheckDesktopHost()
    {
        if (!IsNativeWindowAlive) return;
        if (app.Settings.AlwaysOnTop || app.InspectionMode) return;
        Native.GetWindowThreadProcessId(desktopHost, out uint process);
        if (desktopHost == 0 || !Native.IsWindow(desktopHost) || process != hostProcess || Native.GetParent(handle) != desktopHost)
            ApplyDesktopMode();
    }
    public void ApplyDesktopMode()
    {
        if (!IsNativeWindowAlive) return;
        Native.GetWindowRect(handle, out var bounds);
        Detach();
        if (app.Settings.AlwaysOnTop)
        {
            Native.SetWindowPos(handle, -1, 0, 0, 0, 0, Native.NoActivate|Native.NoMove|Native.NoSize);
            SetStatus(app.Text("Uvijek na vrhu", "Always on top"));
            return;
        }
        if (app.InspectionMode) { SetStatus("Inspection window — desktop host disabled for UI testing"); return; }
        var host = FindDesktopHost();
        AppSettings.Log($"Host candidate={host}; widget DPI={Native.GetWindowDpiAwarenessContext(handle)}; host DPI={(host == 0 ? 0 : Native.GetWindowDpiAwarenessContext(host))}");
        // Avoid the documented cross-process DPI reset on incompatible hosts.
        if (host != 0 && Native.AreDpiAwarenessContextsEqual(Native.GetWindowDpiAwarenessContext(handle), Native.GetWindowDpiAwarenessContext(host)))
        {
            var style = Native.GetWindowLongPtr(handle, Native.Style).ToInt64();
            Native.SetWindowLongPtr(handle, Native.Style, (nint)((style & ~Native.Popup) | Native.Child));
            Native.SetParent(handle, host);
            if (Native.GetParent(handle) == host)
            {
                desktopHost = host;
                Native.GetWindowThreadProcessId(host, out hostProcess);
                MoveScreen(bounds.Left, bounds.Top);
                Native.ShowWindow(handle, 4);
                SetStatus(app.Text("Radna površina · probni način", "Desktop · compatibility mode"));
                return;
            }
            AppSettings.Log("SetParent failed: " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            Detach();
        }
        MoveScreen(bounds.Left, bounds.Top);
        Native.SetWindowPos(handle, 1, 0, 0, 0, 0, Native.NoActivate|Native.NoMove|Native.NoSize|Native.Show);
        SetStatus(app.Text("Rezervni prozor · otvori Status", "Fallback window · open Status"));
    }
    private void SetStatus(string text)
    {
        DesktopStatus = text;


        if (lastStatus != text) { AppSettings.Log(text); lastStatus=text; }
    }
    private void Detach()
    {
        Native.SetParent(handle, 0);
        var style = Native.GetWindowLongPtr(handle, Native.Style).ToInt64();
        Native.SetWindowLongPtr(handle, Native.Style, (nint)((style & ~Native.Child)|Native.Popup));
        Native.SetWindowPos(handle, -2, 0, 0, 0, 0, Native.NoActivate|Native.NoMove|Native.NoSize|Native.FrameChanged);
        desktopHost=0;
    }
    private static nint FindDesktopHost()
    {
        // Explorer's 0x052C and WorkerW topology are undocumented and may change.
        var progman = Native.FindWindow("Progman", null);
        if (progman == 0) return 0;
        Native.SendMessageTimeout(progman, 0x052C, 0, 0, 2, 200, out _);
        nint result=0;
        Native.EnumWindows((window, _) =>
        {
            if (Native.FindWindowEx(window, 0, "SHELLDLL_DefView", null) == 0) return true;
            var worker = Native.FindWindowEx(0, window, "WorkerW", null);
            if (worker != 0 && Native.IsWindowVisible(worker)) result=worker;
            // Recent Explorer versions may keep the wallpaper WorkerW below Progman.
            if (result == 0) result=Native.FindWindowEx(progman, 0, "WorkerW", null);
            return result == 0;
        }, 0);
        return result;
    }
    private void RestorePosition()
    {
        var work=Forms.Screen.PrimaryScreen!.WorkingArea;
        MoveScreen((int)(app.Settings.Left ?? work.Right-260), (int)(app.Settings.Top ?? work.Top+48));
        RecoverOnScreen();
    }
    public void RecoverOnScreen()
    {
        if (!IsNativeWindowAlive) return;
        Native.GetWindowRect(handle, out var rect);
        var target = new System.Drawing.Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
        var screen=Forms.Screen.FromRectangle(target).WorkingArea;
        MoveScreen(Math.Clamp(rect.Left, screen.Left, Math.Max(screen.Left, screen.Right-rect.Width)), Math.Clamp(rect.Top, screen.Top, Math.Max(screen.Top, screen.Bottom-rect.Height)));
        Native.ShowWindow(handle, 4);
        SavePosition();
    }
    private void MoveScreen(int x, int y)
    {
        var point = new Native.Point(x,y);
        var parent=Native.GetParent(handle);
        if (parent != 0) Native.ScreenToClient(parent, ref point);
        Native.SetWindowPos(handle, 0, point.X, point.Y, 0, 0, Native.NoActivate|Native.NoSize|0x4);
    }
    public void SavePosition()
    {
        if (!IsNativeWindowAlive || !Native.GetWindowRect(handle, out var rect)) return;
        app.Settings.Left=rect.Left; app.Settings.Top=rect.Top; app.Settings.Save();
    }
    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
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
        if (moved) { RecoverOnScreen(); SavePosition(); }
        else CardClicked?.Invoke(this, EventArgs.Empty);
        e.Handled=true;
    }
}
