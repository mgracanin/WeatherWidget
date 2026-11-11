# WeatherWidget implementation tasks

Owner approved the plan and defaults. Task 1 is in progress.
Use AGENTS.md for model routing. Scope estimates count production files; keep each slice small.

## 1. Desktop shell prototype

- [ ] Implement a draggable borderless placeholder card with one-instance enforcement and saved visible position.
- [ ] Demonstrate and document Win+D, normal app layering, Explorer restart, virtual desktops and monitor removal; agree the resulting desktop behavior with the owner.
- [ ] Verify in the actual Windows UI at 100%, 150% and 200% scaling. A simulated test is insufficient.

Dependencies: none. Likely files: project, App, widget window, window-placement helper (4-5). Scope: medium. Model: workhorse for shell behavior; escalate only for a concrete unresolved interop problem.

## 2. Live current weather with Classic theme

- [ ] Display Zagreb weather using bundled original card images, preserving proportions and readable text.
- [ ] Handle timeout, invalid/missing fields and unknown codes; show honest loading/offline/stale states and timestamps.
- [ ] Verify parsing/mapping/freshness with fixtures and one live request, then inspect the actual rendered card.

Dependencies: 1. Likely files: weather client, data model, condition mapper, card view/model, focused tests (5). Scope: medium. Model: standard integration with lower-cost mapping/fixture work.

## Checkpoint A

- [ ] Build and focused tests pass; desktop prototype and live card inspected by owner before further polish.

## 3. City search and preferences

- [ ] Search and choose disambiguated cities; cancellation or failed search preserves the existing location.
- [ ] Persist city, units and window preferences atomically; recover from invalid settings and discard obsolete search responses.
- [ ] Test ambiguous names, rapid searches, cancel, restart and corrupt settings; inspect the city picker in Windows.

Dependencies: 2. Likely files: search client, picker, settings model/store, tests (5). Scope: medium. Model: workhorse for concurrency/persistence; lower-cost isolated UI work.

## 4. Forecast expansion

- [ ] Clicking opens/collapses a panel; dragging does not open it. Escape and close control collapse it.
- [ ] Show next 12 hours, five daily entries and current details in the chosen city's local time.
- [ ] Test midnight/time-zone transitions, truncated data and edge-of-monitor positioning; inspect keyboard and mouse use.

Dependencies: 3. Likely files: forecast models/client extension, panel, view model, positioning, tests (5). Scope: medium. Model: workhorse integration, lower-cost row templates and fixtures.

## 5. Modern theme and scale

- [ ] Switch Classic/Modern without losing city, data or position; persist the choice and scale.
- [ ] Both themes expose equivalent features and preserve image aspect ratio and readable contrast.
- [ ] Inspect both themes at the agreed scales, with long city names, negative temperatures and keyboard access; obtain owner visual review.

Dependencies: 4. Likely files: two theme dictionaries, settings/view changes, UI fixtures (4-5). Scope: medium. Model: lower-cost well-scoped XAML; workhorse only for integration issues.

## Checkpoint B

- [ ] Core flow and theme previews pass owner review; no repeated full-suite runs unless changes justify them.

## 6. Tray and lifecycle

- [ ] Tray can restore a hidden/locked/off-screen widget and offers Refresh, Settings and Exit; closing the app stops timers/requests.
- [ ] Startup is off initially; opt-in persists and can be reversed without administrator rights.
- [ ] Verify sleep/resume, network recovery, instance relaunch, monitor changes and startup enable/disable in Windows.

Dependencies: 5. Likely files: tray service, startup service, App lifecycle, settings, tests (5). Scope: medium. Model: workhorse Windows integration.

## 7. Portable release and acceptance

- [ ] Produce an independent Windows x64 package with artwork, attribution and launch instructions; no dependency on the old widget folders or local SDK.
- [ ] Run focused suite/build and packaged-executable smoke test; inspect disk/network/idle behavior and record measured results.
- [ ] Owner verifies city/units/themes, move/restart, forecast and offline status; document remaining limitations accurately.

Dependencies: 6. Likely files: publish configuration, README, attribution, validation report (4). Scope: medium. Model: lower-cost packaging/docs, standard model only if a build/runtime failure warrants it.
