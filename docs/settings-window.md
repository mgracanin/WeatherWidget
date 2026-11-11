# Settings window — v0.4.0

Implemented: lazy single-instance Settings window with four scrollable sections, selected-theme preview, current custom scales retained, full editable quiet hours, owned city picker, status access and explicit Apply/Save and close/Cancel behavior. Context menu has six quick actions and no nested preference menus. ShowInTaskbar remains false.

Persistence: editor uses an isolated draft. Commit merges editable values into a fresh copy of current live settings, preserving current placement and reminder history; it updates Windows startup registration if changed, atomically writes settings and only then changes live preferences. A failed write leaves the live preferences unchanged and attempts to restore the prior startup registration. Save failures are displayed and keep the editor open. Saved preferences are loaded by the existing startup path without a migration or reset.

Validation: core 10/10 and application 23/23 assertion groups passed; build zero warnings/errors. Covers full preference save/reload, draft isolation, latest placement/history retention, startup invocation, write/startup failure, rollback, first tray opening with current lock state, no nested menus, four settings sections, custom-scale retention, selected-theme label, Apply, Cancel and failed Save remaining open. No test changes actual Windows startup registration or the owner's settings.

Visual inspection: actual WPF control renders in English/Croatian at normal and minimum sizes; fixed blank selected-theme label and kept the theme preview uncropped. Scroll areas keep small-window fields accessible while actions remain visible. This is offscreen component evidence, not an automated native desktop acceptance test. Native context-menu opening, city picker workflow and keyboard focus remain owner acceptance checks.

Packaged v0.4.0 idle observation with Settings closed: 22.00 MiB private working set, 28.50 MiB private committed, 104.45 MiB total working set and 0.015625 CPU seconds over 15 seconds after warmup. Raw data: performance/memory-v040.json. These are short observations, not guaranteed memory ceilings.
