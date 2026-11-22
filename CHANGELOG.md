# Changelog

All notable changes to **Snappy Layouts** are documented in this file.

---

## [v1.11] — 2026-11-22
### Added
- **Active Window Current-Zone Indicator (`SnapFlyoutWindow` & `TopSnapBarWindow`):** When opening Snap Layouts for a window that already occupies an exact layout zone (4-edge alignment + `IoU >= 0.85`), that zone box is clearly marked with a bright accent border, active tint fill, the window's own application icon, and a top-right indicator dot (`●`) so you immediately see which zone the active window is currently in.

---

## [v1.10] — 2026-11-22
### Added
- **Conflict-Free Default Shortcuts for All Unassigned Actions:**
  - Assigned `Ctrl + Alt + V` to **Toggle Virtual Monitors on / off**.
  - Assigned `Ctrl + Alt + Shift + 1`..`4` to the built-in **Virtual Monitor Presets** (and automatically assigns the next free shortcut in `Ctrl + Alt + Shift + 1..9, 0` for newly created presets).
  - Automatically assigns free `Ctrl + Alt + F1`..`F12` shortcuts to saved **Workspaces** (safe for Turkish Q `AltGr` characters as well as US/international layouts).
  - Existing user `settings.json` configurations automatically upgrade on startup (`SchemaVersion = 3`) to fill any unassigned shortcuts, and **Restore Default Shortcuts** refreshes all actions, presets, and workspaces together.

---

## [v1.9] — 2026-11-22
### Added
- **Reorderable Snap Layouts (`▲` / `▼`):** Added Move Up and Move Down controls on every non-permanent layout card in the Snap Layouts tab so you can customize the exact display order across the Snap Flyout and Top Snap Bar. The permanent **Virtual Monitor Fullscreen** item stays pinned at position #1.
- **Multi-Select Screen Type Filter Bar:** Added a multi-select toggle chip bar above the layout list (`Standard`, `Ultra-Wide`, `Portrait`) with a live match counter (`visible / total`) to quickly filter layouts by target screen orientation.
- **Selectable Virtual Monitor Fullscreen Panel:** Clicking the permanent **Virtual Monitor Fullscreen** card now selects it exclusively and displays its dedicated settings & details panel on the right (100% true-aspect preview, target resolution/DPI, shortcut, and direct Virtual Maximize / Shift-bypass toggles).

### Changed
- **Eliminated Double-Selected Border & Added Permanent Badge:** The **Virtual Monitor Fullscreen** card now only shows an active selection border when actually selected, and includes the same `🔒 PERMANENT / INDELETABLE` badge and lock icon as the Full Physical Monitor card.
- **Renamed "Full Physical Screen" → "Full Physical Monitor":** Updated `PermFullTitle`, `FlyoutPhysicalFull`, and related labels across all 8 languages while keeping Fullscreen labels unchanged.
- **Distinct Physical Monitor vs. Virtual Monitor Visual Language:** Applied unmistakable color, shape, and typography differentiation across **Snap Layouts**, **Virtual Monitors**, **Settings & DPI**, and **Snap Flyout** — Physical Monitors use bold indigo/blue hardware cards (`🖥`) while Virtual Monitors use vibrant cyan/teal rounded pills (`◫`).

---

## [v1.8] — 2026-11-22
### Changed
- **Snap Flyout Header Layout Polish:** Moved the green **Full Physical Screen** button to the far left of the Virtual Monitors selector row with a vertical divider when virtual monitors are present (no divider when none exist), updated multi-virtual-monitor span chip labels to use full names (`Virtual 1 + Virtual 2`, `Virtual 2 + Virtual 3`, localized), and added a full-width horizontal divider line below the selector row.

---

## [v1.7] — 2026-11-22
### Fixed
- **Mixed-DPI Screen Edge Overflow Fixed (`SnapFlyoutWindow` & `TopSnapBarWindow`):** Fixed Snap Flyout and Top Snap Bar getting clipped off the right edge of the screen when opening across monitors with different DPI scales (e.g., `125%` ↔ `175%`) by applying two-pass `WM_DPICHANGED`-synchronized positioning using actual `GetWindowRect` physical dimensions and capping flyout width to the host monitor's work area.

---

## [v1.6] — 2026-11-22
### Fixed
- **Strict Exact-Zone App Icons & No Duplicate Layouts:** Removed the duplicated top suggestion row from `SnapFlyoutWindow` so every layout appears once, and tightened `WindowIconProvider.BuildOccupiedZoneIcons` to require exact 4-edge alignment (`<= 24px * scale`), `IoU >= 0.85`, and Z-order occlusion filtering — ensuring an app icon only appears inside a zone that the app genuinely occupies at that exact size and position.

---

## [v1.5] — 2026-11-22
### Added
- **Live App Icons & Windows 11-Style Snap Suggestions in Layouts:**
  - Added `WindowIconProvider` to extract and cache crisp per-window application icons (Win32, Chrome profile badges, Explorer folders, and UWP/WinUI apps) with zero blocking on hung windows.
  - Added a Windows 11-style top **Snap Suggestions** row in `SnapFlyoutWindow` showing icons of your most recently used open apps inside multi-zone layouts (on recessed dark/light icon tiles); clicking a zone arranges both the active window and the suggested companion apps together on the selected physical or virtual monitor.
  - Regular layout tiles in both `SnapFlyoutWindow` and `TopSnapBarWindow` also display icons of apps currently occupying matching zones.

