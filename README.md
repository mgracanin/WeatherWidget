# WeatherWidget

Standalone Windows 11 weather widget. Native C# / WPF on .NET 10, with no XWidget dependency or AI-model calls.

## Run

Open `artifacts/win-x64/WeatherWidget.exe` after building a portable release. Keep its `Assets` folder beside the executable. The self-contained x64 package includes its runtime; no SDK, XWidget, browser or API key is needed on the target PC.

The widget runs in the notification area (system tray), without a taskbar button. Drag the compact card to move it. Click without dragging to expand forecasts. Escape, the close button or clicking outside collapses the expanded view. Right-click the card or tray icon for settings. Double-click the tray icon or launch the app again to recover the existing widget. Exit through the menu. Startup with Windows is off by default and can be enabled/disabled in the menu.

## Features

- Modern and Classic compact themes. Weather scenes form the complete background, preserving the original card proportions.
- Larger weather-scene background in the expanded view, with hourly and five-day forecast tabs.
- Current temperature, condition, feels-like, humidity, wind, daily high/low and precipitation probabilities.
- City search with country/region choices; selected city's local timestamps.
- Croatian/English with Windows default and manual selection; Celsius/Fahrenheit and wind units.
- Saved theme, scale, city and position; lock position, optional always-on-top and tray recovery.
- Normal refresh every 15 minutes, bounded retries, network/resume refresh and labelled offline cache. Old current weather is marked stale after an hour and suppressed after six hours. Past cached forecasts are not presented as upcoming.

Default city: Zagreb. Settings, cache and rotating diagnostic log are in `%LOCALAPPDATA%/WeatherWidget`. Deleting that folder while the app is closed resets preferences. No precise geolocation is requested; city coordinates are sent to Open-Meteo for the chosen forecast.

## Desktop compatibility

Default desktop mode attaches the card behind applications through Explorer's wallpaper host, so it can behave like a desktop widget. Explorer's WorkerW layout is undocumented and may change after Windows updates. The app validates the host/DPI context and reports a fallback in Status if hosting fails; tray recovery is always available. Always-on-top intentionally uses a different window layer.

The exact Windows validation evidence and any remaining gaps are recorded in `docs/validation.md`. Compilation and fixture tests alone do not establish shell behavior on every Windows build or display setup.

## Develop

Use .NET 10 SDK, or the local SDK in `tools/dotnet` (excluded from Git):

```powershell
.\build.ps1 -Test -Publish
```

For a development apphost, set `DOTNET_ROOT` to the local `tools/dotnet` folder and launch `bin/Debug/net10.0-windows/WeatherWidget.exe`. Launching a DLL through `dotnet.exe` bypasses the application manifest's DPI settings and can prevent desktop attachment. Debug builds have an explicit `--inspect` window for UI testing; Release ignores it and remains tray-only. `--exit` gracefully stops the running instance.

The full feature plan and checklist are in `tasks/`; balanced model routing is in `AGENTS.md`. Build output, local settings and the SDK are excluded from Git. This is a private development repository; see `NOTICE.md` for resource ownership and provider attribution.
