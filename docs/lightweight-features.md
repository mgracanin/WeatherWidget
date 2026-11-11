# Lightweight features — v0.3.0

Owner-approved bundle: rain soon, richer tray tooltip, edge snapping, optional weather notifications and two vector themes. Notifications default off. No new timers, animation loops, weather endpoints or background location tracking.

- Rain soon: existing hourly entries, rain weather codes and precipitation probability at least 60%, within the next three hours including the active hourly period. Wording uses hourly resolution, never minute-level onset. Hidden on offline or stale data.
- Tray hover: selected city, temperature, feels-like, rain or freshness state, bounded to 63 characters for shell compatibility. Uses the existing minute/update events.
- Snapping: compact card snaps within 12 DPI-scaled pixels of current monitor work-area edges on drop. Negative monitor origins supported; taskbar area excluded. Toggle in the right-click menu. Default on.
- Notifications: optional Windows tray balloon reminders for rain soon, current temperature at/below freezing and current wind at least 50 km/h. Fresh online data only, existing successful refreshes only, six-hour cooldown per type and active location, bounded history persisted across restarts. These are forecast reminders, not official severe-weather alerts. Windows notification settings/Do Not Disturb can suppress balloons.
- Quiet hours: computer local time, default 22:00–07:00, selectable 23:00–08:00 or Off. Skipped reminders do not consume the cooldown; next eligible refresh can notify.
- Nordic: pale ice/mountain artwork with navy text. Forest: green woodland artwork with light text. Both use frozen weather/day/night vector scenes at compact and expanded sizes. Seven themes total, unchanged default size and low-memory software rendering.

Verification: 10 core and 20 application assertion groups passed. Covers rain threshold/type/time/freshness, opt-in, quiet boundaries, persisted cooldown, location changes, bounded history, malformed preferences, negative monitor snapping, oversized windows, tooltip bounds/units/offline markers, seven theme round-trips and frozen artwork reuse. Actual WPF fixture renders inspected for Nordic/Forest in both sizes; offscreen fixture evidence does not establish real desktop dragging or delivered OS notifications. No test notifications sent to the owner's desktop.

Packaged v0.3.0 warm sample after 30 seconds: 22.48 MiB private working set, 29.37 MiB private committed, 106.59 MiB total working set; 0.03125 CPU seconds over a 15-second idle window. This is a short observation, not a memory ceiling or long-duration leak assessment. Raw metrics: performance/memory-v030.json.
