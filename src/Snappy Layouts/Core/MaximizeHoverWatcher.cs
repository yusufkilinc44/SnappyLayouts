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
                // Yalnızca gerçek Snap Flyout/Bar popup'ıysa ve yakın zamanda maksimize tuşu üzerindeysek bastır;
                // Task View / Sanal Masaüstü Seçici / Başlat Menüsü pencerelerine ASLA dokunma!
                if (IsRecentlyHoveringMaxButton() && NativeSnapSuppressor.IsNativeSnapPopup(root))
                {
                    NativeSnapSuppressor.SuppressWindowInstant(root);
                    root = FindTopTargetWindowAtPoint(p);
                    child = root;
                    if (root == IntPtr.Zero) return IntPtr.Zero;
                }
                else
                {
                    return IntPtr.Zero;
                }
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

            if (!WindowPlacer.IsValidTarget(root) || !IsPointInCaptionButtonBand(root, p))
                return IntPtr.Zero;

            // 1) Anlık geometrik kontrol (0ms, hedef uygulamanın CPU/UI yükünden %100 bağımsız!)
            bool inGeomMax = IsPointInWindowMaxButton(root, p);

            // Hedef uygulama kilitli/yanıt vermiyorsa (örn. ağır PDF açan Adobe Acrobat) IPC beklemeden anında sonuç dön
            if (IsHungAppWindow(root))
                return inGeomMax ? root : IntPtr.Zero;

            // 2) Kısa zaman aşımlı WM_NCHITTEST ile kesin doğrulama (18ms — asla uzun bloklamaz)
            IntPtr lp = MakeLParam(p.X, p.Y);
            var (rootResp, rootHit) = TryHitTest(root, lp, timeoutMs: 18);
            if (!rootResp)
            {
                // Hedef uygulamanın UI iş parçacığı meşgul → geometrik maksimize kutusuna anında güven
                return inGeomMax ? root : IntPtr.Zero;
            }
            if (rootHit == HTMAXBUTTON) return root;
            if (rootHit == HTCLOSE || rootHit == HTMINBUTTON) return IntPtr.Zero;

            long childHit = -1;
            if (child != root)
            {
                var (childResp, cHit) = TryHitTest(child, lp, timeoutMs: 12);
                if (!childResp)
                    return inGeomMax ? root : IntPtr.Zero;
                childHit = cHit;
                if (childHit == HTMAXBUTTON) return root;
                if (childHit == HTCLOSE || childHit == HTMINBUTTON) return IntPtr.Zero;
            }

            // 3) Modern özel başlık çubuğu kullanan uygulamalar (Adobe Acrobat, Electron, Qt vb.)
            //    maksimize tuşu üzerinde bile HTCLIENT döndürebilir → sıkı geometrik eşleşme ile aç
            if (inGeomMax && (rootHit == HTCLIENT || childHit == HTCLIENT) && !IsFullscreenWindow(root))
                return root;

            return IntPtr.Zero;
        }

        private static RECT GetClampedFrame(IntPtr hwnd)
        {
            var fr = GetExtendedFrame(hwnd);
            if (IsZoomed(hwnd))
            {
                var mon = MonitorService.FromWindow(hwnd);
                if (mon != null)
                {
                    var wa = mon.WorkArea;
                    fr = new RECT(
                        Math.Max(fr.Left, wa.Left),
                        Math.Max(fr.Top, wa.Top),
                        Math.Min(fr.Right, wa.Right),
                        Math.Min(fr.Bottom, wa.Bottom));
                }
            }
            return fr;
        }

        private static bool IsFullscreenWindow(IntPtr hwnd)
        {
            var mon = MonitorService.FromWindow(hwnd);
            if (mon == null) return false;
            var fr = GetExtendedFrame(hwnd);
            return fr.Left <= mon.Bounds.Left && fr.Top <= mon.Bounds.Top &&
                   fr.Right >= mon.Bounds.Right && fr.Bottom >= mon.Bounds.Bottom &&
                   (GetStyle(hwnd) & WS_CAPTION) == 0;
        }

        private static bool IsPointInCaptionButtonBand(IntPtr hwnd, POINT p)
        {
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return false;
            if ((GetStyle(hwnd) & WS_MAXIMIZEBOX) == 0) return false;
            var fr = GetClampedFrame(hwnd);
            if (fr.Width < 120 || fr.Height < 60) return false;
            double scale = GetDpiForWindow(hwnd) / 96.0;
            if (scale <= 0.1) scale = 1.0;

            int bandH = (int)Math.Round(56 * scale);
            int bandW = (int)Math.Round(180 * scale);
            if (p.Y < fr.Top || p.Y > fr.Top + bandH) return false;

            bool isRtl = (GetExStyle(hwnd) & WS_EX_LAYOUTRTL) != 0;
            return isRtl
                ? (p.X >= fr.Left && p.X <= fr.Left + bandW)
                : (p.X >= fr.Right - bandW && p.X <= fr.Right);
        }

        public static bool IsPointInWindowMaxButton(IntPtr hwnd, POINT p)
        {
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return false;
            if ((GetStyle(hwnd) & WS_MAXIMIZEBOX) == 0) return false;
            var fr = GetClampedFrame(hwnd);
            if (fr.Width < 160 || fr.Height < 60) return false;
            double scale = GetDpiForWindow(hwnd) / 96.0;
            if (scale <= 0.1) scale = 1.0;

            // Standart Windows 10/11 ve modern uygulama (Adobe Acrobat, Chrome, Office) başlık butonları:
            // Kapat (~46px), Büyüt (~46px), Küçült (~46px); yükseklik 32-36px
            int btnW = (int)Math.Round(46 * scale);
            int btnH = (int)Math.Round(36 * scale);
            int inset = (int)Math.Round(2 * scale);
            if (p.Y < fr.Top || p.Y > fr.Top + btnH) return false;

            bool isRtl = (GetExStyle(hwnd) & WS_EX_LAYOUTRTL) != 0;
            if (isRtl)
            {
                int maxLeft = fr.Left + btnW + inset;
                int maxRight = fr.Left + btnW * 2 - inset;
                return p.X >= maxLeft && p.X <= maxRight;
            }
            else
            {
                int maxLeft = fr.Right - btnW * 2 + inset;
                int maxRight = fr.Right - btnW - inset;
                return p.X >= maxLeft && p.X <= maxRight;
            }
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

        private static (bool Responded, long HitCode) TryHitTest(IntPtr hwnd, IntPtr lp, uint timeoutMs)
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
                var r = SendMessageTimeout(hwnd, WM_NCHITTEST, IntPtr.Zero, lp, SMTO_ABORTIFHUNG, timeoutMs, out IntPtr res);
                if (r == IntPtr.Zero) return (false, 0);
                return (true, res.ToInt64());
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
