# Snappy Layouts — Technical Architecture

**Author:** **YUSUF KILINÇ — November 2026**

This document describes the low-level Win32, DWM, Shcore, COM, and WPF architecture powering **Snappy Layouts**.

---

## 1. Windows 11 Native Snap Flyout & Snap Bar Suppression (`NativeSnapSuppressor`)

On Windows 11, hovering over a window's Maximize button (`HTMAXBUTTON`) or dragging a window to the top edge of the screen causes `explorer.exe` (`twinui.dll`) to display `XamlExplorerHostIslandWindow`. Simply writing `0` to `EnableSnapAssistFlyout` and `EnableSnapBar` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` is insufficient on its own because `explorer.exe` caches these values in memory inside `CSettingsCache` on startup.

Snappy Layouts prevents native Snap overlays from flashing or colliding via a **4-layer strategy**:

1. **ImmersiveShell `ISettingsCache` COM Refresh:**
   - Instantiates `CLSID_ImmersiveShell` (`{C2F03A33-21F5-47FA-B4BB-156362A2F239}`) via `CoCreateInstance`, queries `IServiceProvider`, and requests `SID_SettingsCache` (`{86FB57A6-3C81-4F40-A971-6280BE4E526E}` / `{E78295EF-4439-494F-9D40-16E91A4CBF03}`) for `IID_ISettingsCache` (`{B1BED1AE-63DA-4687-8063-C89EC276D28A}`).
   - Invokes vtable slot 3 (`RefreshSetting(int settingId)`) with `11` (`EnableSnapAssistFlyout`) and `12` (`EnableSnapBar`), immediately refreshing Explorer's in-memory cache without restarting `explorer.exe`.
2. **`SettingsCacheChangeMessage` Broadcast:**
   - Broadcasts `RegisterWindowMessage("SettingsCacheChangeMessage")` and `WM_SETTINGCHANGE` via `SendNotifyMessage(HWND_BROADCAST, ...)`.
3. **`MainWindowWndProc` `HTMAXBUTTON` Interception:**
   - On Snappy Layouts's own `MainWindow`, `MainWindowWndProc` intercepts `WM_NCHITTEST`. When `DefWindowProc` returns `HTMAXBUTTON` while no mouse button is pressed, it notifies `MaximizeHoverWatcher` directly and returns `HTBORDER` to Windows so DWM never starts its native Snap Flyout timer on `MainWindow`. When the user clicks (`VK_LBUTTON`), `HTMAXBUTTON` passes through normally.
4. **Zero-Latency `EVENT_OBJECT_SHOW` Hook:**
   - `WindowEventService` registers `SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW, ...)`. Whenever the user is hovering a Maximize button or actively dragging a window (`IsDraggingWindow`), any `XamlExplorerHostIslandWindow` or `Shell_LightDismissOverlay` is immediately hidden and moved off-screen (`SWP_HIDEWINDOW | SWP_ASYNCWINDOWPOS`) before DWM composites a frame.

When Snappy Layouts exits cleanly, `RestoreWindowsNativeFlyout()` restores the original registry values and refreshes `ISettingsCache`.

---

## 2. Per-Monitor V2 Dynamic DPI & Two-Pass Placement (`WindowPlacer` & `SnapZonePreviewOverlay`)

Windows 10/11 windows include invisible drop-shadow borders (~7–8px) in `GetWindowRect`. The visible frame is queried via `DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, ...)`.

When moving a window or preview overlay between monitors with different DPI scales (e.g., `125%` ↔ `175%`), Windows delivers `WM_DPICHANGED`, causing the window (or WPF's DPI handler) to rescale its dimensions. Both `WindowPlacer.Place()` and `SnapZonePreviewOverlay.ShowRect()` solve this with a **two-pass algorithm**:

1. **Pass 1:** Move the window to the target monitor rectangle using `SetWindowPos`, triggering `WM_DPICHANGED` synchronously.
2. **Pass 2:** Re-measure `DWMWA_EXTENDED_FRAME_BOUNDS` (or synchronize WPF's logical `Width`/`Height` to the target monitor's DPI scale) and issue a second `SetWindowPos` so the final physical pixel bounds match the target zone with 0px drift.

---

## 3. Top-of-Screen Drag Snap Bar (`TopSnapBarWindow`)

During an active window drag (`EVENT_SYSTEM_MOVESIZESTART` → `EVENT_SYSTEM_MOVESIZEEND`), mouse capture belongs to the dragged window, so WPF mouse events do not fire on overlay windows.

- `TopSnapBarWindow` uses `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TRANSPARENT` so it never steals focus or interferes with dragging.
- A `20ms` `DispatcherTimer` polls `GetCursorPos` and `GetAsyncKeyState(VK_LBUTTON)`, dynamically switches between virtual and physical monitors as the user drags along the top edge, and drives external hit-testing on each `LayoutPreviewControl` via `SetExternalHoverPoint`.
- On mouse release (`MOVESIZEEND` or `!VK_LBUTTON`), the dragged window is snapped into the highlighted zone.

---

## 4. Virtual Monitor Segmentation (`MonitorService` & `AreaService`)

- Physical displays are identified with hardware-friendly names via `QueryDisplayConfig` (CCD API) in `DisplayConfig.cs`.
- Each physical monitor can be split into `N` side-by-side `SnapArea` segments (`MonitorKey::vm:SegmentCount:SegmentIndex`) with arbitrary weight ratios.
- Each `SnapArea` stores its own isolated snap layout collection in `Storage.Data.AreaLayouts` and computes its own live aspect ratio (`Aspect = Width / Height`).

---

## 5. Workspace & Browser URL Capture (`WindowCatalog` & `WorkspaceService`)

- Process executable paths are resolved via `QueryFullProcessImageName`, and command lines are inspected via `NtQueryInformationProcess` (`PEB -> ProcessParameters -> CommandLine`).
- Active browser URLs (Chrome, Edge, Brave, Vivaldi, Opera, Firefox) and Explorer folder paths are captured and re-launched on demand in either the current desktop or a newly created Windows Virtual Desktop (`Win + Ctrl + D`).
