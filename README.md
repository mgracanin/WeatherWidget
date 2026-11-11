# WeatherWidget

Standalone Windows 11 weather widget. Native C# / WPF on .NET 10, with no XWidget dependency or AI-model calls.

## Run

Open `artifacts/win-x64/WeatherWidget.exe` after building a portable release. Keep its `Assets` folder beside the executable. The self-contained x64 package includes its runtime; no SDK, XWidget, browser or API key is needed on the target PC.

The widget runs in the notification area (system tray), without a taskbar button. Its sun/cloud icon is available as editable vector artwork under Assets/Brand. The compact card defaults to the original 130 x 67 size. Drag it to move it; use its lower-right grip or Ctrl+wheel to resize proportionally. Click without dragging to expand forecasts. The detail view defaults to 330 x 243, uses the matching larger background, and can be moved by its header and resized from its lower-right grip. Both sizes are saved. Escape, the close button or clicking outside collapses the expanded view. Right-click the card or tray icon for settings. Double-click the tray icon or launch the app again to recover the existing widget. Exit through the menu. Startup with Windows is off by default and can be enabled/disabled in the menu.

## Features

- Seven selectable themes: Modern, Classic, Aurora, Paper, Sunset, Nordic and Forest. The five newer themes use cached, scalable vector artwork. Weather scenes form the complete background, preserving the original card proportions.
- Larger weather-scene background in the expanded view, with hourly and five-day forecast tabs, consistent flat controls, arrow navigation and horizontal mouse-wheel scrolling.
- Current temperature, condition, feels-like, humidity, wind, daily high/low and precipitation probabilities.
- City search with country/region choices; selected city's local timestamps.
- Croatian/English with Windows default and manual selection; Celsius/Fahrenheit and wind units.
- Saved theme, scale, city and position; lock position, optional always-on-top and tray recovery.
- Normal refresh every 15 minutes, bounded retries, network/resume refresh and labelled offline cache. Old current weather is marked stale after an hour and suppressed after six hours. Past cached forecasts are not presented as upcoming.

Default city: Zagreb. Settings, cache and rotating diagnostic log are in `%LOCALAPPDATA%/WeatherWidget`. Deleting that folder while the app is closed resets preferences. No precise geolocation is requested; city coordinates are sent to Open-Meteo for the chosen forecast.

## Desktop compatibility

Default desktop mode keeps the card as a top-level transparent popup owned by the desktop shell, behind ordinary application windows. The earlier wallpaper-child implementation failed visibility testing and has been replaced. Shell-owned Win+D/virtual-desktop behavior still requires compatibility checks on Windows updates. Tray recovery briefly lifts the card into view without changing the always-on-top preference. Always-on-top intentionally uses a different window layer.

The exact Windows validation evidence and any remaining gaps are recorded in `docs/validation.md`. Compilation and fixture tests alone do not establish shell behavior on every Windows build or display setup.

## Develop

Use .NET 10 SDK, or the local SDK in `tools/dotnet` (excluded from Git):

```powershell
.\build.ps1 -Test -Publish
```

For a development apphost, set `DOTNET_ROOT` to the local `tools/dotnet` folder and launch `bin/Debug/net10.0-windows/WeatherWidget.exe`. Launching a DLL through `dotnet.exe` bypasses the application manifest's DPI settings and can prevent desktop attachment. Debug builds have an explicit `--inspect` window for UI testing; Release ignores it and remains tray-only. `--exit` gracefully stops the running instance.

The full feature plan and checklist are in `tasks/`; balanced model routing is in `AGENTS.md`. Build output, local settings and the SDK are excluded from Git. This is a private development repository; see `NOTICE.md` for resource ownership and provider attribution.

## Efficiency and code review

See docs/review-and-performance.md for fixed review findings, measured before/after managed-allocation and layout results, and limits of resident-memory comparisons. Run scripts/BenchmarkRender.ps1 to reproduce the controlled fixture benchmark. Normal minute ticks retain unchanged visual trees; no production forced-GC or working-set trimming is used.

### Lightweight extras (v0.3.0)

Seven selectable themes now include **Nordic** and **Forest**, alongside Modern, Classic, Aurora, Paper and Sunset. All use weather-aware backgrounds.

The compact card can show **Rain soon**; hover it for the hourly estimate. Hover the tray icon for temperature, feels-like and rain/offline status. **Snap to screen edges** is on by default and can be disabled from the right-click menu.

Enable **Weather notifications** in that menu for rain-soon, frost-risk and strong-wind reminders. Notifications default off. **Quiet hours** use your computer's local time (22:00–07:00 by default); alternatives are 23:00–08:00 or Off. Repeated reminders are limited to once per type per six hours for the active location, including across restarts. Windows notification settings may suppress them. These are weather reminders, not official emergency alerts.

These features reuse existing weather downloads and refresh timers. See [feature behavior and checks](docs/lightweight-features.md).
