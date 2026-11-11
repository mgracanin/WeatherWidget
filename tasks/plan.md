# WeatherWidget plan

Status: owner approved, 2026-11-11. Implementation active.
Project: D:\Development\WeatherWidget; private GitHub repository mgracanin/WeatherWidget.
Tasks: tasks/todo.md. Development/model policy: AGENTS.md.

## Agreed direction

- Standalone Windows 11 application, independent of XWidget.
- Both selectable themes: Classic WinPrognoza and Modern Windows 11.
- Compact current-weather card; click to expand hourly and five-day forecasts.
- Easily movable like an XWidget widget.
- Balanced model use, with lower-cost models for suitable work.

## Proposed version 1 experience

### Compact card

Show city, temperature, weather illustration and a short condition label. Keep provider attribution and last weather timestamp accessible in the detail view/tooltip. Classic preserves the original card proportions; Modern overlays readable typography on the supplied weather scene as the full card background; no inset scene thumbnail. Both show identical data and controls. Include adjustable scale without stretching or clipping artwork. Default city Zagreb and Celsius. Modern is the default at 220 pixels wide, with height derived from the original 130:67 background aspect ratio (about 113 device-independent pixels); Classic preserves the original proportions. Support Croatian and English, following Windows initially with a manual override.

### Expanded view

Click the card (without dragging) to toggle a panel anchored beside it. Use the larger 264 x 194 weather background for the entire expanded panel (528 x 388 at default size). Overlay feels-like temperature, humidity, wind and today's high/low. Tabs switch between the next 12 hourly forecast entries (temperature and rain probability) and five daily entries (condition, high/low, rain probability). Display the selected city's local time, not the PC's time zone. Position the panel within the current monitor's work area; Escape and an explicit close control collapse it.

### Desktop behavior

Borderless, draggable card; remember its position and chosen monitor. Optional lock-position setting and always-on-top toggle. Default intent is to sit behind ordinary application windows, with no regular taskbar button and a tray icon to show the widget, open settings or exit. Tray recovery must work even when the card is hidden. Single running instance. Preserve settings across restart; recover a visible position if a monitor is removed. Keep text and hit targets usable at Windows display scaling levels.

Validate exact desktop integration early: Win+D, Explorer restart, virtual desktops, mixed-DPI monitors and dragging must be tested in Windows. Owner approved: the compact widget remains visible with Win+D, the expanded panel collapses on Show Desktop, and the compact widget appears on every virtual desktop at the same position. Do not silently substitute an ordinary floating window and call it equivalent to XWidget. Avoid relying on undocumented shell parenting without a specific compatibility review.

### Settings and commands

Right-click/tray menu: Refresh, Change city, Theme, Scale, Lock position, Always on top, Start with Windows, About/status and Exit. City search shows country/region to disambiguate names. Settings expose Celsius/Fahrenheit and wind units. Startup is opt-in, per user and reversible; no administrator privileges. Store settings/cache under the user's local application-data folder, with atomic writes and recovery from corrupt files.

### Weather and failure behavior

Open-Meteo current/hourly/daily data and geocoding over HTTPS; no API key for the planned noncommercial use. Refresh normally every 15 minutes; deduplicate in-flight refreshes and cancel obsolete city searches. Refresh after resume or network recovery with bounded retry/backoff. Separate the weather's timestamp from the fetch timestamp. Cache the last successful response with its city and time zone. If offline, label cached data as old/offline and show its age; never imply it was refreshed. Beyond a defined freshness threshold, suppress current temperature and keep the last timestamp accessible. Initial proposal: after one hour, mark stale; after six hours, replace current temperature with --. This threshold is a proposed product default, not a guarantee about forecast validity. Unknown conditions use a neutral fallback image. Errors must be accessible in Status, without repeated modal popups.

## Architecture and packaging

Use C# / WPF on .NET 10, asynchronous HttpClient and standard JSON serialization. Keep the provider adapter and condition-to-artwork mapping separate from UI/state so a future provider change is localized. Use packaged local images with no dependence on the old portable directory. Use ordinary .NET unit tests for mapping, parsing, time-zone logic and persisted settings; Windows runtime checks verify shell/UI behavior. Build a portable Windows x64 release first; prefer a self-contained published package to avoid requiring the owner's build tools. Installer, signing and automatic updates are later decisions. Keep downloaded SDK and publish outputs untracked.

## Implementation order

1. Desktop shell prototype and behavior proof, using a placeholder card.
2. End-to-end live current weather for Zagreb, with Classic card and error states.
3. City search and persistent preferences.
4. Expand/collapse hourly and five-day forecast panel.
5. Modern theme and scaling, with theme parity.
6. Tray, startup opt-in and desktop lifecycle polish.
7. Package and perform owner acceptance checks.

Each step has acceptance criteria in todo.md. Prototype desktop behavior before investing in complete visual polish. Present Classic and Modern previews before final styling.

## Later candidates (not version 1 commitments)

Multiple saved cities or multiple widgets; animated artwork; sunrise/sunset and UV; air quality; precipitation radar; severe-weather alerts using a suitable source; automatic updates. Weather alerts and notifications need a separate product/source decision.

## Approved defaults

Modern default, 220 x 110; Classic available. Scale control. Win+D retains compact card and collapses detail panel. All virtual desktops. Croatian and English, Windows default with override. Offline cache immediately labelled, stale after one hour, current temperature hidden after six hours. Drag anywhere on the card; distinguish click from drag; optional position lock.

## References

- https://open-meteo.com/en/docs
- https://open-meteo.com/en/docs/geocoding-api
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100

Owner correction during implementation: full-scene backgrounds in compact and expanded views; preserve their proportions. Production application must be tray-only with no taskbar entry. Development inspection mode is excluded from Release.
