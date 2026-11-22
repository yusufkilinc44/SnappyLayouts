using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SnappyLayout.Core;
using SnappyLayout.Models;
using SnappyLayout.Native;
using SnappyLayout.Services;
using SnappyLayout.UI.Controls;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.UI.Windows
{
    /// <summary>
    /// Bir pencere fareyle sürüklenip ekranın (veya sanal monitörün) en üst kenarına yaklaştırıldığında
    /// Windows 11 orijinal Snap Bar görünümünde açılan üst yerleşim çubuğu.
    /// Fare bırakıldığında seçili bölgeye pencereyi anında yerleştirir.
    /// </summary>
    public sealed class TopSnapBarWindow : Window
    {
        private sealed class BarTileEntry
        {
            public LayoutPreviewControl Control;
            public bool IsVirtualFull;
            public bool IsPhysicalFull;
            public SnapArea OverrideArea; // Yan yana birleşik sanal monitörler (örn. 1+2, 2+3) için
        }

        private IntPtr _draggedHwnd;
        private bool _trackingDrag;
        private SnapArea _currentArea;
        private List<WindowItem> _openWindows = new List<WindowItem>();
        private readonly List<BarTileEntry> _tiles = new List<BarTileEntry>();

        private SnapArea _pendingArea;
        private Rect? _pendingRelRect;
        private bool _pendingIsVirtualFull;
        private bool _pendingIsPhysicalFull;
        private RECT? _lastEdgePreviewRect;

        private readonly StackPanel _tilesPanel;
        private readonly DispatcherTimer _pollTimer;
        private readonly SnapZonePreviewOverlay _zonePreview = new SnapZonePreviewOverlay();

        public TopSnapBarWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;

            // Her zaman Windows sistem temasını kullanır
            ThemeManager.ApplyWindowsThemeToDictionary(Resources);
            ThemeManager.ThemeChanged += () => ThemeManager.ApplyWindowsThemeToDictionary(Resources);
            ThemeManager.WindowsThemeChanged += () => ThemeManager.ApplyWindowsThemeToDictionary(Resources);

            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(12, 4, 12, 12),
                Padding = new Thickness(12, 10, 12, 10),
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black }
            };
            outerBorder.SetResourceReference(Border.BackgroundProperty, "Win11SurfaceBrush");
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "Win11BorderBrush");

            _tilesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            outerBorder.Child = _tilesPanel;
            Content = outerBorder;

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            _pollTimer.Tick += OnPollTick;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            AddExStyle(hwnd, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TRANSPARENT);
        }

        public void OnDragStateChanged(bool dragging, IntPtr hwnd)
        {
            if (dragging)
            {
                if ((App.Instance?.Paused ?? false) || !Storage.Data.Settings.FlyoutEnabled) return;
                if (!WindowPlacer.IsValidTarget(hwnd)) return;

                _draggedHwnd = hwnd;
                _trackingDrag = true;
                _openWindows = WindowCatalog.GetAppWindows(includeMinimized: false);
                ClearPending();
                _pollTimer.Start();
            }
            else
            {
                FinishDrag(commitPending: true);
            }
        }

        private void ClearPending()
        {
            _pendingArea = null;
            _pendingRelRect = null;
            _pendingIsVirtualFull = false;
            _pendingIsPhysicalFull = false;
            _lastEdgePreviewRect = null;
        }

        private void FinishDrag(bool commitPending)
        {
            if (!_trackingDrag && !IsVisible) return;
            _trackingDrag = false;
            _pollTimer.Stop();

            IntPtr hwndToSnap = _draggedHwnd;
            var snapArea = _pendingArea;
            var snapRel = _pendingRelRect;
            bool isVirtFull = _pendingIsVirtualFull;
            bool isPhysFull = _pendingIsPhysicalFull;

            ClearPending();
            _zonePreview.HidePreview();
            if (IsVisible) Hide();

            if (commitPending && hwndToSnap != IntPtr.Zero && snapArea != null && IsWindow(hwndToSnap))
            {
                // MOVESIZEEND tamamlandıktan hemen sonra pencereyi hedef bölgeye yerleştir
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!IsWindow(hwndToSnap)) return;
                    if (isPhysFull)
                        WindowPlacer.PhysicalMaximize(hwndToSnap, snapArea.Monitor);
                    else if (isVirtFull)
                        WindowPlacer.VirtualMaximize(hwndToSnap, snapArea, allowToggle: false);
                    else if (snapRel.HasValue)
                        WindowPlacer.SnapToZone(hwndToSnap, snapArea, snapRel.Value);
                }), DispatcherPriority.Background);
            }
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (!_trackingDrag) { _pollTimer.Stop(); return; }

            // Windows 11'in kendi üst Snap Bar penceresini anında bastır
            NativeSnapSuppressor.DismissActiveNativeFlyout();

            if (!IsKeyDown(VK_LBUTTON))
            {
                FinishDrag(commitPending: true);
                return;
            }

            if (!GetCursorPos(out var pt)) return;

            var area = AreaService.AreaFromPoint(pt);
            if (area == null) return;
            var wa = area.Monitor.WorkArea;
            double scale = area.Monitor.Scale;
            int triggerBand = (int)Math.Round(38 * scale);

            if (!IsVisible)
            {
                if (pt.Y <= wa.Top + triggerBand)
                {
                    ClearPending();
                    _zonePreview.HidePreview();
                    ShowForArea(area);
                    // İmleç doğrudan en üst kenardaysa anında aktif sanal monitör tam ekran önizlemesini ve ilk kutu seçimini göster
                    if (pt.Y <= wa.Top + (int)Math.Round(16 * scale))
                    {
                        var barHwnd = new WindowInteropHelper(this).Handle;
                        var fullPx = WindowPlacer.ZoneToPixels(area, new Rect(0, 0, 1, 1));
                        _pendingArea = area;
                        _pendingRelRect = new Rect(0, 0, 1, 1);
                        _pendingIsVirtualFull = true;
                        _pendingIsPhysicalFull = false;
                        _lastEdgePreviewRect = fullPx;
                        if (_tiles.Count > 0 && _tiles[0].IsVirtualFull)
                            _tiles[0].Control.SetForcedSelected(true);
                        _zonePreview.ShowRect(fullPx, isPhysicalFull: false, barHwnd);
                    }
                    return;
                }

                // Fiziksel monitörün en sol veya en sağ kenarına sürüklenince:
                // Windows'un kendi yarım-fiziksel önizlemesi yerine o taraftaki uç sanal monitörün
                // (sanal monitör yoksa fiziksel monitörün) ilgili yarısını (%50) göster ve bırakınca yerleştir.
                int edgeBand = (int)Math.Round(16 * scale);
                bool atLeftEdge = pt.X <= wa.Left + edgeBand;
                bool atRightEdge = pt.X >= wa.Right - edgeBand;

                if (atLeftEdge || atRightEdge)
                {
                    var areas = AreaService.GetAreas(area.Monitor);
                    if (areas != null && areas.Count > 0)
                    {
                        var edgeArea = atLeftEdge ? areas[0] : areas[areas.Count - 1];
                        var rel = atLeftEdge ? new Rect(0, 0, 0.5, 1) : new Rect(0.5, 0, 0.5, 1);
                        _pendingArea = edgeArea;
                        _pendingRelRect = rel;
                        _pendingIsVirtualFull = false;
                        _pendingIsPhysicalFull = false;

                        var pxRect = WindowPlacer.ZoneToPixels(edgeArea, rel);
                        if (!_lastEdgePreviewRect.HasValue ||
                            _lastEdgePreviewRect.Value.Left != pxRect.Left ||
                            _lastEdgePreviewRect.Value.Top != pxRect.Top ||
                            _lastEdgePreviewRect.Value.Right != pxRect.Right ||
                            _lastEdgePreviewRect.Value.Bottom != pxRect.Bottom)
                        {
                            _lastEdgePreviewRect = pxRect;
                            _zonePreview.ShowRect(pxRect, isPhysicalFull: false);
                        }
                    }
                }
                else if (_pendingArea != null || _lastEdgePreviewRect.HasValue)
                {
                    ClearPending();
                    _zonePreview.HidePreview();
                }
                return;
            }

            // Üst bölgedeyken başka bir sanal/fiziksel monitöre geçildiyse çubuğu o alana taşı
            if (_currentArea == null ||
                _currentArea.Monitor.Key != area.Monitor.Key ||
                _currentArea.SegmentIndex != area.SegmentIndex)
            {
                if (pt.Y <= wa.Top + (int)Math.Round(120 * scale))
                {
                    ShowForArea(area);
                }
            }

            // Çubuk içindeki kutularda fare konumuna göre bölge eşleştirme (hit-test)
            var hwnd = new WindowInteropHelper(this).Handle;
            GetWindowRect(hwnd, out var barRect);

            // Kullanıcı pencereyi tekrar aşağı çektiyse çubuğu gizle
            if (pt.Y > barRect.Bottom + (int)Math.Round(65 * scale))
            {
                ClearPending();
                foreach (var t in _tiles) t.Control.SetExternalHoverPoint(null);
                _zonePreview.HidePreview();
                Hide();
                return;
            }

            bool hitAny = false;
            foreach (var entry in _tiles)
            {
                try
                {
                    Point local = entry.Control.PointFromScreen(new Point(pt.X, pt.Y));
                    if (local.X >= 0 && local.X <= entry.Control.ActualWidth &&
                        local.Y >= 0 && local.Y <= entry.Control.ActualHeight)
                    {
                        var rel = entry.Control.SetExternalHoverPoint(local);
                        if (rel.HasValue)
                        {
                            hitAny = true;
                            _lastEdgePreviewRect = null;
                            var effectiveArea = entry.OverrideArea ?? _currentArea;
                            _pendingArea = effectiveArea;
                            _pendingRelRect = rel.Value;
                            _pendingIsVirtualFull = entry.IsVirtualFull || entry.OverrideArea != null;
                            _pendingIsPhysicalFull = entry.IsPhysicalFull;

                            if (entry.IsPhysicalFull)
                                _zonePreview.ShowRect(_currentArea.Monitor.WorkArea, true, hwnd);
                            else if (entry.OverrideArea != null)
                                _zonePreview.ShowRect(entry.OverrideArea.Rect, false, hwnd);
                            else if (entry.IsVirtualFull)
                                _zonePreview.ShowRect(WindowPlacer.ZoneToPixels(_currentArea, new Rect(0, 0, 1, 1)), false, hwnd);
                            else
                                _zonePreview.ShowRect(WindowPlacer.ZoneToPixels(_currentArea, rel.Value), false, hwnd);
                            continue;
                        }
                    }
                    entry.Control.SetExternalHoverPoint(null);
                }
                catch { }
            }

            if (!hitAny)
            {
                // İmleç en üst kenardayken (herhangi bir kutu üzerinde değilken) aktif sanal monitörde Tam Ekran önizlemesi ve ilk kutu seçimini göster
                int topFullscreenBand = (int)Math.Round(16 * scale);
                if (pt.Y <= wa.Top + topFullscreenBand)
                {
                    var targetArea = area;
                    var fullPx = WindowPlacer.ZoneToPixels(targetArea, new Rect(0, 0, 1, 1));
                    _pendingArea = targetArea;
                    _pendingRelRect = new Rect(0, 0, 1, 1);
                    _pendingIsVirtualFull = true;
                    _pendingIsPhysicalFull = false;

                    if (_tiles.Count > 0 && _tiles[0].IsVirtualFull)
                        _tiles[0].Control.SetForcedSelected(true);

                    if (!_lastEdgePreviewRect.HasValue ||
                        _lastEdgePreviewRect.Value.Left != fullPx.Left ||
                        _lastEdgePreviewRect.Value.Top != fullPx.Top ||
                        _lastEdgePreviewRect.Value.Right != fullPx.Right ||
                        _lastEdgePreviewRect.Value.Bottom != fullPx.Bottom)
                    {
                        _lastEdgePreviewRect = fullPx;
                        _zonePreview.ShowRect(fullPx, isPhysicalFull: false, hwnd);
                    }
                }
                else
                {
                    ClearPending();
                    _zonePreview.HidePreview();
                }
            }
        }

        private void ShowForArea(SnapArea area)
        {
            _currentArea = area;
            ThemeManager.ApplyWindowsThemeToDictionary(Resources);

            _tilesPanel.Children.Clear();
            _tiles.Clear();

            double aspect = area.Aspect;
            double boxH = 54;
            double boxW = Math.Clamp(boxH * Math.Clamp(aspect, 0.55, 2.5), 52, 130);

            // 1) Sanal Monitör Tam Ekran (100%) her zaman en başta
            var vmFull = new LayoutPreviewControl
            {
                Layout = null,
                MonitorAspect = aspect,
                IsEditor = false,
                IsWin11Style = true,
                ZoneIcons = WindowIconProvider.BuildOccupiedZoneIcons(area, null, _openWindows, _draggedHwnd),
                Width = boxW,
                Height = boxH,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _tilesPanel.Children.Add(vmFull);
            _tiles.Add(new BarTileEntry { Control = vmFull, IsVirtualFull = true });

            // 2) Seçili alanın pencere yerleşim düzenleri (Windows 11 Snap Bar yatay şerit)
            var areaLayouts = AreaService.GetLayoutsForArea(area);
            var layouts = areaLayouts.Where(l => l.IsVisibleFor(aspect)).ToList();
            if (layouts.Count == 0) layouts = areaLayouts.ToList();

            // Yatay çubuğun ekranı aşmaması için makul sayıda (en fazla 6) şablon göster
            foreach (var layout in layouts.Take(6))
            {
                var ctrl = new LayoutPreviewControl
                {
                    Layout = layout,
                    MonitorAspect = aspect,
                    IsEditor = false,
                    IsWin11Style = true,
                    ZoneIcons = WindowIconProvider.BuildOccupiedZoneIcons(area, layout, _openWindows, _draggedHwnd),
                    Width = boxW,
                    Height = boxH,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                _tilesPanel.Children.Add(ctrl);
                _tiles.Add(new BarTileEntry { Control = ctrl });
            }

            // 3) Sanal monitör aktifse en sağda ince ayırıcı + (3+ sanal monitörde komşu 2'li birleşik tam ekran) + Gerçek Fiziksel Tam Ekran
            if (area.IsVirtual)
            {
                var sep = new Border
                {
                    Width = 1,
                    Height = 40,
                    Margin = new Thickness(2, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                sep.SetResourceReference(Border.BackgroundProperty, "Win11BorderBrush");
                _tilesPanel.Children.Add(sep);

                var allAreas = AreaService.GetAreas(area.Monitor);
                if (allAreas.Count >= 3)
                {
                    // Aktif sanal monitörle komşu birleşik alanları (örn. 1+2 veya 2+3) ekle
                    int idx = area.SegmentIndex;
                    var spanIndices = new List<(int s, int e)>();
                    if (idx > 0) spanIndices.Add((idx - 1, idx));
                    if (idx < allAreas.Count - 1) spanIndices.Add((idx, idx + 1));

                    foreach (var (sIdx, eIdx) in spanIndices)
                    {
                        var combined = AreaService.CombineAreas(allAreas, sIdx, eIdx);
                        if (combined == null) continue;
                        double spanW = Math.Clamp(boxH * Math.Clamp(combined.Aspect, 0.7, 2.5), 62, 124);
                        var spanCtrl = new LayoutPreviewControl
                        {
                            Layout = null,
                            MonitorAspect = combined.Aspect,
                            IsEditor = false,
                            IsWin11Style = true,
                            Width = spanW,
                            Height = boxH,
                            Margin = new Thickness(0, 0, 10, 0)
                        };
                        _tilesPanel.Children.Add(spanCtrl);
                        _tiles.Add(new BarTileEntry { Control = spanCtrl, IsVirtualFull = true, OverrideArea = combined });
                    }
                }

                double physW = Math.Clamp(boxH * Math.Clamp(area.Monitor.Aspect, 0.6, 2.6), 54, 132);
                var physFull = new LayoutPreviewControl
                {
                    IsPermanentFull = true,
                    MonitorAspect = area.Monitor.Aspect,
                    IsEditor = false,
                    IsWin11Style = true,
                    Width = physW,
                    Height = boxH
                };
                _tilesPanel.Children.Add(physFull);
                _tiles.Add(new BarTileEntry { Control = physFull, IsPhysicalFull = true });
            }

            if (!IsVisible) Show();
            UpdateLayout();

            var hwnd = new WindowInteropHelper(this).Handle;
            double scale = area.Monitor.Scale;
            var targetRect = area.Rect;
            var wa = area.Monitor.WorkArea;
            int y = wa.Top + (int)Math.Round(2 * scale);

            int CalcBarX(int w)
            {
                int cx = targetRect.Left + (targetRect.Width - w) / 2;
                if (cx + w > wa.Right - 6) cx = wa.Right - w - 6;
                if (cx < wa.Left + 6) cx = wa.Left + 6;
                return cx;
            }

            // 1. Geçiş: hedef monitöre taşı (WM_DPICHANGED tetiklensin)
            int estW = Math.Max((int)Math.Round(ActualWidth * scale), (int)(400 * scale));
            NativeSnapSuppressor.DismissActiveNativeFlyout();
            SetWindowPos(hwnd, HWND_TOPMOST, CalcBarX(estW), y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            // 2. Geçiş: gerçek fiziksel genişliği GetWindowRect ile ölçüp ekrana tam sığdır
            UpdateLayout();
            if (GetWindowRect(hwnd, out var realRect) && realRect.Width > 0)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, CalcBarX(realRect.Width), y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }
    }
}
