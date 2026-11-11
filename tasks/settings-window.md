# Settings window acceptance — v0.4.0

- [x] Six quick context actions; Settings replaces all nested preference menus.
- [x] Four sections, theme preview, city picker, full preference controls and status access.
- [x] Isolated draft with Apply, Save and close and Cancel/Escape/close semantics.
- [x] Atomic persistence, next-load round-trip, failure reporting and startup-registration rollback.
- [x] Preserve live placement/reminder history changed while editing.
- [x] Lazy singleton window released on close; no added timers or network calls.
- [x] 33 test groups, zero build warnings/errors and English/Croatian layout inspection.
- [ ] Owner native Settings, city-picker and keyboard-focus acceptance.

Detailed evidence and limitations: docs/settings-window.md.
