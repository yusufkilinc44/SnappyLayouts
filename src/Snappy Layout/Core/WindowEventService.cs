using System;
using System.Collections.Generic;
using System.Windows.Threading;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>
    /// Sistem pencere olaylarını izler:
    ///  • Bir pencere maksimize edildiğinde (maksimize tuşu, başlık çubuğuna çift tık, Win+↑ ...)
    ///    ve o monitörde sanal monitörler varsa, pencereyi o sanal monitörün sınırlarında tam ekran yapar.
    ///  • Pencere sürükleme başlangıç/bitişini bildirir (sanal monitör sınırlarını göstermek için).
    /// </summary>
    public class WindowEventService : IDisposable
    {
        private IntPtr _hookLocation, _hookMoveSize, _hookForeground, _hookObjectShow;
        private readonly WinEventDelegate _proc;
        private readonly HashSet<IntPtr> _zoomed = new HashSet<IntPtr>();
        private readonly Dispatcher _dispatcher;

        public static bool IsDraggingWindow { get; private set; }
        public event Action<bool> MoveSizeChanged;   // true = başladı
        public event Action<bool, IntPtr> DragStateChanged;
        public event Action<IntPtr> ForegroundChanged;

        public WindowEventService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _proc = OnWinEvent;
            _hookLocation = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
            _hookMoveSize = SetWinEventHook(EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
            _hookForeground = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            _hookObjectShow = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }

        private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            try
            {
                switch (evt)
                {
                    case EVENT_SYSTEM_MOVESIZESTART:
                        IsDraggingWindow = true;
                        MoveSizeChanged?.Invoke(true);
                        DragStateChanged?.Invoke(true, hwnd);
                        return;
                    case EVENT_SYSTEM_MOVESIZEEND:
                        IsDraggingWindow = false;
                        MoveSizeChanged?.Invoke(false);
                        DragStateChanged?.Invoke(false, hwnd);
                        return;
                    case EVENT_SYSTEM_FOREGROUND: ForegroundChanged?.Invoke(hwnd); return;
                    case EVENT_OBJECT_CREATE:
                    case EVENT_OBJECT_SHOW:
                        if (idObject == 0 && idChild == 0 && hwnd != IntPtr.Zero)
                            TrySuppressNativeSnapWindow(hwnd);
                        return;
                    case EVENT_OBJECT_LOCATIONCHANGE:
                        if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero) return;
                        if (TrySuppressNativeSnapWindow(hwnd)) return;
                        HandleLocationChange(hwnd);
                        return;
                }
            }
            catch (Exception ex) { Logger.Log("WinEvent: " + ex.Message); }
        }

        private static bool TrySuppressNativeSnapWindow(IntPtr hwnd)
        {
            if (Storage.Data?.Settings?.FlyoutEnabled != true) return false;
            if (App.Current != null && ((App)App.Current).Paused) return false;

            string cls = SnappyLayout.Native.NativeMethods.GetClassName(hwnd);
            if (!NativeSnapSuppressor.IsNativeSnapWindowClass(cls)) return false;

            bool flyoutVisible = App.Instance?.Flyout?.IsVisible ?? false;
            bool topBarVisible = App.Instance?.TopSnapBar?.IsVisible ?? false;
            if (flyoutVisible || topBarVisible || IsDraggingWindow || MaximizeHoverWatcher.IsRecentlyHoveringMaxButton())
            {
                NativeSnapSuppressor.SuppressWindowInstant(hwnd);
                return true;
            }
            if (GetCursorPos(out var pt) && MaximizeHoverWatcher.HitMaxButton(pt) != IntPtr.Zero)
            {
                NativeSnapSuppressor.SuppressWindowInstant(hwnd);
                return true;
            }
            return false;
        }

        private void HandleLocationChange(IntPtr hwnd)
        {
            bool zoomed = IsZoomed(hwnd);
            if (!zoomed)
            {
                if (_zoomed.Remove(hwnd)) RemoveProp(hwnd, WindowPlacer.PropPhysicalMax);
                return;
            }
            if (!_zoomed.Add(hwnd)) return; // zaten maksimizeydi
            if (_zoomed.Count > 500) _zoomed.Clear();

            var s = Storage.Data.Settings;
            if (!s.VirtualMonitorsEnabled || !s.VirtualMaximize || App.Current == null || ((App)App.Current).Paused) return;
            if (GetProp(hwnd, WindowPlacer.PropPhysicalMax) != IntPtr.Zero) return;
            if (s.ShiftBypassesVirtualMaximize && IsKeyDown(VK_SHIFT)) { SetProp(hwnd, WindowPlacer.PropPhysicalMax, new IntPtr(1)); return; }
            if (GetAncestor(hwnd, GA_ROOT) != hwnd) return;
            if ((GetStyle(hwnd) & WS_MAXIMIZEBOX) == 0) return;
            if (!WindowPlacer.IsValidTarget(hwnd)) return;

            var mon = MonitorService.FromWindow(hwnd);
            if (AreaService.GetWeights(mon) == null) return;

            // Hangi sanal monitör? Pencerenin maksimize öncesi (normal) konumunun merkezine bakılır.
            var area = AreaService.AreaForWindow(hwnd);
            if (area == null || !area.IsVirtual) return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsWindow(hwnd) || !IsZoomed(hwnd)) return;
                WindowPlacer.VirtualMaximize(hwnd, area, allowToggle: true);
                _zoomed.Remove(hwnd);
            }), DispatcherPriority.Send);
        }

        public void Dispose()
        {
            if (_hookLocation != IntPtr.Zero) UnhookWinEvent(_hookLocation);
            if (_hookMoveSize != IntPtr.Zero) UnhookWinEvent(_hookMoveSize);
            if (_hookForeground != IntPtr.Zero) UnhookWinEvent(_hookForeground);
            if (_hookObjectShow != IntPtr.Zero) UnhookWinEvent(_hookObjectShow);
            _hookLocation = _hookMoveSize = _hookForeground = _hookObjectShow = IntPtr.Zero;
        }
    }
}
