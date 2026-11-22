# Changelog

All notable changes to **Snappy Layouts** are documented in this file.

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
