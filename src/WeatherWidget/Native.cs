using System.Runtime.InteropServices;
using System.Text;
namespace WeatherWidget;
internal static class Native
{
    internal const int Style = -16, ExStyle = -20;
    internal const long Child = 0x40000000, Popup = 0x80000000;
    internal const uint NoActivate = 0x10, NoSize = 1, NoMove = 2, FrameChanged = 0x20, Show = 0x40;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public int Width => Right-Left; public int Height => Bottom-Top; }
    internal delegate bool EnumProc(nint hwnd, nint data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern nint FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, nint data);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll", SetLastError=true)] internal static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW", SetLastError=true)] internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint hwnd, ref Point point);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern nint SendMessageTimeout(nint hwnd, uint message, nint wp, nint lp, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] internal static extern nint GetWindowDpiAwarenessContext(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool AreDpiAwarenessContextsEqual(nint first, nint second);
}
