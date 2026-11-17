using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SnappyLayout.Services;

namespace SnappyLayout.Models
{
    /// <summary>Izgaradaki bir bölge (bir veya birden fazla birleştirilmiş hücre).</summary>
    public class ZoneCell
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public int ColSpan { get; set; } = 1;
        public int RowSpan { get; set; } = 1;

        public ZoneCell() { }
        public ZoneCell(int col, int row, int colSpan = 1, int rowSpan = 1)
        { Col = col; Row = row; ColSpan = colSpan; RowSpan = rowSpan; }

        public bool ContainsCell(int c, int r) => c >= Col && c < Col + ColSpan && r >= Row && r < Row + RowSpan;
        public bool Intersects(int c0, int r0, int c1, int r1) =>
            Col <= c1 && Col + ColSpan - 1 >= c0 && Row <= r1 && Row + RowSpan - 1 >= r0;
        public ZoneCell Clone() => new ZoneCell(Col, Row, ColSpan, RowSpan);
    }

    /// <summary>
    /// Pencere yerleşim düzeni: sınırsız sayıda sütun/satır (oranlı genişlikler) + birleştirilebilir hücreler.
    /// Tüm koordinatlar 0..1 aralığında görecelidir; seçilen fiziksel ya da sanal monitörün boyutuna uyarlanır.
    /// </summary>
    public class SnapLayout
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string DefaultKey { get; set; }   // Windows varsayılanlarından geldiyse
        public List<double> Columns { get; set; } = new List<double> { 1 };
        public List<double> Rows { get; set; } = new List<double> { 1 };
        public List<ZoneCell> Zones { get; set; } = new List<ZoneCell>();
        public bool ShowOnPortrait { get; set; } = true;
        public bool ShowOnLandscape { get; set; } = true;
        public bool ShowOnUltrawide { get; set; } = true;

        public const double UltrawideAspect = 2.0;

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrEmpty(DefaultKey))
                {
                    string defName = DefaultLayouts.GetLocalizedDefaultName(DefaultKey);
                    // Kullanıcı özel bir isim vermediyse (veya yanlışlıkla "2 monitör" yazdıysa) yerel pencere düzeni adını göster
                    if (string.IsNullOrWhiteSpace(Name) || Name == "2 monitör" || DefaultLayouts.IsStandardCodeName(Name))
                        return defName;
                }
                return Name;
            }
        }

        public bool IsVisibleFor(double aspect)
        {
            if (aspect < 1.0) return ShowOnPortrait;
            if (aspect >= UltrawideAspect) return ShowOnUltrawide;
            return ShowOnLandscape;
        }

        public static double[] Offsets(IList<double> weights)
        {
            double total = weights.Sum();
            if (total <= 0) total = 1;
            var res = new double[weights.Count + 1];
            double acc = 0;
            for (int i = 0; i < weights.Count; i++) { res[i] = acc / total; acc += weights[i]; }
            res[weights.Count] = 1.0;
            return res;
        }

        /// <summary>Bölgenin göreli dikdörtgeni (0..1).</summary>
        public Rect GetZoneRect(ZoneCell z)
        {
            var cx = Offsets(Columns);
            var ry = Offsets(Rows);
            int c1 = Math.Min(z.Col + z.ColSpan, Columns.Count);
            int r1 = Math.Min(z.Row + z.RowSpan, Rows.Count);
            return new Rect(cx[z.Col], ry[z.Row], cx[c1] - cx[z.Col], ry[r1] - ry[z.Row]);
        }

        public IEnumerable<Rect> GetZoneRects() => Zones.Select(GetZoneRect);

        public void ResetZonesToCells()
        {
            Zones.Clear();
            for (int r = 0; r < Rows.Count; r++)
                for (int c = 0; c < Columns.Count; c++)
                    Zones.Add(new ZoneCell(c, r));
        }

        /// <summary>Boşta kalan hücreleri tek hücrelik bölgelerle doldurur, taşan bölgeleri kırpar.</summary>
        public void Normalize()
        {
            if (Columns.Count == 0) Columns.Add(1);
            if (Rows.Count == 0) Rows.Add(1);
            for (int i = 0; i < Columns.Count; i++) if (Columns[i] <= 0.0001) Columns[i] = 0.01;
            for (int i = 0; i < Rows.Count; i++) if (Rows[i] <= 0.0001) Rows[i] = 0.01;

            var kept = new List<ZoneCell>();
            foreach (var z in Zones)
            {
                if (z.Col >= Columns.Count || z.Row >= Rows.Count) continue;
                z.ColSpan = Math.Max(1, Math.Min(z.ColSpan, Columns.Count - z.Col));
                z.RowSpan = Math.Max(1, Math.Min(z.RowSpan, Rows.Count - z.Row));
                bool overlap = kept.Any(k => k.Intersects(z.Col, z.Row, z.Col + z.ColSpan - 1, z.Row + z.RowSpan - 1));
                if (!overlap) kept.Add(z);
            }
            for (int r = 0; r < Rows.Count; r++)
                for (int c = 0; c < Columns.Count; c++)
                    if (!kept.Any(k => k.ContainsCell(c, r))) kept.Add(new ZoneCell(c, r));
            Zones = kept.OrderBy(z => z.Col).ThenBy(z => z.Row).ToList();
        }

        /// <summary>Seçili hücre dikdörtgenini tek bölge yapar.</summary>
        public void Merge(int c0, int r0, int c1, int r1)
        {
            if (c0 > c1) (c0, c1) = (c1, c0);
            if (r0 > r1) (r0, r1) = (r1, r0);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var z in Zones.Where(z => z.Intersects(c0, r0, c1, r1)).ToList())
                {
                    int zc1 = z.Col + z.ColSpan - 1, zr1 = z.Row + z.RowSpan - 1;
                    if (z.Col < c0) { c0 = z.Col; changed = true; }
                    if (z.Row < r0) { r0 = z.Row; changed = true; }
                    if (zc1 > c1) { c1 = zc1; changed = true; }
                    if (zr1 > r1) { r1 = zr1; changed = true; }
                }
            }
            Zones.RemoveAll(z => z.Intersects(c0, r0, c1, r1));
            Zones.Add(new ZoneCell(c0, r0, c1 - c0 + 1, r1 - r0 + 1));
            Normalize();
        }

        public void Split(ZoneCell zone)
        {
            Zones.Remove(zone);
            for (int r = zone.Row; r < zone.Row + zone.RowSpan; r++)
                for (int c = zone.Col; c < zone.Col + zone.ColSpan; c++)
                    Zones.Add(new ZoneCell(c, r));
            Normalize();
        }

        public void AddColumn()
        {
            double avg = Columns.Average();
            Columns.Add(avg);
            Normalize();
        }

        public void RemoveColumn()
        {
            if (Columns.Count <= 1) return;
            Columns.RemoveAt(Columns.Count - 1);
            Normalize();
        }

        public void AddRow()
        {
            double avg = Rows.Average();
            Rows.Add(avg);
            Normalize();
        }

        public void RemoveRow()
        {
            if (Rows.Count <= 1) return;
            Rows.RemoveAt(Rows.Count - 1);
            Normalize();
        }

        public SnapLayout Clone()
        {
            return new SnapLayout
            {
                Id = Guid.NewGuid(),
                Name = Name,
                DefaultKey = DefaultKey,
                Columns = Columns.ToList(),
                Rows = Rows.ToList(),
                Zones = Zones.Select(z => z.Clone()).ToList(),
                ShowOnPortrait = ShowOnPortrait,
                ShowOnLandscape = ShowOnLandscape,
                ShowOnUltrawide = ShowOnUltrawide
            };
        }

        public static SnapLayout Grid(string name, double[] cols, double[] rows)
        {
            var l = new SnapLayout { Name = name, Columns = cols.ToList(), Rows = rows.ToList() };
            l.ResetZonesToCells();
            return l;
        }
    }

    /// <summary>Windows 11 Pencere Yerleşim Düzenlerinin (Snap Layouts) varsayılan şablonları.</summary>
    public static class DefaultLayouts
    {
        private static readonly HashSet<string> StandardCodeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "50 / 50", "66 / 33", "33 / 33 / 33", "50 / 25+25", "2 × 2", "25 / 50 / 25", "25 × 4", "50 / 50 ↕", "33 / 33 / 33 ↕"
        };

        public static bool IsStandardCodeName(string name) => StandardCodeNames.Contains(name?.Trim() ?? "");

        public static string GetLocalizedDefaultName(string defaultKey)
        {
            bool tr = Loc.IsTurkish;
            return defaultKey switch
            {
                "halves" => tr ? "2 Pencere: Sol / Sağ (50% / 50%)" : "2 Windows: Left / Right (50% / 50%)",
                "twothirds" => tr ? "2 Pencere: Geniş / Dar (66% / 33%)" : "2 Windows: Wide / Narrow (66% / 33%)",
                "thirds" => tr ? "3 Pencere: Üç Sütun (33% / 33% / 33%)" : "3 Windows: Three Columns (33% / 33% / 33%)",
                "halfstack" => tr ? "3 Pencere: Sol Tam + Sağ Üst/Alt" : "3 Windows: Left Half + Right Stack",
                "quad" => tr ? "4 Pencere: 2×2 Izgara" : "4 Windows: 2×2 Grid",
                "center" => tr ? "3 Pencere: Orta Odak (25% / 50% / 25%)" : "3 Windows: Center Focus (25% / 50% / 25%)",
                "fourcols" => tr ? "4 Pencere: Dört Sütun (25% × 4)" : "4 Windows: Four Columns (25% × 4)",
                "topbottom" => tr ? "2 Pencere Dikey: Üst / Alt (50% / 50%)" : "2 Windows Vertical: Top / Bottom",
                "threerows" => tr ? "3 Pencere Dikey: Üst / Orta / Alt" : "3 Windows Vertical: Three Rows",
                _ => defaultKey
            };
        }

        public static List<SnapLayout> Create()
        {
            var list = new List<SnapLayout>();

            var halves = SnapLayout.Grid(GetLocalizedDefaultName("halves"), new double[] { 1, 1 }, new double[] { 1 });
            halves.DefaultKey = "halves"; halves.ShowOnPortrait = false;
            list.Add(halves);

            var twoThirds = SnapLayout.Grid(GetLocalizedDefaultName("twothirds"), new double[] { 2, 1 }, new double[] { 1 });
            twoThirds.DefaultKey = "twothirds"; twoThirds.ShowOnPortrait = false;
            list.Add(twoThirds);

            var thirds = SnapLayout.Grid(GetLocalizedDefaultName("thirds"), new double[] { 1, 1, 1 }, new double[] { 1 });
            thirds.DefaultKey = "thirds"; thirds.ShowOnPortrait = false;
            list.Add(thirds);

            var leftStack = SnapLayout.Grid(GetLocalizedDefaultName("halfstack"), new double[] { 1, 1 }, new double[] { 1, 1 });
            leftStack.Zones = new List<ZoneCell> { new ZoneCell(0, 0, 1, 2), new ZoneCell(1, 0), new ZoneCell(1, 1) };
            leftStack.DefaultKey = "halfstack"; leftStack.ShowOnPortrait = false;
            list.Add(leftStack);

            var quad = SnapLayout.Grid(GetLocalizedDefaultName("quad"), new double[] { 1, 1 }, new double[] { 1, 1 });
            quad.DefaultKey = "quad";
            list.Add(quad);

            var centered = SnapLayout.Grid(GetLocalizedDefaultName("center"), new double[] { 1, 2, 1 }, new double[] { 1 });
            centered.DefaultKey = "center"; centered.ShowOnPortrait = false; centered.ShowOnLandscape = false;
            list.Add(centered);

            var fourCols = SnapLayout.Grid(GetLocalizedDefaultName("fourcols"), new double[] { 1, 1, 1, 1 }, new double[] { 1 });
            fourCols.DefaultKey = "fourcols"; fourCols.ShowOnPortrait = false; fourCols.ShowOnLandscape = false;
            list.Add(fourCols);

            var topBottom = SnapLayout.Grid(GetLocalizedDefaultName("topbottom"), new double[] { 1 }, new double[] { 1, 1 });
            topBottom.DefaultKey = "topbottom"; topBottom.ShowOnLandscape = false; topBottom.ShowOnUltrawide = false;
            list.Add(topBottom);

            var threeRows = SnapLayout.Grid(GetLocalizedDefaultName("threerows"), new double[] { 1 }, new double[] { 1, 1, 1 });
            threeRows.DefaultKey = "threerows"; threeRows.ShowOnLandscape = false; threeRows.ShowOnUltrawide = false;
            list.Add(threeRows);

            return list;
        }
    }
}
