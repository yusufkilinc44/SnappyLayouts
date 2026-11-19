using System;
using System.Threading;
using SnappyLayout.Native;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>
    /// Fare imlecinin herhangi bir pencerenin maksimize tuşu üzerinde olup olmadığını arka planda izler.
    /// Windows'un kendi Snap Layouts menüsüyle aynı yöntemi kullanır: WM_NCHITTEST → HTMAXBUTTON.
    /// </summary>
    public sealed class MaximizeHoverWatcher : IDisposable
    {
        public struct Snapshot
        {
            public IntPtr Window;     // maksimize tuşu üzerinde olunan üst düzey pencere (yoksa Zero)
            public POINT Cursor;
            public bool MouseDown;
            public long Tick;
        }

        private readonly Timer _timer;
        private volatile bool _enabled = true;
        private Snapshot _snap;
        private readonly object _lock = new object();
        private int _busy;

        private static long _selfMaxHoverTick;
        private static long _anyMaxHoverTick;

        public MaximizeHoverWatcher()
        {
            _timer = new Timer(Tick, null, 80, 25);
        }

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public Snapshot Current { get { lock (_lock) return _snap; } }

        public static void NotifySelfMaxHover(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero && hwnd == WindowPlacer.MainWindowHandle)
            {
                Interlocked.Exchange(ref _selfMaxHoverTick, Environment.TickCount64);
                Interlocked.Exchange(ref _anyMaxHoverTick, Environment.TickCount64);
            }
        }

        public static bool IsRecentlyHoveringMaxButton()
        {
            return (Environment.TickCount64 - Interlocked.Read(ref _anyMaxHoverTick)) < 1500;
        }

        private void Tick(object state)
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                var s = new Snapshot { Tick = Environment.TickCount64 };
                if (_enabled && GetCursorPos(out var p))
                {
                    s.Cursor = p;
                    s.MouseDown = IsKeyDown(VK_LBUTTON) || IsKeyDown(VK_RBUTTON);
                    if (!s.MouseDown)
                    {
                        s.Window = HitMaxButton(p);
                        if (s.Window != IntPtr.Zero)
                        {
                            Interlocked.Exchange(ref _anyMaxHoverTick, Environment.TickCount64);
                            NativeSnapSuppressor.DismissActiveNativeFlyout();
                        }
                    }
                }
                lock (_lock) _snap = s;
            }
            catch { }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        public static IntPtr HitMaxButton(POINT p)
        {
            IntPtr child = WindowFromPoint(p);
            if (child == IntPtr.Zero) return IntPtr.Zero;
            IntPtr root = GetAncestor(child, GA_ROOT);
            if (root == IntPtr.Zero) root = child;

            string rootCls = NativeMethods.GetClassName(root);
            if (NativeSnapSuppressor.IsNativeSnapWindowClass(rootCls))
            {
                NativeSnapSuppressor.SuppressWindowInstant(root);
                root = FindTopTargetWindowAtPoint(p);
                child = root;
                if (root == IntPtr.Zero) return IntPtr.Zero;
            }

            if (GetProcessId(root) == (uint)Environment.ProcessId)
            {
                if (WindowPlacer.MainWindowHandle != IntPtr.Zero && root == WindowPlacer.MainWindowHandle)
                {
                    if ((Environment.TickCount64 - Interlocked.Read(ref _selfMaxHoverTick)) < 250 ||
                        IsPointInWindowMaxButton(root, p))
                    {
                        return root;
                    }
                }
                return IntPtr.Zero;
            }

            IntPtr lp = MakeLParam(p.X, p.Y);
            if (Hit(child, lp)) return root;
            if (root != child && Hit(root, lp)) return root;
            return IntPtr.Zero;
        }

        public static bool IsPointInWindowMaxButton(IntPtr hwnd, POINT p)
        {
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return false;
            if ((GetStyle(hwnd) & WS_MAXIMIZEBOX) == 0) return false;
            var fr = GetExtendedFrame(hwnd);
            double scale = GetDpiForWindow(hwnd) / 96.0;
            if (scale <= 0.1) scale = 1.0;
            // Standart Windows 10/11 başlık çubuğu sağ üst butonları: Kapat (~46px), Büyüt (~46px)
            int btnW = (int)Math.Round(46 * scale);
            int btnH = (int)Math.Round(32 * scale);
            int maxLeft = fr.Right - btnW * 2 - (int)Math.Round(4 * scale);
            int maxRight = fr.Right - btnW + (int)Math.Round(2 * scale);
            return p.X >= maxLeft && p.X <= maxRight && p.Y >= fr.Top && p.Y <= fr.Top + btnH;
        }

        private static IntPtr FindTopTargetWindowAtPoint(POINT p)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                if (!WindowPlacer.IsValidTarget(h) || IsIconic(h) || IsCloaked(h)) return true;
                var fr = GetExtendedFrame(h);
                if (fr.Contains(p))
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static bool Hit(IntPtr hwnd, IntPtr lp)
        {
            IntPtr prevCtx = IntPtr.Zero;
            try
            {
                IntPtr winCtx = GetWindowDpiAwarenessContext(hwnd);
                if (winCtx != IntPtr.Zero) prevCtx = SetThreadDpiAwarenessContext(winCtx);
            }
            catch { }

            try
            {
                var r = SendMessageTimeout(hwnd, WM_NCHITTEST, IntPtr.Zero, lp, SMTO_ABORTIFHUNG, 50, out IntPtr res);
                return r != IntPtr.Zero && res.ToInt64() == HTMAXBUTTON;
            }
            finally
            {
                if (prevCtx != IntPtr.Zero)
                {
                    try { SetThreadDpiAwarenessContext(prevCtx); } catch { }
                }
            }
        }

        public void Dispose() => _timer.Dispose();
    }
}
