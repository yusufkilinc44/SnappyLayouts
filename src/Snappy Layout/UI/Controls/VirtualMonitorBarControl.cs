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
    /// Fiziksel monitörü gerçek en/boy oranında çizen ve içindeki yan yana sanal monitörleri
    /// fareyle sürüklenebilir kalın sınır çizgileriyle gösteren etkileşimli kontrol.
    /// </summary>
    public class VirtualMonitorBarControl : FrameworkElement
    {
        public double MonitorAspect { get; set; } = 16.0 / 9.0;
        public int PhysicalWidthPx { get; set; } = 1920;
        public int PhysicalHeightPx { get; set; } = 1080;
        public List<double> Weights { get; set; } = new List<double> { 1, 1 };
        public int SelectedIndex { get; set; } = 0;
        public bool IsMiniPreview { get; set; } = false;

        public event Action WeightsChanged;
        public event Action<int> SegmentSelected;

        private int _dragDivider = -1;
        private int _hoverDivider = -1;

        public VirtualMonitorBarControl()
        {
            ClipToBounds = true;
            ThemeManager.ThemeChanged += InvalidateVisual;
        }

        private Rect GetMonitorBox()
        {
            double pad = IsMiniPreview ? 4.0 : 20.0;
            double w = ActualWidth - pad;
            double h = ActualHeight - pad;
            if (w <= 6 || h <= 6) return new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
            double aspect = Math.Clamp(MonitorAspect <= 0 ? 16.0 / 9.0 : MonitorAspect, 0.5, 4.2);
            double bw = w;
            double bh = bw / aspect;
            if (bh > h)
            {
                bh = h;
                bw = bh * aspect;
            }
            return new Rect((ActualWidth - bw) / 2.0, (ActualHeight - bh) / 2.0, bw, bh);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (Weights == null || Weights.Count == 0) Weights = new List<double> { 1 };

            var box = GetMonitorBox();
            if (box.Width < 10 || box.Height < 10) return;

            var res = Application.Current?.Resources;
            var bg = (res?["InputBgBrush"] as Brush) ?? Brushes.Black;
            var border = (res?["CardBorderBrush"] as Brush) ?? Brushes.Gray;
            var zoneFill = (res?["ZoneFillBrush"] as Brush) ?? Brushes.DarkSlateGray;
            var selFill = (res?["AccentSubtleBrush"] as Brush) ?? Brushes.MidnightBlue;
            var textPrimary = (res?["TextPrimaryBrush"] as Brush) ?? Brushes.White;
            var textSecondary = (res?["TextSecondaryBrush"] as Brush) ?? Brushes.Silver;

            var lineColor = ThemeManager.GetBorderOverlayColor(Storage.Data?.Settings?.BorderColor ?? "Accent");
            var lineBrush = new SolidColorBrush(lineColor);

            var offs = SnapLayout.Offsets(Weights);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            if (IsMiniPreview)
            {
                dc.DrawRoundedRectangle(bg, new Pen(border, 1.4), box, 5, 5);
                for (int i = 0; i < Weights.Count; i++)
                {
                    double x0 = box.Left + offs[i] * box.Width;
                    double x1 = box.Left + offs[i + 1] * box.Width;
                    var segRect = new Rect(x0 + 2, box.Top + 2, Math.Max(2, x1 - x0 - 4), Math.Max(2, box.Height - 4));
                    dc.DrawRoundedRectangle(zoneFill, new Pen(border, 1.0), segRect, 3, 3);

                    double pct = Math.Round((offs[i + 1] - offs[i]) * 100.0);
                    string pctText = $"{pct:0}%";
                    var ftPct = new FormattedText(pctText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Semibold"), 9.5, textPrimary, dpi);
                    if (ftPct.Width <= segRect.Width - 2 && ftPct.Height <= segRect.Height - 2)
                    {
                        dc.DrawText(ftPct, new Point(
                            segRect.Left + (segRect.Width - ftPct.Width) / 2.0,
                            segRect.Top + (segRect.Height - ftPct.Height) / 2.0));
                    }
                }
                for (int i = 1; i < Weights.Count; i++)
                {
                    double x = box.Left + offs[i] * box.Width;
                    dc.DrawLine(new Pen(lineBrush, 2.2), new Point(x, box.Top + 2), new Point(x, box.Bottom - 2));
                }
                return;
            }

            // Fiziksel monitör dış gövdesi
            dc.DrawRoundedRectangle(bg, new Pen(border, 2), box, 8, 8);

            for (int i = 0; i < Weights.Count; i++)
            {
                double x0 = box.Left + offs[i] * box.Width;
                double x1 = box.Left + offs[i + 1] * box.Width;
                var segRect = new Rect(x0 + 3, box.Top + 3, Math.Max(4, x1 - x0 - 6), Math.Max(4, box.Height - 6));

                bool isSel = i == SelectedIndex;
                dc.DrawRoundedRectangle(isSel ? selFill : zoneFill,
                    new Pen(isSel ? lineBrush : border, isSel ? 2.0 : 1.0), segRect, 5, 5);

                double pct = (offs[i + 1] - offs[i]) * 100.0;
                int pxW = (int)Math.Round((offs[i + 1] - offs[i]) * PhysicalWidthPx);
                double segAspect = PhysicalHeightPx > 0 ? (double)pxW / PhysicalHeightPx : 1.0;

                string title = $"{Loc.T("VirtualMonitor")} #{i + 1}";
                string sub1 = $"{pct:0.0}%  ({pxW} × {PhysicalHeightPx} px)";
                string sub2 = $"{Loc.T("AspectRatio")}: {segAspect:0.00}:1";

                var ftTitle = new FormattedText(title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Bold"), 13, textPrimary, dpi);
                var ftSub1 = new FormattedText(sub1, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"), 11.5, lineBrush, dpi);
                var ftSub2 = new FormattedText(sub2, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 10.5, textSecondary, dpi);

                double cy = segRect.Top + segRect.Height / 2.0;
                if (ftTitle.Width < segRect.Width - 8)
                    dc.DrawText(ftTitle, new Point(segRect.Left + (segRect.Width - ftTitle.Width) / 2, cy - 26));
                if (ftSub1.Width < segRect.Width - 8)
                    dc.DrawText(ftSub1, new Point(segRect.Left + (segRect.Width - ftSub1.Width) / 2, cy - 4));
                if (ftSub2.Width < segRect.Width - 8)
                    dc.DrawText(ftSub2, new Point(segRect.Left + (segRect.Width - ftSub2.Width) / 2, cy + 15));
            }

            // Sanal monitör sınır çizgileri (kalın ve belirgin)
            int thick = Math.Clamp(Storage.Data?.Settings?.BorderThickness ?? 6, 3, 14);
            for (int i = 1; i < Weights.Count; i++)
            {
                double x = box.Left + offs[i] * box.Width;
                bool active = i == _dragDivider || i == _hoverDivider;
                var glowBrush = new SolidColorBrush(Color.FromArgb(active ? (byte)110 : (byte)60, lineColor.R, lineColor.G, lineColor.B));
                dc.DrawRectangle(glowBrush, null, new Rect(x - thick - 3, box.Top + 2, (thick + 3) * 2, box.Height - 4));
                dc.DrawLine(new Pen(lineBrush, thick), new Point(x, box.Top + 2), new Point(x, box.Bottom - 2));

                // Orta tutamaç
                var handleRect = new Rect(x - 7, box.Top + box.Height / 2 - 18, 14, 36);
                dc.DrawRoundedRectangle(lineBrush, new Pen(Brushes.White, 1.2), handleRect, 4, 4);
            }
        }

        private int HitDivider(Point p)
        {
            if (Weights == null || Weights.Count <= 1) return -1;
            var box = GetMonitorBox();
            if (!box.Contains(p)) return -1;
            var offs = SnapLayout.Offsets(Weights);
            for (int i = 1; i < Weights.Count; i++)
            {
                double x = box.Left + offs[i] * box.Width;
                if (Math.Abs(p.X - x) <= 10) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (IsMiniPreview) return;
            var p = e.GetPosition(this);
            var box = GetMonitorBox();
            if (_dragDivider > 0 && box.Width > 20)
            {
                var offs = SnapLayout.Offsets(Weights);
                double minX = offs[_dragDivider - 1] + 0.06;
                double maxX = offs[_dragDivider + 1] - 0.06;
                double nx = Math.Clamp((p.X - box.Left) / box.Width, minX, maxX);
                double total = Weights.Sum();
                double pairWeight = (offs[_dragDivider + 1] - offs[_dragDivider - 1]) * total;
                double leftFrac = (nx - offs[_dragDivider - 1]) / (offs[_dragDivider + 1] - offs[_dragDivider - 1]);
                Weights[_dragDivider - 1] = Math.Max(1, Math.Round(pairWeight * leftFrac, 2));
                Weights[_dragDivider] = Math.Max(1, Math.Round(pairWeight * (1.0 - leftFrac), 2));
                WeightsChanged?.Invoke();
                InvalidateVisual();
                return;
            }

            int hd = HitDivider(p);
            if (hd != _hoverDivider)
            {
                _hoverDivider = hd;
                Cursor = hd > 0 ? Cursors.SizeWE : Cursors.Hand;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (IsMiniPreview) return;
            var p = e.GetPosition(this);
            int hd = HitDivider(p);
            if (hd > 0)
            {
                _dragDivider = hd;
                CaptureMouse();
                e.Handled = true;
                return;
            }
            var box = GetMonitorBox();
            if (box.Contains(p) && Weights != null && Weights.Count > 0)
            {
                double rx = (p.X - box.Left) / box.Width;
                var offs = SnapLayout.Offsets(Weights);
                for (int i = 0; i < Weights.Count; i++)
                {
                    if (rx >= offs[i] && rx <= offs[i + 1])
                    {
                        SelectedIndex = i;
                        SegmentSelected?.Invoke(i);
                        InvalidateVisual();
                        break;
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragDivider > 0)
            {
                _dragDivider = -1;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }
    }
}
