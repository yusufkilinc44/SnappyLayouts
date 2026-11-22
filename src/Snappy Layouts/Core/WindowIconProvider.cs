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
        /// Seçili alan (SnapArea) üzerinde halihazırda belirli bölgelere yerleşmiş açık pencerelerin ikonlarını eşler.
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

            var result = new Dictionary<int, ImageSource>();
            var usedHwnds = new HashSet<IntPtr>();

            for (int zIdx = 0; zIdx < zoneRects.Count; zIdx++)
            {
                var zr = zoneRects[zIdx];
                double bestIou = 0.52;
                WindowItem bestWin = null;

                foreach (var w in openWindows)
                {
                    if (w.Hwnd == excludeHwnd || w.Minimized || usedHwnds.Contains(w.Hwnd)) continue;
                    double iou = ComputeIoU(w.Frame, zr);
                    if (iou > bestIou)
                    {
                        bestIou = iou;
                        bestWin = w;
                    }
                }

                if (bestWin != null)
                {
                    var ico = GetIcon(bestWin.Hwnd, bestWin.ExePath);
                    if (ico != null)
                    {
                        result[zIdx] = ico;
                        usedHwnds.Add(bestWin.Hwnd);
                    }
                }
            }

            return result.Count > 0 ? result : null;
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
