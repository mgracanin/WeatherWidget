# Current acceptance — v0.1.3

Owner confirmed the corrected layout, compact size, paired backgrounds, detailed-window movement and proportional resizing. Owner confirmed repeated forecast open/close stays running and the tray icon is visible. Owner confirmed Win+D closes the forecast while retaining the compact card, and the card appears on another virtual desktop.

Final current-source checks: Release build has zero warnings/errors; weather-core harness 10/10 and app harness 13/13 pass. Exactly one packaged v0.1.3 process remains running, with complete live hourly/daily data and no current-run unhandled exceptions or icon-load failures. The private repository and portable ZIP contain the implementation and fixes.

Broader compatibility coverage not exercised: Explorer restart, physical monitor removal, the full 100/150/200 mixed-DPI matrix, and a full Windows logon/startup cycle. Settings persistence, coordinate recovery inputs, startup implementation and lifecycle recovery are present; those platform scenarios are not claimed as runtime-tested. Desktop ownership remains a Windows shell compatibility technique, not a guarantee across future Explorer changes.

The records below are chronological, including superseded failures and earlier pending checks. The owner-confirmed target-session feature checks above supersede the earlier pending core acceptance notes.

---
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


## Owner-reported visibility failure and corrective work

The owner reported that the tray and menus worked but the main card was invisible. This contradicts any inference of visual success from the earlier valid child-parent/visible/DPI flags. The earlier WorkerW child-host approach is rejected as a functional implementation.

The corrective implementation retains WPF's layered card as a top-level popup owned by the desktop shell, rather than a wallpaper child. Explicit Show/recover adds a short visibility lift without changing the saved always-on-top preference. Actual visibility, Win+D and virtual-desktop behavior still require rechecking after this change.

A new editable vector sun/cloud app icon and multi-size ICO are included for the executable, tray and forecast window. Assets are under Assets/Brand, and scripts/GenerateIcon.ps1 regenerates the raster/icon files. Existing supplied weather backgrounds are unchanged.

Version 0.1.1: corrected desktop-owned popup launched as the packaged executable and retained saved preferences. Native diagnostics show a top-level WS_POPUP/WS_EX_LAYERED/WS_EX_TOOLWINDOW, owned by the desktop shell, with an on-screen 220x113 rectangle. These remain native-state observations pending owner visibility confirmation. The nine icon frames decoded successfully and integrated WPF checks passed 11/11 after icon wiring. Executable SHA-256: 5CB2E8E64998E5799BE8BE555F89830F99F4765F842FCE4828AB52E12537E963.


Owner confirmation after v0.1.1: the card is visible and clicking it opens the forecast. This verifies the main visibility regression is resolved in the owner's Windows session. Win+D, drag/lock and virtual-desktop behavior remain awaiting direct confirmation.


## v0.1.2 crash and tray-icon corrections

Owner reported a crash after opening the forecast and no custom tray icon. Windows .NET Runtime/Application Error records and shell.log identified an InvalidOperationException from the forecast Deactivated callback calling Close while the same window was already closing. The published Assets/Brand folder contained the PNG but not the ICO; the icon-loader log confirmed fallback due to the missing file.

Fix: forecast dismissal is deferred, RequestClose is guarded throughout closing, and the App clears only the matching forecast reference before explicitly closing it. Escape, close button, toggle, city change and shutdown use the same guarded path. Added a closing-event regression that attempts a secondary close/dismiss and verifies one closing/closed event.

Fix: tray ICO is an embedded assembly resource, cloned independently of its source stream. It no longer depends on a loose ICO being published. The embedded resource decodes all nine Windows frame sizes. The application harness now passes 13/13 groups; Release/self-contained publish passed with zero warnings/errors. Packaged v0.1.2 was launched; owner runtime stress confirmation remains pending.

v0.1.2 executable SHA-256: 492B24A190A1C7B3F7A9B457065A935815D29FF6338FE14E73AA41908DB137AB.


