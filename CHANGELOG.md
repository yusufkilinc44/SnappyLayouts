# Changelog

All notable changes to **Snappy Layout** are documented in this file.  
**Versioning Policy:** Each change increments the version by `0.1` (`v0.1` → `v0.2` → ...), transitioning to `v1.0` for production release.

---

## [v0.14] — 2026-11-19
### Fixed / Changed
- **Zero-Flicker Snap Flyout:** Fixed rapid flickering and layout/button shifting when hovering near the right edge of `Full Physical Screen` or other buttons in `SnapFlyoutWindow` by locking the flyout width during rebuilds (preventing `SizeToContent` width oscillation on status text updates) and keeping `SnapZonePreviewOverlay` strictly behind the Flyout/Snap Bar in Z-order.
- **Cleaner Flyout Label:** Removed `"(Indeletable)"` from the Snap Flyout's `Full Physical Screen` button so it only appears inside the main settings application.

---

## [v0.13] — 2026-11-19
### Added
- **Custom Physical Monitor Names:** Users can now assign custom friendly names to any physical monitor (from the Snap Layouts, Virtual Monitors, or Settings & DPI tabs) or reset back to the hardware name. Custom names persist in `settings.json` and appear everywhere across the UI, including the Maximize Snap Flyout.

---

## [v0.12] — 2026-11-19
### Fixed
- Fixed live preview overlay (`SnapZonePreviewOverlay`) overflowing onto adjacent monitors or shrinking when switching between physical monitors with different DPI scales (e.g. `125%` ↔ `175%`) due to `WM_DPICHANGED` rescaling, by implementing two-pass DPI-synchronized positioning.

---

## [v0.11] — 2026-11-19
### Added
- **Top-of-Screen Drag Snap Bar (`TopSnapBarWindow`):** Dragging any window to the top edge of any physical or virtual monitor suppresses Windows 11's native Snap Bar and displays Snappy Layout's custom horizontal Snap Bar with live zone previews and drop-to-snap support.
### Changed
- **Native Windows 11 Visual Design:** Redesigned both `SnapFlyoutWindow` and `TopSnapBarWindow` to closely match Windows 11's native Snap Layouts aesthetic (neutral `#2C2C2C` / `#F3F3F3` surface, frameless rounded zone tiles, Windows Accent Blue `#0078D4` hover highlight).

---

## [v0.10] — 2026-11-19
### Added
- Added **Physical Monitor selector pills** with live screen preview to the Maximize Snap Flyout (`SnapFlyoutWindow`), allowing users to snap any window directly onto any connected physical monitor and its virtual monitors/layouts.

---

## [v0.9] — 2026-11-18
### Added / Changed
- Completely prevented Windows 11 native Snap Layouts flyout from appearing on any window including `MainWindow` (`ImmersiveShell` `ISettingsCache` COM refresh, `WndProc` `HTMAXBUTTON` intercept, and instant `EVENT_OBJECT_SHOW` hook).
- Snap Flyout now always uses the **Windows system theme** regardless of the application theme setting.
- Positioned Snap Flyout directly below the Maximize button so it never covers the button.
- Moved the **Virtual Monitor Full Screen (`100%`)** card to the beginning of the layout cards list.
- Added `▲` / `▼` spinner buttons and **`25%`, `33%`, `50%`, `66%`, `75%`, `100%`** quick ratio chips to percentage inputs.
- Added true-aspect-ratio visual thumbnail diagrams to Saved Virtual Monitor Presets.

---

## [v0.8] — 2026-11-18
- Enabled Snappy Layout's custom Snap Flyout and virtual monitor maximize/zone snapping on Snappy Layout's own `MainWindow` maximize button.

---

## [v0.7] — 2026-11-18
- Fixed Language and Theme dropdown menus (`ComboBox`) in the sidebar and Settings tab not opening on click after Fluent theme dictionary changes.

---

## [v0.6] — 2026-11-17
- Organized project workspace directory structure under `Snappy Layouts`.

---

## [v0.5] — 2026-11-17
- Standardized application name, folder structure, project files, and source code namespaces to `Snappy Layout`.

---

## [v0.4] — 2026-11-16
- Refactored project and file names across the solution.

---

## [v0.3] — 2026-11-16
- Completely separated Virtual Monitor presets from Window Snap Layouts (independent per-physical and per-virtual-monitor layout lists with 2-step selector).
- Added initial suppression for Windows 11's native maximize Snap Flyout.

---

## [v0.2] — 2026-11-16
- Improved Light theme contrast and visibility across buttons, sidebar items, dropdowns, and text labels.

---

## [v0.1] — 2026-11-16
- Initial release: customizable snap layouts, side-by-side virtual monitors, workspaces with browser URL capture, global hotkeys, English/Turkish localization, Light/Dark/System themes, and Per-Monitor V2 DPI support.
