using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnappyLayout.Models;
using SnappyLayout.Native;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>
    /// Açık pencerelerin uygulama ikonlarını (Chrome profil rozeti, Explorer klasörü, UWP/WinUI ve Win32 uygulamaları dahil)
    /// hedef uygulamayı asla bloklamadan hızlı ve önbellekli olarak çıkarır; Snap Layout kutularına eşler.
    /// </summary>
    public static class WindowIconProvider
    {
        private const int WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL2 = 2;
        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
        private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        private static readonly Dictionary<IntPtr, (ImageSource Icon, long Tick)> _hwndCache = new Dictionary<IntPtr, (ImageSource, long)>();
        private static readonly Dictionary<string, ImageSource> _exeCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource GetIcon(IntPtr hwnd, string exePath)
        {
            long now = Environment.TickCount64;
            if (hwnd != IntPtr.Zero && _hwndCache.TryGetValue(hwnd, out var cached) && (now - cached.Tick) < 20000)
                return cached.Icon;

            ImageSource icon = null;

            // 1) Pencerenin kendi HICON'u (Chrome profil ikonu, Explorer klasör ikonu vb.) — donmuş pencereye asla IPC gönderme
            if (hwnd != IntPtr.Zero && IsWindow(hwnd))
            {
                IntPtr hIcon = IntPtr.Zero;
                if (!IsHungAppWindow(hwnd))
                {
                    if (SendMessageTimeout(hwnd, WM_GETICON, new IntPtr(ICON_BIG), IntPtr.Zero, SMTO_ABORTIFHUNG, 15, out IntPtr r1) != IntPtr.Zero && r1 != IntPtr.Zero)
                        hIcon = r1;
                    else if (SendMessageTimeout(hwnd, WM_GETICON, new IntPtr(ICON_SMALL2), IntPtr.Zero, SMTO_ABORTIFHUNG, 10, out IntPtr r2) != IntPtr.Zero && r2 != IntPtr.Zero)
                        hIcon = r2;
                    else if (SendMessageTimeout(hwnd, WM_GETICON, new IntPtr(ICON_SMALL), IntPtr.Zero, SMTO_ABORTIFHUNG, 10, out IntPtr r3) != IntPtr.Zero && r3 != IntPtr.Zero)
                        hIcon = r3;
                }

                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hwnd, GCLP_HICON);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hwnd, GCLP_HICONSM);

                if (hIcon != IntPtr.Zero)
                    icon = FromHIcon(hIcon, destroyHandle: false);

                // UWP (ApplicationFrameHost.exe) pencerelerinde gerçek CoreWindow alt sürecinin exe yolunu bul
                if (icon == null && string.Equals(Path.GetFileName(exePath ?? ""), "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
                {
                    IntPtr core = FindWindowEx(hwnd, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
                    if (core != IntPtr.Zero)
                    {
                        uint childPid = GetProcessId(core);
                        string realExe = WindowCatalog.GetExePath(childPid);
                        if (!string.IsNullOrEmpty(realExe)) exePath = realExe;
                    }
                }
            }

            // 2) Sıfır IPC yedeği: exe dosyasından yüksek kaliteli shell ikonu çıkar (önbellekli)
            if (icon == null && !string.IsNullOrEmpty(exePath))
                icon = GetExeIcon(exePath);

            if (hwnd != IntPtr.Zero)
            {
                if (_hwndCache.Count > 160) PruneHwndCache(now);
                _hwndCache[hwnd] = (icon, now);
            }
            return icon;
        }

        private static ImageSource GetExeIcon(string exePath)
        {
            if (_exeCache.TryGetValue(exePath, out var cached)) return cached;
            ImageSource icon = null;
            try
            {
                if (File.Exists(exePath))
                {
                    var shfi = new SHFILEINFO();
                    IntPtr res = SHGetFileInfo(exePath, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
                    if (res != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                        icon = FromHIcon(shfi.hIcon, destroyHandle: true);
                }
            }
            catch { }

            if (_exeCache.Count > 160) _exeCache.Clear();
            _exeCache[exePath] = icon;
            return icon;
        }

        private static ImageSource FromHIcon(IntPtr hIcon, bool destroyHandle)
        {
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            catch { return null; }
            finally
            {
                if (destroyHandle && hIcon != IntPtr.Zero)
                {
                    try { DestroyIcon(hIcon); } catch { }
                }
            }
        }

        private static void PruneHwndCache(long now)
        {
            var dead = _hwndCache.Where(kv => (now - kv.Value.Tick) >= 20000 || !IsWindow(kv.Key)).Select(kv => kv.Key).ToList();
            foreach (var k in dead) _hwndCache.Remove(k);
            if (_hwndCache.Count > 160) _hwndCache.Clear();
        }

        /// <summary>
        /// Seçili alan (SnapArea) üzerinde GERÇEKTEN tam o bölgenin boyut ve konumunda bulunan
        /// (ve üstü başka bir pencereyle kapanmamış) açık pencerelerin ikonlarını eşler.
        /// </summary>
        public static Dictionary<int, ImageSource> BuildOccupiedZoneIcons(SnapArea area, SnapLayout layout, IList<WindowItem> openWindows, IntPtr excludeHwnd)
        {
            if (area == null || openWindows == null || openWindows.Count == 0) return null;

            var zoneRects = new List<RECT>();
            if (layout == null)
            {
                zoneRects.Add(WindowPlacer.ZoneToPixels(area, new Rect(0, 0, 1, 1)));
            }
            else
            {
                for (int i = 0; i < layout.Zones.Count; i++)
                    zoneRects.Add(WindowPlacer.ZoneToPixels(area, layout.GetZoneRect(layout.Zones[i])));
            }

            // Kenar toleransı: yalnızca gerçekten o bölgeye yerleşmiş pencereleri kabul et
            // (ör. %50 pencere %66 veya %33 kutuda ASLA görünmesin)
            int edgeTol = (int)Math.Round(24 * Math.Max(1.0, area.Monitor.Scale));

            var result = new Dictionary<int, ImageSource>();
            var usedHwnds = new HashSet<IntPtr>();

            for (int zIdx = 0; zIdx < zoneRects.Count; zIdx++)
            {
                var zr = zoneRects[zIdx];
                WindowItem matchedWin = null;

                for (int wi = 0; wi < openWindows.Count; wi++)
                {
                    var w = openWindows[wi];
                    if (w.Hwnd == excludeHwnd || w.Minimized || usedHwnds.Contains(w.Hwnd)) continue;

                    // 4 kenar da hedef bölgeye tam oturmalı + IoU >= 0.85
                    if (Math.Abs(w.Frame.Left - zr.Left) > edgeTol ||
                        Math.Abs(w.Frame.Top - zr.Top) > edgeTol ||
                        Math.Abs(w.Frame.Right - zr.Right) > edgeTol ||
                        Math.Abs(w.Frame.Bottom - zr.Bottom) > edgeTol)
                        continue;

                    if (ComputeIoU(w.Frame, zr) < 0.85) continue;

                    // Z-sırasında kendisinden önde bulunan başka bir pencere bu pencereyi tamamen kapatıyorsa gösterme
                    if (IsOccludedByEarlierWindow(w.Frame, openWindows, wi, excludeHwnd))
                        continue;

                    matchedWin = w;
                    break; // openWindows Z-sırasındadır (önden arkaya) → en üstteki eşleşeni al
                }

                if (matchedWin != null)
                {
                    var ico = GetIcon(matchedWin.Hwnd, matchedWin.ExePath);
                    if (ico != null)
                    {
                        result[zIdx] = ico;
                        usedHwnds.Add(matchedWin.Hwnd);
                    }
                }
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Hedef pencere (targetHwnd) şu anda seçili alan (area) içindeki bu düzenin belirli bir bölgesine
        /// tam olarak yerleşmiş durumdaysa o bölgenin indeksini döndürür; değilse -1.
        /// </summary>
        public static int FindActiveWindowZoneIndex(SnapArea area, SnapLayout layout, IList<WindowItem> openWindows, IntPtr targetHwnd)
        {
            if (area == null || targetHwnd == IntPtr.Zero || !IsWindow(targetHwnd)) return -1;

            RECT frame;
            var item = openWindows?.FirstOrDefault(w => w.Hwnd == targetHwnd);
            if (item != null)
            {
                if (item.Minimized) return -1;
                frame = item.Frame;
            }
            else
            {
                if (IsIconic(targetHwnd)) return -1;
                frame = GetExtendedFrame(targetHwnd);
            }
            if (frame.Width <= 0 || frame.Height <= 0) return -1;

            int edgeTol = (int)Math.Round(24 * Math.Max(1.0, area.Monitor.Scale));

            if (layout == null)
            {
                var zr = WindowPlacer.ZoneToPixels(area, new Rect(0, 0, 1, 1));
                if (Math.Abs(frame.Left - zr.Left) <= edgeTol &&
                    Math.Abs(frame.Top - zr.Top) <= edgeTol &&
                    Math.Abs(frame.Right - zr.Right) <= edgeTol &&
                    Math.Abs(frame.Bottom - zr.Bottom) <= edgeTol &&
                    ComputeIoU(frame, zr) >= 0.85)
                {
                    return 0;
                }
                return -1;
            }

            for (int i = 0; i < layout.Zones.Count; i++)
            {
                var zr = WindowPlacer.ZoneToPixels(area, layout.GetZoneRect(layout.Zones[i]));
                if (Math.Abs(frame.Left - zr.Left) <= edgeTol &&
                    Math.Abs(frame.Top - zr.Top) <= edgeTol &&
                    Math.Abs(frame.Right - zr.Right) <= edgeTol &&
                    Math.Abs(frame.Bottom - zr.Bottom) <= edgeTol &&
                    ComputeIoU(frame, zr) >= 0.85)
                {
                    return i;
                }
            }
            return -1;
        }

        public static ImageSource GetWindowIcon(IList<WindowItem> openWindows, IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero) return null;
            var item = openWindows?.FirstOrDefault(w => w.Hwnd == targetHwnd);
            return GetIcon(targetHwnd, item?.ExePath);
        }

        private static bool IsOccludedByEarlierWindow(RECT target, IList<WindowItem> openWindows, int targetIndex, IntPtr excludeHwnd)
        {
            double targetArea = Math.Max(1.0, (double)target.Width * target.Height);
            for (int i = 0; i < targetIndex; i++)
            {
                var front = openWindows[i];
                if (front.Hwnd == excludeHwnd || front.Minimized) continue;
                int il = Math.Max(target.Left, front.Frame.Left);
                int it = Math.Max(target.Top, front.Frame.Top);
                int ir = Math.Min(target.Right, front.Frame.Right);
                int ib = Math.Min(target.Bottom, front.Frame.Bottom);
                if (ir <= il || ib <= it) continue;
                double covered = (double)(ir - il) * (ib - it);
                if (covered / targetArea >= 0.85) return true;
            }
            return false;
        }

        private static double ComputeIoU(RECT a, RECT b)
        {
            int il = Math.Max(a.Left, b.Left);
            int it = Math.Max(a.Top, b.Top);
            int ir = Math.Min(a.Right, b.Right);
            int ib = Math.Min(a.Bottom, b.Bottom);
            if (ir <= il || ib <= it) return 0;

            double inter = (double)(ir - il) * (ib - it);
            double areaA = Math.Max(1.0, (double)a.Width * a.Height);
            double areaB = Math.Max(1.0, (double)b.Width * b.Height);
            double union = areaA + areaB - inter;
            return union > 0 ? inter / union : 0;
        }
    }
}
