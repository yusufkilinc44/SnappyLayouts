# Changelog

All notable changes to **Snappy Layouts** are documented in this file.  
**Versioning Policy:** Each change increments the version by `0.1` (`v0.1` → `v0.2` → ...), transitioning to `v1.0` for production release.

---

## [v0.25] — 2026-11-19
### Changed / Fixed
- **Renamed to Snappy Layouts:** Renamed the application, solution, project, launcher, UI strings, and documentation to **Snappy Layouts** (with automatic one-time migration of existing `%AppData%\Snappy Layout\settings.json` so no saved layouts or workspaces are lost).
- **Reliable Restore of Windows Native Snap Layouts on Pause & Exit:** Pausing (from sidebar or system tray) or exiting Snappy Layouts now immediately restores `EnableSnapAssistFlyout`, `EnableSnapBar`, `WindowArrangementActive`, and `SPI_SETWINARRANGING` so Windows 11's native Snap Layouts and Aero Snap resume immediately.
- **Fixed Windows 11 Virtual Desktops Switcher & Workspaces Placement:** Narrowed native snap popup suppression (`IsNativeSnapPopup`) so Windows 11's Task View / Virtual Desktops switcher (`Win + Tab` / taskbar Desktops) is never hidden when the mouse moves over it. Ensured Workspaces & Apps always place windows at their exact saved coordinates even if Snap Layouts were modified or deleted later.

---

## [v0.24] — 2026-11-19
### Fixed
- **Clear Top-Edge Fullscreen Selection in Top Snap Bar:** Dragging a window to the very top edge now visually highlights the **Virtual Monitor Fullscreen** tile in accent blue as selected, while keeping the **Full Physical Screen** tile neutral until hovered so it no longer looks pre-selected.

---

## [v0.23] — 2026-11-19
### Added / Fixed
- **Virtual-Monitor-Aware Edge & Top-Edge Drag Snap:** Suppressed Windows's built-in edge/top-drag Aero Snap overlay (`SPI_SETWINARRANGING`, restored automatically on exit or pause) and replaced it with Snappy Layouts's own translucent zone preview and snap behavior:
  - Dragging any window to the **far left or far right edge** of a physical monitor previews and snaps to the corresponding **50% half of the leftmost or rightmost virtual monitor** (instead of 50% of the entire physical screen).
  - Dragging any window to the **very top edge** previews and snaps directly to **100% Virtual Monitor Fullscreen** of whichever virtual monitor the cursor is currently over (while moving slightly down into the Top Snap Bar still lets you choose any layout zone).
- **Fixed Collapsed Sidebar Icon Clipping:** Widened the collapsed sidebar rail (`78px`) and tuned nav button padding (`9px`) so the `32×32` icon badges render completely with zero right-edge clipping at any DPI.

---

## [v0.22] — 2026-11-19
### Changed
- **UK Flag for English:** Updated the English language selector flag icon to the United Kingdom (Union Jack) flag.

---

## [v0.21] — 2026-11-19
### Changed / Added
- **Zero-Shift Sidebar Expand/Collapse:** Unified sidebar padding, fixed icon columns, and footer layout so expanding or collapsing the left sidebar only reveals or hides text labels and dropdowns while keeping every icon's `(X, Y)` coordinates pixel-locked.
- **Full Feature Parity in Collapsed Mode:** Added clickable active **language flag** and **theme icon** buttons (each opening a quick selection menu) alongside the border highlight and pause/resume controls in collapsed mode so every expanded sidebar feature is available when collapsed.

---

## [v0.20] — 2026-11-19
### Fixed
- **Accurate Vector Country Flags:** Redrew all 8 country flags on an exact `22×15` vector Canvas: United States flag for English, authentic crescent + 5-pointed star for Turkey, Coat of Arms (*Escudo de España*) on Spain's yellow band, armillary sphere & shield for Portugal, 5-star arc for China, and tricolors for France, Germany, and Russia.

---

## [v0.19] — 2026-11-19
### Changed
- Kept sidebar navigation icon sizes (`32×32`) and menu row heights (`46px`) identical between expanded and collapsed modes so icons never shift vertically when toggling or resizing.

---

## [v0.18] — 2026-11-19
### Fixed
- Fixed the version badge in the sidebar header getting clipped next to the sidebar collapse button (`☰`) by placing it neatly next to the `Active` status indicator.

---

## [v0.17] — 2026-11-19
### Added / Changed / Fixed
- **8 Languages & Vector Country Flags:** Added Spanish, Portuguese, Chinese (Simplified), French, German, and Russian alongside English (new default) and Turkish, complete with crisp vector flag icons in both sidebar and Settings dropdowns.
- **Standard Aspect Ratio Formatting:** Monitor and virtual monitor aspect ratios are now displayed in familiar standard formats (`16:9`, `16:10`, `21:9`, `32:9`, `4:3`, `3:2`, etc.) instead of decimals like `1.58:1`.
- **Softer Preview Overlays & Z-Order / Width Fix:** Made blue and green snap preview overlays even more transparent and pastel, and fixed an issue where `SnapZonePreviewOverlay` clipped or failed to cover windows like Notepad on ultrawide monitors.
- **Collapsible Colorful Sidebar & Enriched Palette:** Made the left navigation sidebar automatically collapse into a crisp icon rail on smaller window widths (with manual toggle), added per-section vibrant accent colors across tabs, headers, and primary actions, and kept author/year credits exclusively in the Help & About section.

---

## [v0.16] — 2026-11-19
### Added
- **Multi-Virtual-Monitor Spanning (`1+2`, `2+3`, `Shift+Click`):** On monitors split into 3 or more virtual monitors, added one-click adjacent span chips (`◫ 1+2`, `◫ 2+3`, etc.) and `Shift + Click` range spanning in `SnapFlyoutWindow`, plus adjacent span drop tiles in `TopSnapBarWindow`, allowing a window to maximize across multiple side-by-side virtual monitors with live preview.
- **Direct Virtual Maximize (Zero Physical Flash):** Added `MaximizeClickInterceptor` (`WH_MOUSE_LL`) to intercept Maximize button clicks before Windows physically maximizes the window, transitioning directly to Virtual Monitor Fullscreen without flashing across the full physical screen first.

---

## [v0.15] — 2026-11-19
### Changed
- **Subtler Preview Overlays:** Increased transparency of the blue (virtual monitor / zone) and green (physical monitor) live preview boxes (`SnapZonePreviewOverlay`) for a lighter, less intrusive visual highlight.

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
- **Top-of-Screen Drag Snap Bar (`TopSnapBarWindow`):** Dragging any window to the top edge of any physical or virtual monitor suppresses Windows 11's native Snap Bar and displays Snappy Layouts's custom horizontal Snap Bar with live zone previews and drop-to-snap support.
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
- Enabled Snappy Layouts's custom Snap Flyout and virtual monitor maximize/zone snapping on Snappy Layouts's own `MainWindow` maximize button.

---

## [v0.7] — 2026-11-18
- Fixed Language and Theme dropdown menus (`ComboBox`) in the sidebar and Settings tab not opening on click after Fluent theme dictionary changes.

---

## [v0.6] — 2026-11-17
- Organized project workspace directory structure under `Snappy Layouts`.

---

## [v0.5] — 2026-11-17
- Standardized application name, folder structure, project files, and source code namespaces to `Snappy Layouts`.

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
