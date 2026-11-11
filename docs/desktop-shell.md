# Desktop shell prototype

The shell is a .NET 10 WPF application. It uses a named per-session mutex and event to keep one instance; a second launch asks the first instance to recover its card. The tray offers recovery, lock, always-on-top, status and exit. Settings and a size-limited shell.log live under %LOCALAPPDATA%\WeatherWidget. Position is stored in physical screen pixels and clamped to a current monitor work area on startup, recovery, monitor change and drag completion. Negative monitor origins are valid.

## Build and launch

From D:\Development\WeatherWidget:

    .\tools\dotnet\dotnet.exe build .\src\WeatherWidget\WeatherWidget.csproj -c Debug
    $env:DOTNET_ROOT = Join-Path (Get-Location) "tools\dotnet"
    .\bin\Debug\net10.0-windows\WeatherWidget.exe

This is a framework-dependent development build. The output executable is bin\Debug\net10.0-windows\WeatherWidget.exe; to launch it directly with the bundled runtime, set DOTNET_ROOT to the absolute tools\dotnet directory first. A self-contained portable publish belongs to release packaging.

## Compatibility boundary

Win32 SetParent and SetWindowPos are documented APIs, but Explorer's WorkerW discovery, its SHELLDLL_DefView tree and Progman message 0x052C are **undocumented shell behavior**. This prototype explicitly depends on that behavior for the requested behind-applications, Win+D-visible and all-virtual-desktops mode. Microsoft does not promise those behaviors for this hosting technique. It is an experiment requiring actual Windows acceptance, not a supported shell extension contract.

The app finds a wallpaper WorkerW after the desktop icon host (with a child-WorkerW alternative for newer Explorer), checks DPI awareness compatibility, sets the WS_CHILD/WS_POPUP styles required by SetParent, and verifies the resulting parent. It does not inject code, terminate or restart Explorer, install hooks into Explorer, or alter registry shell settings.

If Explorer's host cannot be found, the DPI contexts differ, or parenting fails, the card falls back to a normal non-taskbar window placed at the bottom of the application Z order. Its status explicitly says "Fallback window"; that mode does **not** establish the Win+D/all-desktops requirements. Tray recovery shows and clamps the card again. Always-on-top deliberately detaches from the desktop host into an ordinary topmost window; virtual-desktop and Win+D shell behavior therefore differs in that opt-in mode.

A three-second dispatcher timer checks HWND validity, parent identity and owning process. If Explorer destroys the card's native child window, App creates a new WPF card using the persisted screen position. A TaskbarCreated/WM_DISPLAYCHANGE handler also requests recovery, while polling covers broadcasts that child windows might not receive. The NotifyIcon wrapper handles taskbar recreation. Automatic recovery is implemented but Explorer restart has not been exercised by the implementing subagent.

Cross-process parenting between incompatible DPI awareness modes can force-reset a process's awareness. The app checks context equality first and falls back rather than knowingly requesting such a reset. Parent and monitor DPI behavior still needs Windows verification.

## Evidence and acceptance still required

Integrated builds now include weather, views and tests. See validation.md for actual evidence and remaining Windows checks.

No claim of runtime desktop acceptance follows from compilation. Verify all of the following in the actual Windows session:

- Card appears initially, drag and lock work, second launch recovers the first instance, restart restores position.
- Ordinary application windows cover the card; Win+D keeps it visible and interactive in desktop-host mode.
- Switch and create virtual desktops, including with applications maximized; card remains present.
- Restart Explorer through the owner's normal UI and verify card and tray recovery, without intentionally killing Explorer in application code.
- Disconnect a monitor and recover through the tray; check monitors to the left/above the primary display.
- Inspect and drag at 100%, 150%, 200% and mixed monitor scaling.
- Compare host mode, visible fallback status and optional always-on-top behavior.
- Corrupt settings recovery and permission failures do not terminate the app.

Primary API references:

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent — required style changes and cross-process DPI caveats.
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos — bottom/topmost ordering and placement.