Owner confirmed v0.1.2 stays running after repeated forecast open/close operations (close button and outside-click test) and the custom sun/cloud tray icon is visible. This verifies the reported forecast-close crash and missing tray icon fixes in the actual packaged application.


## v0.1.3 layout and manipulation corrections

Owner screenshot identified oversized default compact view, mismatched compact/expanded backgrounds, a standard white scrollbar among glass controls and a nonmovable expanded panel.

Compact default is now 130x67 for both themes. Cloudy and other supported conditions pair docked_black_* and undocked_black_* artwork; day sun/partly compact scenes use the original supplied small card because matching small blue files are absent. Expanded default is 330 x (330*194/264). No supplied weather artwork was edited.

Shared flat controls replace system button/scrollbar chrome. Hourly navigation uses arrows/wheel and whole columns; five-day entries fit in the panel. Both windows support proportional lower-right resize; compact also supports Ctrl+wheel. Main Scale and DetailScale persist separately. Expanded header drag ignores buttons/grips and suppresses auto-dismiss while moving/resizing; the v0.1.2 close guard is preserved.

The application harness passed 13/13 after adapting actual size/asset/tab and detail-scale assertions. Actual WPF fixture rendering produced four 130x67 compact previews and two 330x243 expanded previews under artifacts/previews. Root inspected rain, sun, long-name/negative-temperature and hourly/daily scenes; moved the footer inside the painted asset edge. These are offscreen fixtures, not live readings or desktop input tests. Packaged v0.1.3 built without warnings/errors and was launched; native compact rectangle is 130x67. Owner movement/resize and visual confirmation remain pending.

v0.1.3 executable SHA-256: AE9B563D1683215D6BF5BBB324F51141B637AE36F30555218B4BDD202FF70FAC.


Owner confirmed v0.1.3: layout is consistent, detailed-window header movement works, and proportional resizing works for both windows. This verifies the owner-reported oversized/inconsistent/immovable layout issues are resolved in the packaged app. Win+D and virtual-desktop checks remain pending.


## v0.2.0 review, efficiency and themes

Release build and self-contained publish passed with zero warnings/errors. Core 10/10 and application 15/15 assertion groups passed on the final source, including closing reentrancy, unchanged-view reuse, theme persistence/repainting and malformed provider recovery. Aurora, Paper and Sunset use frozen weather-aware vector backgrounds in both sizes. Offscreen WPF fixture renders were inspected; these do not establish fresh native desktop interaction coverage.

See review-and-performance.md and performance/ for findings, reproducible controlled allocation benchmarks and the short warm runtime observation. Existing owner acceptance of movement, resizing, Win+D and virtual desktops remains the native interaction evidence; Explorer restart and the broader mixed-DPI/monitor/logon matrix remain unverified.


## v0.2.1 memory and tray follow-up

Core 10/10 and app 16/16 groups pass; build and self-contained publish have zero warnings/errors. First-opening tray cancellation was reproduced in a failing regression before the fix. Process-level software rendering was compared with fresh hardware-default runs; measured private working set dropped substantially on this machine. See review-and-performance.md for raw metrics, startup CPU cost and native-interaction coverage limits. The packaged application was relaunched intentionally for measurements.

## v0.3.0 lightweight bundle

Core 10/10 and application 20/20 groups pass after integrating rain-soon information, richer tray text, edge snapping, optional persisted weather reminders with quiet hours, and Nordic/Forest themes. The seven-theme checks cover frozen/reused artwork and persistence. New tests cover rain threshold/type/freshness/time range, opt-in, quiet boundaries, persisted six-hour cooldown, city changes, bounded history and malformed preferences, negative monitor coordinates/oversized cards and tooltip length/units/offline state.

Actual WPF fixture previews for Nordic and Forest were rendered at 130x67 and 330x243 and inspected. The code review found no additional concrete defect. Build/publish passed without warnings/errors. Native mouse snapping, OS notification delivery and user theme acceptance remain owner checks; no test balloons were sent. Existing desktop integration compatibility limits remain unchanged. See lightweight-features.md for behavior and defaults.
