using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SnappyLayout.Core;
using SnappyLayout.Models;
using SnappyLayout.Native;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.UI.Windows
{
    /// <summary>
    /// Sanal monitörlerin sınırlarını ekranda farklı ve kalın çizgilerle gösteren
    /// tıklama-geçirgen (click-through) üst katman pencerelerini yönetir.
    /// </summary>
    public sealed class VirtualBorderOverlayManager : IDisposable
    {
        private readonly List<MonitorBorderOverlayWindow> _windows = new List<MonitorBorderOverlayWindow>();
        private readonly DispatcherTimer _flashTimer;
        private bool _isDraggingWindow;
        private bool _isFlashing;
        private bool _manualToggle;

        public VirtualBorderOverlayManager()
        {
            _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _flashTimer.Tick += (s, e) =>
            {
                _flashTimer.Stop();
                _isFlashing = false;
                UpdateVisibility();
            };
        }

        public void SetDragging(bool dragging)
        {
            var s = Storage.Data?.Settings;
            _isDraggingWindow = dragging && (s?.ShowBordersWhileDragging ?? true);
            // Sürükleme başlarken bağlı monitör listesini tazele (çıkarılmış monitörün eski çizgileri asla görünmesin)
            UpdateVisibility(recreate: _isDraggingWindow);
        }

        public void ToggleManual()
        {
            _manualToggle = !_manualToggle;
            if (_manualToggle) Flash(seconds: 6);
            else
            {
                _isFlashing = false;
                UpdateVisibility();
            }
        }

        public void Flash(int seconds = -1)
        {
            int sec = seconds > 0 ? seconds : Math.Max(2, Storage.Data?.Settings?.BorderFlashSeconds ?? 3);
            _isFlashing = true;
            _flashTimer.Stop();
            _flashTimer.Interval = TimeSpan.FromSeconds(sec);
            _flashTimer.Start();
            Refresh();
        }

        public void Refresh()
        {
            UpdateVisibility(recreate: true);
        }

        public void UpdateVisibility(bool recreate = false)
        {
            if (recreate) CloseAll();

            var s = Storage.Data?.Settings;
            if (s == null || !s.VirtualMonitorsEnabled || (App.Instance?.Paused ?? false))
            {
                CloseAll();
                return;
            }

            bool shouldShow = s.AlwaysShowBorders || _isDraggingWindow || _isFlashing || _manualToggle;
            if (!shouldShow)
            {
                CloseAll();
                return;
            }

            if (recreate || _windows.Count == 0)
            {
                CloseAll();
                foreach (var m in MonitorService.GetMonitors(refresh: true))
                {
                    var weights = AreaService.GetWeights(m);
                    if (weights == null || weights.Count <= 1) continue;
                    var ow = new MonitorBorderOverlayWindow(m, weights, showBadges: _isFlashing || _isDraggingWindow || _manualToggle);
                    _windows.Add(ow);
                    ow.Show();
                    ow.PlaceOnMonitor();
                }
            }
            else
            {
                foreach (var w in _windows)
                {
                    w.ShowBadges = _isFlashing || _isDraggingWindow || _manualToggle;
                    w.InvalidateVisual();
                    if (!w.IsVisible) w.Show();
                    w.PlaceOnMonitor();
                }
            }
        }

        private void CloseAll()
        {
            foreach (var w in _windows)
            {
                try { w.Close(); } catch { }
            }
            _windows.Clear();
        }

        public void Dispose()
        {
            _flashTimer.Stop();
            CloseAll();
        }
    }

    internal sealed class MonitorBorderOverlayWindow : Window
    {
        private readonly MonitorInfo _monitor;
        private readonly IList<double> _weights;
        public bool ShowBadges { get; set; }

        public MonitorBorderOverlayWindow(MonitorInfo monitor, IList<double> weights, bool showBadges)
        {
            _monitor = monitor;
            _weights = weights;
            ShowBadges = showBadges;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            AddExStyle(hwnd, WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST);
            PlaceOnMonitor();
        }

        public void PlaceOnMonitor()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var r = _monitor.WorkArea;
            SetWindowPos(hwnd, HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (_weights == null || _weights.Count <= 1) return;

            var s = Storage.Data?.Settings;
            var baseColor = ThemeManager.GetBorderOverlayColor(s?.BorderColor ?? "Accent");
            int thick = Math.Clamp(s?.BorderThickness ?? 6, 2, 18);

            var solidBrush = new SolidColorBrush(baseColor);
            var glowBrush = new SolidColorBrush(Color.FromArgb(85, baseColor.R, baseColor.G, baseColor.B));
            var framePen = new Pen(new SolidColorBrush(Color.FromArgb(170, baseColor.R, baseColor.G, baseColor.B)), Math.Max(2, thick / 2.0));
            var mainPen = new Pen(solidBrush, thick);
            var innerDashPen = new Pen(Brushes.White, 1.5) { DashStyle = new DashStyle(new double[] { 5, 5 }, 0) };

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // Dış çerçeve
            dc.DrawRectangle( null, framePen, new Rect(1, 1, w - 2, h - 2));

            var offs = SnapLayout.Offsets(_weights);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            for (int i = 1; i < _weights.Count; i++)
            {
                double x = offs[i] * w;
                dc.DrawRectangle(glowBrush, null, new Rect(x - thick - 4, 0, (thick + 4) * 2, h));
                dc.DrawLine(mainPen, new Point(x, 0), new Point(x, h));
                dc.DrawLine(innerDashPen, new Point(x, 0), new Point(x, h));
            }

            if (ShowBadges)
            {
                var badgeBg = new SolidColorBrush(Color.FromArgb(225, 15, 23, 42));
                var badgePen = new Pen(solidBrush, 2);
                for (int i = 0; i < _weights.Count; i++)
                {
                    double x0 = offs[i] * w;
                    double x1 = offs[i + 1] * w;
                    double segW = x1 - x0;
                    double pct = (offs[i + 1] - offs[i]) * 100.0;
                    int pxW = (int)Math.Round((offs[i + 1] - offs[i]) * _monitor.WorkArea.Width);

                    string label = $"{Loc.T("VirtualMonitor")} #{i + 1}  •  {pct:0.#}%  ({pxW}×{_monitor.WorkArea.Height})";
                    var ft = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Semibold"), 13, Brushes.White, dpi);

                    double bw = ft.Width + 28;
                    double bh = ft.Height + 14;
                    if (bw < segW - 16)
                    {
                        var br = new Rect(x0 + (segW - bw) / 2, 18, bw, bh);
                        dc.DrawRoundedRectangle(badgeBg, badgePen, br, 8, 8);
                        dc.DrawText(ft, new Point(br.Left + 14, br.Top + 7));
                    }
                }
            }
        }
    }
}
