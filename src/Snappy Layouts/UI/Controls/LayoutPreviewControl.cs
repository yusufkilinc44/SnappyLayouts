using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SnappyLayout.Models;
using SnappyLayout.Services;

namespace SnappyLayout.UI.Controls
{
    /// <summary>
    /// Bir yerleşim düzenini (SnapLayout) hedef monitörün (veya sanal monitörün) gerçek en/boy oranına
    /// sadık kalarak çizen kontrol. Hem küçük önizleme/flyout kutusu hem de etkileşimli ızgara düzenleyicisi olarak çalışır.
    /// </summary>
    public class LayoutPreviewControl : FrameworkElement
    {
        public static readonly DependencyProperty LayoutProperty =
            DependencyProperty.Register(nameof(Layout), typeof(SnapLayout), typeof(LayoutPreviewControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MonitorAspectProperty =
            DependencyProperty.Register(nameof(MonitorAspect), typeof(double), typeof(LayoutPreviewControl),
                new FrameworkPropertyMetadata(16.0 / 9.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty IsEditorProperty =
            DependencyProperty.Register(nameof(IsEditor), typeof(bool), typeof(LayoutPreviewControl),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsPermanentFullProperty =
            DependencyProperty.Register(nameof(IsPermanentFull), typeof(bool), typeof(LayoutPreviewControl),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsWin11StyleProperty =
            DependencyProperty.Register(nameof(IsWin11Style), typeof(bool), typeof(LayoutPreviewControl),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public SnapLayout Layout
        {
            get => (SnapLayout)GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        public double MonitorAspect
        {
            get => (double)GetValue(MonitorAspectProperty);
            set => SetValue(MonitorAspectProperty, value);
        }

        public bool IsEditor
        {
            get => (bool)GetValue(IsEditorProperty);
            set => SetValue(IsEditorProperty, value);
        }

        public bool IsPermanentFull
        {
            get => (bool)GetValue(IsPermanentFullProperty);
            set => SetValue(IsPermanentFullProperty, value);
        }

        public bool IsWin11Style
        {
            get => (bool)GetValue(IsWin11StyleProperty);
            set => SetValue(IsWin11StyleProperty, value);
        }

        /// <summary>
        /// Windows 11 orijinal Snap Layouts görünümünde bölge kutularının içine çizilecek uygulama ikonları (zoneIndex -> ImageSource).
        /// </summary>
        public Dictionary<int, ImageSource> ZoneIcons { get; set; }

        /// <summary>
        /// Tek bölgeli kutularda (ör. Sanal Monitör Tam Ekran) harici olarak seçili/vurgulu durumu açıp kapatır.
        /// </summary>
        public void SetForcedSelected(bool selected)
        {
            int hz = selected ? 0 : -1;
            if (hz != _hoverZoneIndex)
            {
                _hoverZoneIndex = hz;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Pencere sürüklenirken (mouse capture başka penceredeyken) dışarıdan fare konumunu bildirip
        /// ilgili bölgeyi vurgular ve hedef dikdörtgeni döndürür.
        /// </summary>
        public Rect? SetExternalHoverPoint(Point? localPt)
        {
            int hz = localPt.HasValue ? HitZoneIndex(localPt.Value) : -1;
            if (hz != _hoverZoneIndex)
            {
                _hoverZoneIndex = hz;
                InvalidateVisual();
            }
            if (hz < 0) return null;
            if (IsPermanentFull || Layout == null) return new Rect(0, 0, 1, 1);
            if (hz < Layout.Zones.Count) return Layout.GetZoneRect(Layout.Zones[hz]);
            return null;
        }

        public event Action<Rect, ZoneCell> ZoneClicked;
        public event Action<Rect?> ZoneHovered;
        public event Action LayoutEdited;
        public event Action SelectionChanged;

        private int _hoverZoneIndex = -1;
        private int _dragColDivider = -1;
        private int _dragRowDivider = -1;
        private bool _isSelectingCells;
        private (int c, int r)? _selStart;
        private (int c, int r)? _selEnd;

        public (int c0, int r0, int c1, int r1)? SelectedCellRange
        {
            get
            {
                if (_selStart == null || _selEnd == null) return null;
                int c0 = Math.Min(_selStart.Value.c, _selEnd.Value.c);
                int r0 = Math.Min(_selStart.Value.r, _selEnd.Value.r);
                int c1 = Math.Max(_selStart.Value.c, _selEnd.Value.c);
                int r1 = Math.Max(_selStart.Value.r, _selEnd.Value.r);
                return (c0, r0, c1, r1);
            }
        }

        public LayoutPreviewControl()
        {
            ClipToBounds = true;
            Cursor = Cursors.Hand;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            ThemeManager.ThemeChanged += InvalidateVisual;
            ThemeManager.WindowsThemeChanged += InvalidateVisual;
        }

        public void ClearSelection()
        {
            _selStart = _selEnd = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
        }

        private Rect GetScreenBox()
        {
            double margin = IsWin11Style ? 2.0 : 8.0;
            double w = ActualWidth - margin;
            double h = ActualHeight - margin;
            if (w <= 4 || h <= 4) return new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));

            double aspect = Math.Clamp(MonitorAspect <= 0 ? 16.0 / 9.0 : MonitorAspect, 0.35, 4.5);
            double boxW = w;
            double boxH = boxW / aspect;
            if (boxH > h)
            {
                boxH = h;
                boxW = boxH * aspect;
            }
            double x = (ActualWidth - boxW) / 2.0;
            double y = (ActualHeight - boxH) / 2.0;
            return new Rect(x, y, boxW, boxH);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

            var screen = GetScreenBox();
            if (screen.Width < 6 || screen.Height < 6) return;

            var frameBg = (TryFindResource("InputBgBrush") as Brush) ?? Brushes.Black;
            var frameStroke = (TryFindResource("CardBorderBrush") as Brush) ?? Brushes.Gray;
            var zoneFill = (TryFindResource(IsWin11Style ? "Win11ZoneFillBrush" : "ZoneFillBrush") as Brush) ?? Brushes.DarkSlateGray;
            var iconZoneBg = (TryFindResource("Win11IconZoneBgBrush") as Brush) ?? zoneFill;
            var zoneHover = (TryFindResource(IsWin11Style ? "Win11ZoneHoverBrush" : "ZoneHoverBrush") as Brush) ?? Brushes.DodgerBlue;
            var zoneStroke = (TryFindResource(IsWin11Style ? "Win11ZoneStrokeBrush" : "ZoneStrokeBrush") as Brush) ?? Brushes.LightSlateGray;
            var zoneHoverStroke = (TryFindResource(IsWin11Style ? "Win11ZoneHoverStrokeBrush" : "ZoneHoverBrush") as Brush) ?? Brushes.White;
            var permFill = (TryFindResource(IsWin11Style ? "Win11PermFillBrush" : "PermanentSubtleBrush") as Brush) ?? Brushes.DarkGreen;
            var permStroke = (TryFindResource(IsWin11Style ? "Win11PermStrokeBrush" : "PermanentBrush") as Brush) ?? Brushes.MediumSeaGreen;
            var permHover = (TryFindResource(IsWin11Style ? "Win11PermHoverBrush" : "PermanentBrush") as Brush) ?? Brushes.MediumSeaGreen;
            var textBrush = (TryFindResource("TextPrimaryBrush") as Brush) ?? Brushes.White;
            var mutedBrush = (TryFindResource("TextSecondaryBrush") as Brush) ?? Brushes.Silver;

            // Dış monitör çerçevesi (Windows 11 stilinde dış çerçeve çizilmez, doğrudan kutular görünür)
            if (!IsWin11Style)
            {
                dc.DrawRoundedRectangle(frameBg, new Pen(frameStroke, 1.5), screen, 6, 6);
            }

            double pad = IsWin11Style ? 1.0 : (IsEditor ? 8.0 : 4.0);
            double gap = IsWin11Style ? 3.5 : (IsEditor ? 5.0 : 3.0);
            double radius = IsWin11Style ? 4.0 : (IsEditor ? 5.0 : 3.0);
            var inner = new Rect(screen.Left + pad, screen.Top + pad,
                Math.Max(2, screen.Width - pad * 2), Math.Max(2, screen.Height - pad * 2));

            if (IsPermanentFull || Layout == null)
            {
                bool hov = _hoverZoneIndex == 0;
                ImageSource singleIcon = null;
                bool hasSingleIcon = !IsPermanentFull && !IsEditor && ZoneIcons != null && ZoneIcons.TryGetValue(0, out singleIcon) && singleIcon != null;

                // Win11 TopSnapBar stilinde Fiziksel Tam Ekran kutusu seçili değilken nötr görünür,
                // yalnızca üzerine gelindiğinde yeşil olarak vurgulanır (seçiliymiş gibi kafa karışıklığı yaratmaz).
                var fill = IsPermanentFull
                    ? (hov ? permHover : (IsWin11Style ? zoneFill : permFill))
                    : (hov ? zoneHover : (hasSingleIcon && IsWin11Style ? iconZoneBg : zoneFill));
                var stroke = IsPermanentFull
                    ? (hov ? Brushes.White : (IsWin11Style ? zoneStroke : permStroke))
                    : (hov ? zoneHoverStroke : zoneStroke);
                dc.DrawRoundedRectangle(fill, new Pen(stroke, IsWin11Style ? 1.15 : 1.5), inner, radius, radius);

                if (hasSingleIcon && inner.Width >= 16 && inner.Height >= 16)
                {
                    double sz = Math.Clamp(Math.Min(inner.Width, inner.Height) * 0.44, 14, 24);
                    dc.DrawImage(singleIcon, new Rect(inner.Left + (inner.Width - sz) / 2.0, inner.Top + (inner.Height - sz) / 2.0, sz, sz));
                }
                else if (!IsWin11Style && inner.Width > 50 && inner.Height > 24)
                {
                    var ft = new FormattedText("100%", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Semibold"), IsEditor ? 13 : 10, hov ? Brushes.White : textBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(ft, new Point(inner.Left + (inner.Width - ft.Width) / 2, inner.Top + (inner.Height - ft.Height) / 2));
                }
                return;
            }

            var zones = Layout.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                var rel = Layout.GetZoneRect(z);
                var zr = new Rect(
                    inner.Left + rel.Left * inner.Width + gap / 2,
                    inner.Top + rel.Top * inner.Height + gap / 2,
                    Math.Max(2, rel.Width * inner.Width - gap),
                    Math.Max(2, rel.Height * inner.Height - gap));

                bool hov = _hoverZoneIndex == i;
                bool sel = false;
                var selRange = SelectedCellRange;
                if (selRange != null && z.Intersects(selRange.Value.c0, selRange.Value.r0, selRange.Value.c1, selRange.Value.r1))
                    sel = true;

                ImageSource zoneIcon = null;
                bool hasIcon = !IsEditor && ZoneIcons != null && ZoneIcons.TryGetValue(i, out zoneIcon) && zoneIcon != null;

                Brush fill = (hov || sel) ? zoneHover : (hasIcon && IsWin11Style ? iconZoneBg : zoneFill);
                Pen pen = new Pen((hov || sel) ? (IsWin11Style ? zoneHoverStroke : Brushes.White) : zoneStroke, IsEditor ? 1.5 : 1.1);
                dc.DrawRoundedRectangle(fill, pen, zr, radius, radius);

                if (hasIcon && zr.Width >= 14 && zr.Height >= 14)
                {
                    double sz = Math.Clamp(Math.Min(zr.Width, zr.Height) * 0.56, 12, 22);
                    dc.DrawImage(zoneIcon, new Rect(zr.Left + (zr.Width - sz) / 2.0, zr.Top + (zr.Height - sz) / 2.0, sz, sz));
                }
                else if (IsEditor && zr.Width > 42 && zr.Height > 22)
                {
                    string label = $"{Math.Round(rel.Width * 100)}% × {Math.Round(rel.Height * 100)}%";
                    var ft = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Semibold"), 11, (hov || sel) ? Brushes.White : textBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    if (ft.Width <= zr.Width - 6)
                        dc.DrawText(ft, new Point(zr.Left + (zr.Width - ft.Width) / 2, zr.Top + (zr.Height - ft.Height) / 2));
                }
            }

            // Düzenleyici modunda sürüklenebilir sütun/satır ayırıcı tutamaçlarını göster
            if (IsEditor && Layout != null)
            {
                var cx = SnapLayout.Offsets(Layout.Columns);
                var ry = SnapLayout.Offsets(Layout.Rows);
                var dashPen = new Pen(mutedBrush, 1) { DashStyle = DashStyles.Dot };
                for (int c = 1; c < Layout.Columns.Count; c++)
                {
                    double x = inner.Left + cx[c] * inner.Width;
                    dc.DrawLine(dashPen, new Point(x, inner.Top), new Point(x, inner.Bottom));
                }
                for (int r = 1; r < Layout.Rows.Count; r++)
                {
                    double y = inner.Top + ry[r] * inner.Height;
                    dc.DrawLine(dashPen, new Point(inner.Left, y), new Point(inner.Right, y));
                }
            }
        }

        private Rect GetInnerRect()
        {
            var screen = GetScreenBox();
            double pad = IsWin11Style ? 1.0 : (IsEditor ? 8.0 : 4.0);
            return new Rect(screen.Left + pad, screen.Top + pad,
                Math.Max(2, screen.Width - pad * 2), Math.Max(2, screen.Height - pad * 2));
        }

        private (int colDiv, int rowDiv) HitDivider(Point p)
        {
            if (!IsEditor || Layout == null) return (-1, -1);
            var inner = GetInnerRect();
            if (!inner.Contains(p)) return (-1, -1);
            var cx = SnapLayout.Offsets(Layout.Columns);
            var ry = SnapLayout.Offsets(Layout.Rows);
            for (int c = 1; c < Layout.Columns.Count; c++)
            {
                double x = inner.Left + cx[c] * inner.Width;
                if (Math.Abs(p.X - x) <= 6) return (c, -1);
            }
            for (int r = 1; r < Layout.Rows.Count; r++)
            {
                double y = inner.Top + ry[r] * inner.Height;
                if (Math.Abs(p.Y - y) <= 6) return (-1, r);
            }
            return (-1, -1);
        }

        private (int c, int r)? PointToCell(Point p)
        {
            if (Layout == null) return null;
            var inner = GetInnerRect();
            if (!inner.Contains(p)) return null;
            double rx = Math.Clamp((p.X - inner.Left) / inner.Width, 0, 0.9999);
            double ry = Math.Clamp((p.Y - inner.Top) / inner.Height, 0, 0.9999);
            var cx = SnapLayout.Offsets(Layout.Columns);
            var cy = SnapLayout.Offsets(Layout.Rows);
            int col = 0, row = 0;
            for (int i = 0; i < Layout.Columns.Count; i++) if (rx >= cx[i] && rx < cx[i + 1]) { col = i; break; }
            for (int i = 0; i < Layout.Rows.Count; i++) if (ry >= cy[i] && ry < cy[i + 1]) { row = i; break; }
            return (col, row);
        }

        private int HitZoneIndex(Point p)
        {
            var inner = GetInnerRect();
            if (!inner.Contains(p)) return -1;
            if (IsPermanentFull || Layout == null) return 0;
            for (int i = 0; i < Layout.Zones.Count; i++)
            {
                var rel = Layout.GetZoneRect(Layout.Zones[i]);
                var zr = new Rect(inner.Left + rel.Left * inner.Width, inner.Top + rel.Top * inner.Height,
                    rel.Width * inner.Width, rel.Height * inner.Height);
                if (zr.Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var p = e.GetPosition(this);
            var inner = GetInnerRect();

            if (IsEditor && Layout != null)
            {
                if (_dragColDivider > 0 && inner.Width > 10)
                {
                    var cx = SnapLayout.Offsets(Layout.Columns);
                    double minX = cx[_dragColDivider - 1] + 0.05;
                    double maxX = cx[_dragColDivider + 1] - 0.05;
                    double newX = Math.Clamp((p.X - inner.Left) / inner.Width, minX, maxX);
                    double totalWeight = Layout.Columns.Sum();
                    double pairShare = (cx[_dragColDivider + 1] - cx[_dragColDivider - 1]) * totalWeight;
                    double leftFrac = (newX - cx[_dragColDivider - 1]) / (cx[_dragColDivider + 1] - cx[_dragColDivider - 1]);
                    Layout.Columns[_dragColDivider - 1] = Math.Max(0.05, pairShare * leftFrac);
                    Layout.Columns[_dragColDivider] = Math.Max(0.05, pairShare * (1.0 - leftFrac));
                    LayoutEdited?.Invoke();
                    InvalidateVisual();
                    return;
                }
                if (_dragRowDivider > 0 && inner.Height > 10)
                {
                    var ry = SnapLayout.Offsets(Layout.Rows);
                    double minY = ry[_dragRowDivider - 1] + 0.05;
                    double maxY = ry[_dragRowDivider + 1] - 0.05;
                    double newY = Math.Clamp((p.Y - inner.Top) / inner.Height, minY, maxY);
                    double totalWeight = Layout.Rows.Sum();
                    double pairShare = (ry[_dragRowDivider + 1] - ry[_dragRowDivider - 1]) * totalWeight;
                    double topFrac = (newY - ry[_dragRowDivider - 1]) / (ry[_dragRowDivider + 1] - ry[_dragRowDivider - 1]);
                    Layout.Rows[_dragRowDivider - 1] = Math.Max(0.05, pairShare * topFrac);
                    Layout.Rows[_dragRowDivider] = Math.Max(0.05, pairShare * (1.0 - topFrac));
                    LayoutEdited?.Invoke();
                    InvalidateVisual();
                    return;
                }
                if (_isSelectingCells)
                {
                    var cell = PointToCell(p);
                    if (cell != null)
                    {
                        _selEnd = cell;
                        SelectionChanged?.Invoke();
                        InvalidateVisual();
                    }
                    return;
                }

                var (cd, rd) = HitDivider(p);
                if (cd > 0) Cursor = Cursors.SizeWE;
                else if (rd > 0) Cursor = Cursors.SizeNS;
                else Cursor = Cursors.Hand;
            }

            int hz = HitZoneIndex(p);
            if (hz != _hoverZoneIndex)
            {
                _hoverZoneIndex = hz;
                if (!IsEditor)
                {
                    if (hz < 0) ZoneHovered?.Invoke(null);
                    else if (IsPermanentFull || Layout == null) ZoneHovered?.Invoke(new Rect(0, 0, 1, 1));
                    else if (hz < Layout.Zones.Count) ZoneHovered?.Invoke(Layout.GetZoneRect(Layout.Zones[hz]));
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverZoneIndex != -1)
            {
                _hoverZoneIndex = -1;
                if (!IsEditor) ZoneHovered?.Invoke(null);
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var p = e.GetPosition(this);
            if (IsEditor && Layout != null)
            {
                var (cd, rd) = HitDivider(p);
                if (cd > 0 || rd > 0)
                {
                    _dragColDivider = cd;
                    _dragRowDivider = rd;
                    CaptureMouse();
                    e.Handled = true;
                    return;
                }
                var cell = PointToCell(p);
                if (cell != null)
                {
                    _selStart = _selEnd = cell;
                    _isSelectingCells = true;
                    CaptureMouse();
                    SelectionChanged?.Invoke();
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                int hz = HitZoneIndex(p);
                if (hz >= 0)
                {
                    if (IsPermanentFull || Layout == null)
                    {
                        ZoneClicked?.Invoke(new Rect(0, 0, 1, 1), new ZoneCell(0, 0, 1, 1));
                    }
                    else if (hz < Layout.Zones.Count)
                    {
                        var z = Layout.Zones[hz];
                        ZoneClicked?.Invoke(Layout.GetZoneRect(z), z);
                    }
                    e.Handled = true;
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragColDivider > 0 || _dragRowDivider > 0 || _isSelectingCells)
            {
                _dragColDivider = -1;
                _dragRowDivider = -1;
                _isSelectingCells = false;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (IsEditor && Layout != null)
            {
                int hz = HitZoneIndex(e.GetPosition(this));
                if (hz >= 0 && hz < Layout.Zones.Count)
                {
                    var z = Layout.Zones[hz];
                    if (z.ColSpan > 1 || z.RowSpan > 1)
                    {
                        Layout.Split(z);
                        ClearSelection();
                        LayoutEdited?.Invoke();
                        InvalidateVisual();
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
