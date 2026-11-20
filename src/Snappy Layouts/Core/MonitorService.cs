using System;
using System.Collections.Generic;
using System.Linq;
using SnappyLayout.Models;
using SnappyLayout.Native;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>Fiziksel monitör bilgisi. Tüm koordinatlar fiziksel piksel (Per-Monitor V2 DPI).</summary>
    public class MonitorInfo
    {
        public IntPtr Handle;
        public string DeviceName;
        public string Key;
        public string FriendlyName;
        public int Index;               // 1'den başlar (soldan sağa)
        public RECT Bounds;
        public RECT WorkArea;
        public bool IsPrimary;
        public uint Dpi = 96;
        public double Scale => Dpi / 96.0;
        public double Aspect => WorkArea.Height == 0 ? 1.777 : (double)WorkArea.Width / WorkArea.Height;
        public double FullAspect => Bounds.Height == 0 ? 1.777 : (double)Bounds.Width / Bounds.Height;
        public string AspectLabel => MonitorService.FormatAspectRatio(FullAspect);

        public string HardwareName => FriendlyName ?? DeviceName?.TrimStart('\\', '.') ?? $"Monitor {Index}";

        public string EffectiveName
        {
            get
            {
                if (!string.IsNullOrEmpty(Key) &&
                    Storage.Data?.MonitorCustomNames != null &&
                    Storage.Data.MonitorCustomNames.TryGetValue(Key, out var custom) &&
                    !string.IsNullOrWhiteSpace(custom))
                {
                    return custom.Trim();
                }
                return HardwareName;
            }
        }

        public bool HasCustomName =>
            !string.IsNullOrEmpty(Key) &&
            Storage.Data?.MonitorCustomNames != null &&
            Storage.Data.MonitorCustomNames.TryGetValue(Key, out var custom) &&
            !string.IsNullOrWhiteSpace(custom);

        public string DisplayName =>
            $"{Index}. {EffectiveName} — {Bounds.Width}×{Bounds.Height} @ {Math.Round(Scale * 100)}%"
            + (IsPrimary ? " ★" : "");

        public string ShortName => $"{Index}. {EffectiveName}";
    }

    /// <summary>
    /// Pencerelerin yerleştirileceği hedef alan:
    /// - Monitörde sanal monitör yoksa: fiziksel monitörün kendisi (SegmentIndex = -1).
    /// - Monitörde sanal monitörler oluşturulduysa: ilgili sanal monitör (SegmentIndex = 0..N-1).
    /// Her hedef alanın kendi bağımsız Pencere Yerleşim Düzenleri (LayoutKey) vardır.
    /// </summary>
    public class SnapArea
    {
        public MonitorInfo Monitor;
        public int SegmentIndex = -1;    // -1: fiziksel monitörün tamamı
        public int SegmentEndIndex = -1; // > SegmentIndex ise birden fazla yan yana sanal monitörü kapsar (örn. 1+2)
        public int SegmentCount = 1;
        public RECT Rect;                 // fiziksel piksel (çalışma alanı içinde)
        public bool IsVirtual => SegmentIndex >= 0 && SegmentCount > 1;
        public bool IsSpan => IsVirtual && SegmentEndIndex > SegmentIndex;
        public double Aspect => Rect.Height == 0 ? 1.777 : (double)Rect.Width / Rect.Height;
        public string AspectLabel => IsVirtual
            ? MonitorService.FormatAspectRatio(Aspect)
            : Monitor.AspectLabel;

        /// <summary>Bu fiziksel veya sanal monitöre özel pencere yerleşim düzenlerinin saklama anahtarı.</summary>
        public string LayoutKey => IsVirtual
            ? $"{Monitor.Key}::vm:{SegmentIndex}"
            : $"{Monitor.Key}::phys";

        public string Label
        {
            get
            {
                if (IsSpan)
                    return $"{Monitor.ShortName}  ▸  {Loc.T("VirtualMonitor")} #{SegmentIndex + 1}–#{SegmentEndIndex + 1}  ({Rect.Width}×{Rect.Height})";
                if (IsVirtual)
                    return $"{Monitor.ShortName}  ▸  {Loc.T("VirtualMonitor")} #{SegmentIndex + 1}/{SegmentCount}  ({Rect.Width}×{Rect.Height})";
                return $"{Monitor.ShortName}  ▸  {Loc.T("FullPhysicalMonitor")}  ({Rect.Width}×{Rect.Height})";
            }
        }

        public string ShortSegmentLabel
        {
            get
            {
                if (IsVirtual)
                    return $"{Loc.T("VirtualMonitor")} #{SegmentIndex + 1} ({Rect.Width}×{Rect.Height} px • {AspectLabel})";
                return $"{Loc.T("FullPhysicalMonitor")} ({Rect.Width}×{Rect.Height} px • {AspectLabel})";
            }
        }
    }

    public static class MonitorService
    {
        private static List<MonitorInfo> _cache;
        private static long _cacheTick;
        public static event Action MonitorsChanged;

        public static List<MonitorInfo> GetMonitors(bool refresh = false)
        {
            // Önbelleği en fazla 1.5 saniye tut: monitör çıkarılıp takıldığında veya uykudan çıkınca asla bayat monitör kalmasın
            if (_cache != null && !refresh && (Environment.TickCount64 - _cacheTick) < 1500)
                return _cache;
            var names = DisplayConfig.Query();
            var list = new List<MonitorInfo>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr d) =>
            {
                var mi = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
                if (GetMonitorInfo(h, ref mi))
                {
                    var info = new MonitorInfo
                    {
                        Handle = h,
                        DeviceName = mi.szDevice,
                        Bounds = mi.rcMonitor,
                        WorkArea = mi.rcWork,
                        IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                    };
                    if (GetDpiForMonitor(h, 0, out uint dx, out _) == 0) info.Dpi = dx;
                    if (names.TryGetValue(mi.szDevice ?? "", out var n))
                    {
                        info.FriendlyName = n.FriendlyName;
                        info.Key = n.DevicePath;
                    }
                    if (string.IsNullOrEmpty(info.Key)) info.Key = mi.szDevice;
                    list.Add(info);
                }
                return true;
            }, IntPtr.Zero);

            list = list.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToList();
            var seen = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Index = i + 1;
                if (!seen.Add(list[i].Key)) { list[i].Key += "#" + list[i].DeviceName; seen.Add(list[i].Key); }
            }
            _cache = list;
            _cacheTick = Environment.TickCount64;
            return list;
        }

        public static void Refresh()
        {
            GetMonitors(true);
            MonitorsChanged?.Invoke();
        }

        public static void SetCustomName(MonitorInfo mon, string customName)
        {
            if (mon == null || string.IsNullOrEmpty(mon.Key)) return;
            if (Storage.Data.MonitorCustomNames == null)
                Storage.Data.MonitorCustomNames = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(customName))
                Storage.Data.MonitorCustomNames.Remove(mon.Key);
            else
                Storage.Data.MonitorCustomNames[mon.Key] = customName.Trim();

            Storage.Save();
        }

        public static MonitorInfo FromHandle(IntPtr hMon)
        {
            var m = GetMonitors().FirstOrDefault(x => x.Handle == hMon);
            if (m == null) m = GetMonitors(true).FirstOrDefault(x => x.Handle == hMon);
            return m ?? GetMonitors().FirstOrDefault(x => x.IsPrimary) ?? GetMonitors().First();
        }

        public static MonitorInfo FromWindow(IntPtr hwnd) => FromHandle(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST));
        public static MonitorInfo FromPoint(POINT p) => FromHandle(MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST));
        public static MonitorInfo FromRect(RECT r) => FromHandle(MonitorFromRect(ref r, MONITOR_DEFAULTTONEAREST));
        public static MonitorInfo FindByKey(string key) => GetMonitors().FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// En/boy oranını 1.58:1 gibi ondalık değer yerine 16:9, 16:10, 21:9, 32:9, 3:2, 4:3 gibi
        /// bilinen standart oran biçimine dönüştürür.
        /// </summary>
        public static string FormatAspectRatio(double aspect)
        {
            if (aspect <= 0 || double.IsNaN(aspect) || double.IsInfinity(aspect)) return "16:9";

            var known = new (double Ratio, string Label)[]
            {
                (32.0 / 9.0, "32:9"),
                (21.0 / 9.0, "21:9"),
                (16.0 / 9.0, "16:9"),
                (16.0 / 10.0, "16:10"),
                (3.0 / 2.0, "3:2"),
                (4.0 / 3.0, "4:3"),
                (5.0 / 4.0, "5:4"),
                (1.0, "1:1"),
                (4.0 / 5.0, "4:5"),
                (3.0 / 4.0, "3:4"),
                (2.0 / 3.0, "2:3"),
                (10.0 / 16.0, "10:16"),
                (9.0 / 16.0, "9:16"),
                (9.0 / 21.0, "9:21"),
            };

            string bestKnown = null;
            double bestDiff = double.MaxValue;
            foreach (var (r, label) in known)
            {
                double relDiff = Math.Abs(aspect - r) / r;
                if (relDiff < bestDiff)
                {
                    bestDiff = relDiff;
                    bestKnown = label;
                }
            }
            if (bestDiff <= 0.075) return bestKnown;

            // Bilinen oranlara yakın değilse en yakın küçük tam sayı oranını (örn. 5:6, 7:9) bul
            int bestW = 1, bestH = 1;
            double bestErr = double.MaxValue;
            for (int h = 1; h <= 12; h++)
            {
                int w = Math.Max(1, (int)Math.Round(aspect * h));
                double err = Math.Abs((double)w / h - aspect);
                if (err < bestErr)
                {
                    bestErr = err;
                    bestW = w;
                    bestH = h;
                }
            }
            int g = Gcd(bestW, bestH);
            return $"{bestW / g}:{bestH / g}";
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) { int t = a % b; a = b; b = t; }
            return Math.Max(1, a);
        }
    }

    /// <summary>
    /// Sanal monitör yapılandırmasını okuyup hedef alanları (SnapArea) ve
    /// her fiziksel/sanal monitöre özel Pencere Yerleşim Düzenlerini yönetir.
    /// </summary>
    public static class AreaService
    {
        public static List<double> GetWeights(MonitorInfo m)
        {
            var d = Storage.Data;
            if (!d.Settings.VirtualMonitorsEnabled) return null;
            if (d.VirtualMonitors.TryGetValue(m.Key, out var w) && w != null && w.Count > 1) return w;
            return null;
        }

        public static void SetWeights(MonitorInfo m, IList<double> weights)
        {
            if (weights == null || weights.Count <= 1) Storage.Data.VirtualMonitors.Remove(m.Key);
            else Storage.Data.VirtualMonitors[m.Key] = weights.ToList();
            Storage.Save();
        }

        public static List<double> GetStoredWeights(MonitorInfo m)
        {
            return Storage.Data.VirtualMonitors.TryGetValue(m.Key, out var w) && w != null && w.Count > 0 ? w.ToList() : new List<double> { 1 };
        }

        /// <summary>Monitörün çalışma alanını yan yana sanal monitörlere böler.</summary>
        public static List<RECT> SplitRect(RECT work, IList<double> weights)
        {
            var res = new List<RECT>();
            if (weights == null || weights.Count <= 1) { res.Add(work); return res; }
            var off = SnapLayout.Offsets(weights);
            for (int i = 0; i < weights.Count; i++)
            {
                int l = work.Left + (int)Math.Round(off[i] * work.Width);
                int r = work.Left + (int)Math.Round(off[i + 1] * work.Width);
                res.Add(new RECT(l, work.Top, r, work.Bottom));
            }
            return res;
        }

        /// <summary>
        /// Bir fiziksel monitörün aktif hedef alanları:
        /// - Sanal monitörler oluşturulmuşsa yalnızca sanal monitörleri döndürür (gerçek fiziksel monitöre göre değil, sanal monitöre göre).
        /// - Sanal monitör yoksa fiziksel monitörün kendisini döndürür.
        /// </summary>
        public static List<SnapArea> GetAreas(MonitorInfo m)
        {
            var w = GetWeights(m);
            var rects = SplitRect(m.WorkArea, w);
            var list = new List<SnapArea>();
            if (rects.Count == 1)
            {
                list.Add(new SnapArea { Monitor = m, SegmentIndex = -1, SegmentCount = 1, Rect = rects[0] });
                return list;
            }
            for (int i = 0; i < rects.Count; i++)
                list.Add(new SnapArea { Monitor = m, SegmentIndex = i, SegmentCount = rects.Count, Rect = rects[i] });
            return list;
        }

        public static SnapArea PhysicalArea(MonitorInfo m) => new SnapArea { Monitor = m, SegmentIndex = -1, SegmentCount = 1, Rect = m.WorkArea };

        /// <summary>Yan yana birden fazla sanal monitörü (startIdx..endIdx) tek bir tam ekran alanında birleştirir.</summary>
        public static SnapArea CombineAreas(IList<SnapArea> areas, int startIdx, int endIdx)
        {
            if (areas == null || areas.Count == 0) return null;
            int s = Math.Clamp(Math.Min(startIdx, endIdx), 0, areas.Count - 1);
            int e = Math.Clamp(Math.Max(startIdx, endIdx), 0, areas.Count - 1);
            if (s == e) return areas[s];

            var first = areas[s];
            var last = areas[e];
            var combinedRect = new RECT(
                Math.Min(first.Rect.Left, last.Rect.Left),
                Math.Min(first.Rect.Top, last.Rect.Top),
                Math.Max(first.Rect.Right, last.Rect.Right),
                Math.Max(first.Rect.Bottom, last.Rect.Bottom));

            return new SnapArea
            {
                Monitor = first.Monitor,
                SegmentIndex = s,
                SegmentEndIndex = e,
                SegmentCount = first.SegmentCount,
                Rect = combinedRect
            };
        }

        public static SnapArea AreaFromPoint(POINT p)
        {
            var m = MonitorService.FromPoint(p);
            var areas = GetAreas(m);
            return areas.FirstOrDefault(a => a.Rect.Contains(p)) ?? Nearest(areas, p);
        }

        private static SnapArea Nearest(List<SnapArea> areas, POINT p)
        {
            return areas.OrderBy(a => Math.Abs(a.Rect.Center.X - p.X)).First();
        }

        /// <summary>Pencerenin (normal konumunun) merkezinin bulunduğu alan.</summary>
        public static SnapArea AreaForWindow(IntPtr hwnd)
        {
            var r = WindowPlacer.GetNormalRectScreen(hwnd);
            var m = MonitorService.FromWindow(hwnd);
            var c = r.Center;
            if (!m.Bounds.Contains(c)) c = m.WorkArea.Center;
            var areas = GetAreas(m);
            return areas.FirstOrDefault(a => a.Rect.Contains(c)) ?? Nearest(areas, c);
        }

        /// <summary>
        /// Belirli bir fiziksel veya sanal monitöre (SnapArea) ait Pencere Yerleşim Düzenlerini döndürür.
        /// Her fiziksel/sanal monitörün pencere düzeni listesi birbirinden bağımsızdır!
        /// </summary>
        public static List<SnapLayout> GetLayoutsForArea(SnapArea area)
        {
            if (area == null) return DefaultLayouts.Create();
            var dict = Storage.Data.AreaLayouts;
            if (!dict.TryGetValue(area.LayoutKey, out var list) || list == null)
            {
                list = DefaultLayouts.Create();
                dict[area.LayoutKey] = list;
                Storage.Save();
            }
            return list;
        }

        public static void ResetLayoutsForArea(SnapArea area)
        {
            if (area == null) return;
            Storage.Data.AreaLayouts[area.LayoutKey] = DefaultLayouts.Create();
            Storage.Save();
        }

        /// <summary>
        /// Seçili sanal monitörün pencere yerleşim düzenlerini aynı fiziksel monitördeki diğer sanal monitörlere kopyalar.
        /// </summary>
        public static void CopyLayoutsToSiblingVirtualMonitors(SnapArea sourceArea)
        {
            if (sourceArea == null || !sourceArea.IsVirtual) return;
            var srcList = GetLayoutsForArea(sourceArea);
            foreach (var sib in GetAreas(sourceArea.Monitor))
            {
                if (sib.SegmentIndex == sourceArea.SegmentIndex) continue;
                Storage.Data.AreaLayouts[sib.LayoutKey] = srcList.Select(l => l.Clone()).ToList();
            }
            Storage.Save();
        }
    }
}
