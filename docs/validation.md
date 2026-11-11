# Validation record — 2026-11-11

## Automated evidence

- Core harness: 10/10 assertion groups passed. Covers forecast parsing, WMO mapping, units, null/string numeric fields, truncated arrays, cancellation, HTTP errors, coordinates, DST/local dates and a captured public live Zagreb response.
- Application harness: 11/11 assertion groups passed. Covers cache matching/corruption, offline preservation, cancelled city changes, malformed/IO recovery, disposal, settings roundtrip/recovery and offscreen WPF asset/layout checks.
- Every WMO mapping for codes 0–99, day/night and compact/expanded image sizes loads. Expanded source images are 264x194; the forecast panel is 528x388. Offscreen checks cover hourly/five-day tab data and translated Fahrenheit/mph/offline labels.
- Integrated Debug and Release builds passed with zero compiler errors. Final portable publish is recorded below when complete.

## Actual runtime evidence

The native apphost was launched in this Windows session (Windows 11 build 26300). Diagnostic log recorded a valid Explorer desktop parent, equal widget/host DPI contexts and desktop compatibility mode. A live forecast was fetched and saved for Zagreb with current conditions, full hourly entries and seven daily entries. Launching the DLL via dotnet.exe had earlier selected fallback mode because it bypassed the application manifest; the corrected launch uses the apphost.

Owner supplied a screenshot of the initial live-data Modern card. The owner correctly rejected its inset illustration. The implementation was changed to use full weather-scene backgrounds in compact and expanded views. Production Release ignores --inspect, has ShowInTaskbar=false, and uses a notification-area icon. The temporary taskbar inspection window was closed after owner feedback.

## Remaining runtime acceptance

Computer-use binding to the development widget timed out awaiting application access. The agent requested access and has not received confirmation. Offscreen WPF tests are not a substitute for visual/input acceptance. Therefore the following remain explicitly unverified:

- Visual approval of corrected full-background Modern and Classic cards and the larger expanded scene.
- Mouse dragging, click versus drag, all context/tray commands and keyboard close in the packaged executable.
- Actual Win+D visibility and collapse, switching virtual desktops, Explorer restart recovery, monitor removal and mixed-DPI movement.
- Idle/resource observations over extended use and full Windows-startup opt-in/disable cycle.

No Explorer process was killed, Windows display scaling changed or user startup preference silently enabled for these checks. Runtime compatibility uses undocumented Explorer hosting as explained in desktop-shell.md; tests must not claim universal shell guarantees.

The goal is not yet marked complete while these acceptance checks remain.

## Portable package smoke check

Self-contained win-x64 publish completed with zero warnings/errors. Ran artifacts/win-x64/WeatherWidget.exe with DOTNET_ROOT deliberately pointing at a nonexistent runtime: the bundled app remained running. A second launch exited and left exactly one packaged process. The packaged app log confirmed desktop-host compatibility mode and a fresh live response with 168 hourly and seven daily entries. Release has no taskbar inspection mode. The package SHA-256 is BA6121731288C19F642D62A0558D9862814E0C070AD37E7E7BAB911B59D2C5C3. This is process/data evidence, not a substitute for the pending visual/input checks.


Final manifest declares Windows 10/11 compatibility (supportedOS GUID documented by Microsoft) in addition to PerMonitorV2 DPI awareness. Final self-contained rebuild passed with zero warnings/errors. Final package executable SHA-256: AB20A8E589312D18C22865DDE68F92751E6EE7398E104206631BA6BF7EA4F340.

