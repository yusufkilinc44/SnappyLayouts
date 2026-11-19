using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using SnappyLayout.Native;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>
    /// Sanal monitörler aktifken bir pencerenin Maksimize (Büyüt) tuşuna basıldığında,
    /// tıklamayı Windows fiziksel tam ekran yapmadan önce yakalar ve pencereyi doğrudan
    /// bulunduğu sanal monitörde tam ekran yapar (fiziksel tam ekrana sıçrama/flicker olmaz).
    /// </summary>
    public sealed class MaximizeClickInterceptor : IDisposable
    {
        private IntPtr _hook;
        private readonly LowLevelMouseProc _proc;
        private readonly Dispatcher _dispatcher;
        private bool _swallowNextLeftUp;
        private long _swallowTick;

        public MaximizeClickInterceptor()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _proc = HookCallback;
            using var curProc = Process.GetCurrentProcess();
            using var curMod = curProc.MainModule;
            IntPtr hMod = GetModuleHandle(curMod?.ModuleName);
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, hMod, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONUP)
                {
                    if (_swallowNextLeftUp)
                    {
                        _swallowNextLeftUp = false;
                        if (Environment.TickCount64 - _swallowTick < 2000)
                            return new IntPtr(1);
                    }
                }
                else if (msg == WM_LBUTTONDOWN)
                {
                    try
                    {
                        if (TryInterceptMaximizeClick(lParam))
                        {
                            _swallowNextLeftUp = true;
                            _swallowTick = Environment.TickCount64;
                            return new IntPtr(1);
                        }
                    }
                    catch { }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private bool TryInterceptMaximizeClick(IntPtr lParam)
        {
            var app = App.Instance;
            if (app == null || app.Paused) return false;

            var s = Storage.Data?.Settings;
            if (s == null || !s.VirtualMonitorsEnabled || !s.VirtualMaximize) return false;

            // Shift basılıysa kullanıcı gerçek fiziksel tam ekran istiyor → engelleme
            if (s.ShiftBypassesVirtualMaximize && IsKeyDown(VK_SHIFT)) return false;

            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            POINT pt = info.pt;

            // Eğer tıklama Snappy Layout Flyout penceresinin içindeyse engelleme
            if (app.Flyout != null && app.Flyout.IsVisible)
            {
                var flyHwnd = new WindowInteropHelper(app.Flyout).Handle;
                if (flyHwnd != IntPtr.Zero && GetWindowRect(flyHwnd, out var flyRect) && flyRect.Contains(pt))
                    return false;
            }

            IntPtr targetHwnd = IntPtr.Zero;

            // 1) Hızlı yol: MaximizeHoverWatcher zaten bu noktadaki maksimize tuşunu tespit etmişse 0ms'de al
            if (app.HoverWatcher != null)
            {
                var snap = app.HoverWatcher.Current;
                if (snap.Window != IntPtr.Zero &&
                    Math.Abs(snap.Cursor.X - pt.X) <= 10 &&
                    Math.Abs(snap.Cursor.Y - pt.Y) <= 10)
                {
                    targetHwnd = snap.Window;
                }
            }

            // 2) Hızlı geometrik yedek kontrol (kullanıcı hover beklemeden çok hızlı tıkladıysa)
            if (targetHwnd == IntPtr.Zero)
            {
                IntPtr child = WindowFromPoint(pt);
                if (child != IntPtr.Zero)
                {
                    IntPtr root = GetAncestor(child, GA_ROOT);
                    if (root == IntPtr.Zero) root = child;
                    if (WindowPlacer.IsValidTarget(root) && MaximizeHoverWatcher.IsPointInWindowMaxButton(root, pt))
                        targetHwnd = root;
                }
            }

            if (targetHwnd == IntPtr.Zero || !WindowPlacer.IsValidTarget(targetHwnd)) return false;
            if ((GetStyle(targetHwnd) & WS_MAXIMIZEBOX) == 0) return false;

            // Eğer pencere zaten fiziksel olarak maksimize edilmişse normal restore'a izin ver
            if (IsZoomed(targetHwnd)) return false;

            // Hedef monitörde sanal monitörler gerçekten bölünmüş mü kontrol et
            var mon = MonitorService.FromPoint(pt);
            if (AreaService.GetWeights(mon) == null) return false;

            var area = AreaService.AreaFromPoint(pt);
            if (area == null || !area.IsVirtual) return false;

            IntPtr capturedHwnd = targetHwnd;
            var capturedArea = area;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                app.Flyout?.HideFlyout();
                if (IsWindow(capturedHwnd))
                {
                    WindowPlacer.VirtualMaximize(capturedHwnd, capturedArea, allowToggle: true);
                }
            }), DispatcherPriority.Send);

            return true;
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