---

## [v1.4] — 2026-11-22
### Fixed
- **Single-Instance Native Snap Suppression Race Fixed:** When a new instance of Snappy Layouts replaces an existing running instance (such as on restart or in-place upgrade), the exiting old instance no longer calls `RestoreWindowsNativeFlyout()` behind the new instance's back and cleanly releases its single-instance mutex.
- **Post-Sleep / Display Change Re-Suppression (`EnsureSuppressed`):** Added automatic verification after display topology changes or waking from sleep so Windows 11's native Snap Flyout stays suppressed whenever `FlyoutEnabled` is active.

---

## [v1.3] — 2026-11-22
### Fixed
- **Instant Hover Flyout on Busy / Heavy Apps (e.g., Adobe Acrobat Large PDFs):** Eliminated Snap Flyout delays and failures when hovering over the Maximize button of heavily loaded or slow-responding applications.
  - `MaximizeHoverWatcher` now pre-filters to the top caption-button band with zero cross-process IPC anywhere else on screen.
  - Added immediate `IsHungAppWindow` detection, shortened `WM_NCHITTEST` timeout (`18ms`), and added automatic geometric fallback (`IsPointInWindowMaxButton` with maximized-frame clamping and RTL support) whenever the target UI thread is busy or custom titlebar chrome returns `HTCLIENT` (while still rejecting `HTCLOSE` / `HTMINBUTTON`).
  - Removed blocking cross-process `HitMaxButton` calls from the WPF UI thread in `SnapFlyoutWindow.CheckAutoClose` and `WindowEventService.TrySuppressNativeSnapWindow`, and added `SWP_ASYNCWINDOWPOS` fallback in `WindowPlacer` for hung windows.

---

## [v1.2] — 2026-11-20
### Fixed
- **Disconnected Monitor Ghost Borders Fixed:** Fixed a bug where unplugging an external monitor (e.g., an ultrawide monitor with 3 virtual monitors) could leave its stale border overlay window hidden in memory and re-appear on the remaining laptop screen when dragging windows. `VirtualBorderOverlayManager` now destroys hidden overlays cleanly, refreshes against live connected monitors on drag start, handles `PowerModeChanged` (sleep/wake) with a two-pass display-topology refresh, and `MonitorService` auto-expires its monitor cache after 1.5 seconds.

---

## [v1.1] — 2026-11-19
### Changed / Improved
- **Standard Program Files Installation:** Updated the Inno Setup installer to install into `C:\Program Files\Snappy Layouts` instead of `%LocalAppData%\Programs`.
- **Antivirus False-Positive Mitigation:** Switched the release package from compressed single-file self-extracting bundles to transparent multi-file self-contained binaries (and disabled solid archive packing in Inno Setup) so antivirus engines can inspect all DLLs cleanly.

---

## [v1.0] — 2026-11-19
### Initial Production Release
- **Windows 11-Style Maximize Hover Flyout & Top Drag Snap Bar:** Hover any window's Maximize button (`Ctrl + Alt + Z`) or drag a window to the top edge of any monitor to snap into customizable zones, virtual monitor fullscreen, multi-virtual-monitor spans (`1+2`, `2+3`, `Shift + Click`), or permanent full physical screen.
- **Virtual-Monitor-Aware Edge & Top-Edge Drag Snap:** Dragging a window to the far left or far right edge snaps to the corresponding 50% half of the edge virtual monitor; dragging to the very top edge previews and snaps to 100% fullscreen of the active virtual monitor.
- **Direct Virtual Maximize (Zero Physical Flash):** Clicking a window's Maximize button transitions directly into Virtual Monitor Fullscreen without flashing across the physical screen first, while automatically suppressing Windows 11's native Snap overlays and restoring them cleanly on pause or exit.
- **Side-by-Side Virtual Monitors & Custom Grid Layouts:** Split ultrawide or multi-monitor setups into arbitrary virtual monitors with saved visual presets, custom friendly monitor names, customizable divider overlays, and independent per-monitor/per-virtual-monitor grid layouts.
- **Workspaces & Apps:** Capture running applications, browser URLs, Explorer folders, and exact window positions; restore them to the current or a new Windows Virtual Desktop even if snap layouts change later.
- **8 Languages, Vector Flags & Collapsible UI:** English (default), Turkish, Spanish, Portuguese, Chinese (Simplified), French, German, and Russian with crisp vector country flags, Per-Monitor V2 DPI synchronization, Light/Dark/System themes, and a zero-shift collapsible sidebar.
- **Windows Installer & Portable Distribution:** Added per-user Inno Setup Windows Installer (`SnappyLayouts-v1.0-Setup.exe`), self-contained Portable ZIP (`SnappyLayouts-v1.0-Portable-win-x64.zip`), and automated GitHub Releases workflow.
