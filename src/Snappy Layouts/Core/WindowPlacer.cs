using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using SnappyLayout.Native;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>
    /// Pencereleri piksel hassasiyetinde yerleştirir.
    /// - Windows 10/11'deki görünmez pencere kenarlıklarını (DWM extended frame) telafi eder.
    /// - Farklı DPI'lı (100/125/150%...) monitörler arasında taşırken pencere kendi boyutunu
    ///   yeniden ölçeklediği için yerleştirmeyi iki geçişte yapar (dinamik DPI hesaplaması).
    /// </summary>
    public static class WindowPlacer
    {
        public const string PropPhysicalMax = "SnappyLayouts.PhysicalMax";

        /// <summary>Sanal tam ekran yapılmış pencerelerin önceki konumu (geri almak için).</summary>
        private static readonly Dictionary<IntPtr, (RECT Previous, RECT Virtual)> _virtualMax = new Dictionary<IntPtr, (RECT, RECT)>();

        public static IntPtr MainWindowHandle { get; set; }

        public static bool IsValidTarget(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd)) return false;
            if (hwnd == GetShellWindow()) return false;
            long style = GetStyle(hwnd);
            if ((style & WS_CHILD) != 0) return false;
            string cls = NativeMethods.GetClassName(hwnd);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
            if (GetProcessId(hwnd) == (uint)Environment.ProcessId)
                return MainWindowHandle != IntPtr.Zero && hwnd == MainWindowHandle;
            return true;
        }

        public static RECT GetNormalRectScreen(IntPtr hwnd)
        {
            if (!IsZoomed(hwnd) && !IsIconic(hwnd))
            {
                GetWindowRect(hwnd, out var wr);
                return wr;
            }
            var wp = new WINDOWPLACEMENT { length = System.Runtime.InteropServices.Marshal.SizeOf<WINDOWPLACEMENT>() };
            GetWindowPlacement(hwnd, ref wp);
            var r = wp.rcNormalPosition;
            // rcNormalPosition "workspace" koordinatındadır (birincil monitörün çalışma alanına göre)
            long ex = GetExStyle(hwnd);
            if ((ex & WS_EX_TOOLWINDOW) == 0)
            {
                var prim = MonitorService.GetMonitors().FirstOrDefault(m => m.IsPrimary);
                if (prim != null)
                {
                    int dx = prim.WorkArea.Left - prim.Bounds.Left, dy = prim.WorkArea.Top - prim.Bounds.Top;
                    r = new RECT(r.Left + dx, r.Top + dy, r.Right + dx, r.Bottom + dy);
                }
            }
            return r;
        }

        /// <summary>Pencerenin görünen çerçevesini tam olarak hedef dikdörtgene getirir (fiziksel px).</summary>
        public static void Place(IntPtr hwnd, RECT target, bool activate = false)
        {
            if (!IsWindow(hwnd)) return;
            try
            {
                RemoveProp(hwnd, PropPhysicalMax);
                if (IsZoomed(hwnd) || IsIconic(hwnd))
                {
                    // Önce normal konumu hedefe ayarla ki geri yükleme animasyonu doğrudan oraya gitsin
                    var wp = new WINDOWPLACEMENT { length = System.Runtime.InteropServices.Marshal.SizeOf<WINDOWPLACEMENT>() };
                    GetWindowPlacement(hwnd, ref wp);
                    wp.showCmd = SW_SHOWNORMAL;
                    wp.rcNormalPosition = ToWorkspace(hwnd, target);
                    wp.flags = 0;
                    SetWindowPlacement(hwnd, ref wp);
                }

                var targetMon = MonitorService.FromRect(target);
                var curMon = MonitorService.FromWindow(hwnd);

                // 1. geçiş: pencereyi hedef monitöre taşı (DPI değişirse pencere kendini ölçekler)
                SetFrame(hwnd, target);
                // 2. geçiş: yeni DPI'da görünmez kenarlıklar farklı olabilir → tekrar hesapla
                if (curMon.Dpi != targetMon.Dpi || curMon.Handle != targetMon.Handle)
                    SetFrame(hwnd, target);
                else
                {
                    var fr = GetExtendedFrame(hwnd);
                    if (!fr.SameAs(target, 1)) SetFrame(hwnd, target);
                }

                if (activate) SetForegroundWindow(hwnd);
            }
            catch (Exception ex) { Logger.Log("Place failed: " + ex.Message); }
        }

        private static void SetFrame(IntPtr hwnd, RECT target)
        {
            GetWindowRect(hwnd, out var wr);
            var fr = GetExtendedFrame(hwnd);
            int ml = fr.Left - wr.Left, mt = fr.Top - wr.Top, mr = wr.Right - fr.Right, mb = wr.Bottom - fr.Bottom;
            // Mantıksız değerleri yok say (bazı uygulamalar DWM çerçevesi bildirmez)
            if (ml < 0 || ml > 40) ml = 0; if (mt < 0 || mt > 40) mt = 0;
            if (mr < 0 || mr > 40) mr = 0; if (mb < 0 || mb > 40) mb = 0;
            SetWindowPos(hwnd, IntPtr.Zero,
                target.Left - ml, target.Top - mt,
                target.Width + ml + mr, target.Height + mt + mb,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }

        private static RECT ToWorkspace(IntPtr hwnd, RECT r)
        {
            long ex = GetExStyle(hwnd);
            if ((ex & WS_EX_TOOLWINDOW) != 0) return r;
            var prim = MonitorService.GetMonitors().FirstOrDefault(m => m.IsPrimary);
            if (prim == null) return r;
            int dx = prim.WorkArea.Left - prim.Bounds.Left, dy = prim.WorkArea.Top - prim.Bounds.Top;
            return new RECT(r.Left - dx, r.Top - dy, r.Right - dx, r.Bottom - dy);
        }

        /// <summary>Göreli dikdörtgeni (0..1) hedef alana boşluk (gap) uygulayarak çevirir.</summary>
        public static RECT ZoneToPixels(SnapArea area, Rect rel)
        {
            var a = area.Rect;
            int gap = (int)Math.Round(Storage.Data.Settings.ZoneGap * area.Monitor.Scale);
            int l = a.Left + (int)Math.Round(rel.Left * a.Width);
            int t = a.Top + (int)Math.Round(rel.Top * a.Height);
            int r = a.Left + (int)Math.Round(rel.Right * a.Width);
            int b = a.Top + (int)Math.Round(rel.Bottom * a.Height);
            if (gap > 0)
            {
                int half = gap / 2;
                l += rel.Left <= 0.0001 ? gap : half;
                t += rel.Top <= 0.0001 ? gap : half;
                r -= rel.Right >= 0.9999 ? gap : gap - half;
                b -= rel.Bottom >= 0.9999 ? gap : gap - half;
            }
            return new RECT(l, t, Math.Max(l + 50, r), Math.Max(t + 50, b));
        }

        public static void SnapToZone(IntPtr hwnd, SnapArea area, Rect rel)
        {
            Forget(hwnd);
            Place(hwnd, ZoneToPixels(area, rel), activate: true);
        }

        /// <summary>Pencereyi sanal monitörün tamamına yayar (sanal tam ekran).</summary>
        public static void VirtualMaximize(IntPtr hwnd, SnapArea area, bool allowToggle = true)
        {
            if (area == null) return;
            var target = ZoneToPixels(area, new Rect(0, 0, 1, 1));
            bool zoomed = IsZoomed(hwnd);
            RECT current = zoomed ? FrameFromNormal(hwnd) : GetExtendedFrame(hwnd);
            int tol = zoomed ? 14 : 3;
            if (allowToggle && _virtualMax.TryGetValue(hwnd, out var st) && current.SameAs(st.Virtual, tol))
            {
                // zaten sanal tam ekrandı ve tekrar basıldı → önceki boyuta dön
                _virtualMax.Remove(hwnd);
                Place(hwnd, st.Previous, true);
                return;
            }
            RECT prev = current;
            if (prev.SameAs(target, tol) && _virtualMax.TryGetValue(hwnd, out var old)) prev = old.Previous;
            Place(hwnd, target, true);
            _virtualMax[hwnd] = (prev, target);
        }

        private static RECT FrameFromNormal(IntPtr hwnd)
        {
            var r = GetNormalRectScreen(hwnd);
            // yaklaşık görünmez kenarlık
            int m = (int)Math.Round(7 * (GetDpiForWindow(hwnd) / 96.0));
            return new RECT(r.Left + m, r.Top, r.Right - m, r.Bottom - m);
        }

        public static bool IsVirtualMaximized(IntPtr hwnd)
        {
            if (!_virtualMax.TryGetValue(hwnd, out var st)) return false;
            return GetExtendedFrame(hwnd).SameAs(st.Virtual, 3);
        }

        public static void RestoreFromVirtual(IntPtr hwnd)
        {
            if (_virtualMax.TryGetValue(hwnd, out var st))
            {
                _virtualMax.Remove(hwnd);
                Place(hwnd, st.Previous, true);
            }
            else if (IsZoomed(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        }

        public static void Forget(IntPtr hwnd) => _virtualMax.Remove(hwnd);

        /// <summary>Gerçek fiziksel monitörü tamamen kaplayan (Windows'un kendi) maksimize.</summary>
        public static void PhysicalMaximize(IntPtr hwnd, MonitorInfo monitor = null)
        {
            if (!IsWindow(hwnd)) return;
            Forget(hwnd);
            SetProp(hwnd, PropPhysicalMax, new IntPtr(1));
            if (monitor != null && MonitorService.FromWindow(hwnd).Handle != monitor.Handle)
            {
                if (IsZoomed(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                var w = monitor.WorkArea;
                Place(hwnd, new RECT(w.Left + w.Width / 6, w.Top + w.Height / 6, w.Right - w.Width / 6, w.Bottom - w.Height / 6));
                SetProp(hwnd, PropPhysicalMax, new IntPtr(1));
            }
            if (IsZoomed(hwnd))
            {
                // zaten maksimize: yine de öne getir
                SetForegroundWindow(hwnd);
                return;
            }
            ShowWindow(hwnd, SW_MAXIMIZE);
            SetForegroundWindow(hwnd);
        }

        public static void CleanupDeadWindows()
        {
            foreach (var h in _virtualMax.Keys.ToList())
                if (!IsWindow(h)) _virtualMax.Remove(h);
        }
    }
}
