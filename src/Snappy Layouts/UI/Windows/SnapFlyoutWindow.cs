using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    /// Bir pencerenin maksimize tuşu üzerine gelindiğinde (veya kısayolla) açılan,
    /// Windows 11 orijinal Snap Layouts görünümüne sadık özelleştirilebilir yerleşim menüsü (Flyout).
    /// </summary>
    public sealed class SnapFlyoutWindow : Window
    {
        private IntPtr _targetHwnd;
        private MonitorInfo _anchorMonitor;
        private POINT _lastAnchorPt;
        private MonitorInfo _selectedMonitor;
        private List<MonitorInfo> _allMonitors = new List<MonitorInfo>();
        private SnapArea _currentArea;
        private List<SnapArea> _monitorAreas = new List<SnapArea>();
        private List<WindowItem> _openWindows = new List<WindowItem>();

        private readonly Border _outerBorder;
        private readonly WrapPanel _monitorPillsPanel;
        private readonly WrapPanel _areaPillsPanel;
        private readonly WrapPanel _layoutsWrap;
        private readonly Button _btnPhysicalFull;
        private readonly TextBlock _hoverStatusText;
        private readonly Button _btnBorders;
        private readonly Button _btnOpenMain;
        private readonly DispatcherTimer _autoCloseTimer;
        private readonly SnapZonePreviewOverlay _zonePreview = new SnapZonePreviewOverlay();

        private IntPtr SelfHwnd => new WindowInteropHelper(this).Handle;

        public SnapFlyoutWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;

            // Uygulama teması ne olursa olsun Flyout her zaman Windows sistem temasını kullanır
            ThemeManager.ApplyWindowsThemeToDictionary(Resources);
            ThemeManager.ThemeChanged += () => ThemeManager.ApplyWindowsThemeToDictionary(Resources);
            ThemeManager.WindowsThemeChanged += () => ThemeManager.ApplyWindowsThemeToDictionary(Resources);

            _outerBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                // Üst kenar boşluğunu 2px tut ki maksimize tuşunun üstünü kapatmasın
                Margin = new Thickness(12, 2, 12, 12),
                Padding = new Thickness(12, 10, 12, 10),
                Width = 440,
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black }
            };
            _outerBorder.SetResourceReference(Border.BackgroundProperty, "Win11SurfaceBrush");
            _outerBorder.SetResourceReference(Border.BorderBrushProperty, "Win11BorderBrush");

            var rootStack = new StackPanel { Orientation = Orientation.Vertical };

            // 1) Fiziksel monitör seçici hapları (birden fazla fiziksel monitör varsa görünür)
            _monitorPillsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };
            rootStack.Children.Add(_monitorPillsPanel);

            // 2) Gerçek Fiziksel Tam Ekran (en solda) | Sanal monitör seçici hapları | Birleşik tam ekran çipleri
            _areaPillsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };

            _btnPhysicalFull = new Button
            {
                Padding = new Thickness(8, 2.5, 8, 2.5),
                Margin = new Thickness(0, 0, 4, 3),
                FontSize = 11,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnPhysicalFull.SetResourceReference(Button.BackgroundProperty, "Win11PermFillBrush");
            _btnPhysicalFull.SetResourceReference(Button.BorderBrushProperty, "Win11PermStrokeBrush");
            _btnPhysicalFull.MouseEnter += (s, e) =>
            {
                if (_currentArea?.Monitor != null)
                {
                    _hoverStatusText.Text = Loc.T("FlyoutPhysicalFull");
                    _zonePreview.ShowRect(_currentArea.Monitor.WorkArea, true, SelfHwnd);
                }
            };
            _btnPhysicalFull.MouseLeave += (s, e) =>
            {
                ResetHoverStatus();
                _zonePreview.HidePreview();
            };
            _btnPhysicalFull.Click += (s, e) =>
            {
                if (_targetHwnd != IntPtr.Zero && _currentArea != null)
                {
                    WindowPlacer.PhysicalMaximize(_targetHwnd, _currentArea.Monitor);
                    HideFlyout();
                }
            };

            rootStack.Children.Add(_areaPillsPanel);

            // Seçici satır ile yerleşim önizlemeleri arasında tam genişlikte yatay ayraç
            var headerDivider = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 4, 0, 8)
            };
            headerDivider.SetResourceReference(Border.BackgroundProperty, "Win11BorderBrush");
            rootStack.Children.Add(headerDivider);

            // 3) Windows 11 tarzı temiz yerleşim kutuları (kart çerçevesiz, doğrudan yuvarlatılmış kutular)
            _layoutsWrap = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            rootStack.Children.Add(_layoutsWrap);

            // 4) İnce alt durum şeridi (hover edilen düzen adı + küçük ikon butonlar)
            var footerBorder = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(0, 6, 0, 0)
            };
            footerBorder.SetResourceReference(Border.BorderBrushProperty, "Win11BorderBrush");

            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _hoverStatusText = new TextBlock
            {
                FontSize = 11,
                Height = 16,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, 0, 8, 0)
            };
            _hoverStatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            var footerBtns = new StackPanel { Orientation = Orientation.Horizontal };
            _btnBorders = new Button
            {
                Content = "◫",
                ToolTip = Loc.T("FlyoutShowBorders"),
                Padding = new Thickness(7, 2, 7, 2),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            _btnBorders.SetResourceReference(Button.BackgroundProperty, "Win11PillBgBrush");
            _btnBorders.SetResourceReference(Button.BorderBrushProperty, "Win11PillBorderBrush");
            _btnBorders.Click += (s, e) => App.Instance?.Overlay?.Flash();

            _btnOpenMain = new Button
            {
                Content = "⚙",
                ToolTip = Loc.T("FlyoutOpenApp"),
                Padding = new Thickness(7, 2, 7, 2),
                FontSize = 11.5,
                Cursor = Cursors.Hand
            };
            _btnOpenMain.SetResourceReference(Button.BackgroundProperty, "Win11PillBgBrush");
            _btnOpenMain.SetResourceReference(Button.BorderBrushProperty, "Win11PillBorderBrush");
            _btnOpenMain.Click += (s, e) =>
            {
                HideFlyout();
                App.Instance?.ShowMainWindow();
            };

            footerBtns.Children.Add(_btnBorders);
            footerBtns.Children.Add(_btnOpenMain);

            Grid.SetColumn(_hoverStatusText, 0);
            Grid.SetColumn(footerBtns, 1);
            footerGrid.Children.Add(_hoverStatusText);
            footerGrid.Children.Add(footerBtns);
            footerBorder.Child = footerGrid;
            rootStack.Children.Add(footerBorder);

            _outerBorder.Child = rootStack;
            Content = _outerBorder;

            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _autoCloseTimer.Tick += CheckAutoClose;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            AddExStyle(hwnd, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST);
        }

        public void ShowForWindow(IntPtr targetHwnd, POINT anchorScreenPt)
        {
            if (!WindowPlacer.IsValidTarget(targetHwnd)) return;
            _targetHwnd = targetHwnd;

            // Flyout her açılışta güncel Windows temasını kullanır
            ThemeManager.ApplyWindowsThemeToDictionary(Resources);

            _allMonitors = MonitorService.GetMonitors();
            _openWindows = WindowCatalog.GetAppWindows(includeMinimized: false);
            var mon = MonitorService.FromPoint(anchorScreenPt);
            _anchorMonitor = mon;
            _lastAnchorPt = anchorScreenPt;
            _selectedMonitor = _allMonitors.FirstOrDefault(m => m.Key == mon.Key) ?? mon;
            _monitorAreas = AreaService.GetAreas(_selectedMonitor);
            _currentArea = AreaService.AreaFromPoint(anchorScreenPt);
            if (_currentArea == null || _currentArea.Monitor.Key != _selectedMonitor.Key)
                _currentArea = _monitorAreas.First();

            RebuildUI();

            if (!IsVisible) Show();
            PositionWithinMonitor(mon, anchorScreenPt);
            _autoCloseTimer.Start();
        }

        /// <summary>
        /// Farklı DPI'lı monitörler (%125 ↔ %175) arasında geçişte WM_DPICHANGED kaynaklı sağa/aşağı taşmayı
        /// önlemek için pencereyi iki aşamada (önce hedef monitöre taşı → gerçek GetWindowRect ile piksel kırp) konumlandırır.
        /// </summary>
        private void PositionWithinMonitor(MonitorInfo mon, POINT anchorScreenPt)
        {
            if (mon == null) return;
            UpdateLayout();

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            double scale = mon.Scale;
            var wa = mon.WorkArea;
            var frame = GetExtendedFrame(_targetHwnd);

            int CalcX(int w)
            {
                int cx = anchorScreenPt.X - w + (int)Math.Round(60 * scale);
                if (cx + w > wa.Right - 8) cx = wa.Right - w - 8;
                if (cx < wa.Left + 8) cx = wa.Left + 8;
                return cx;
            }

            int CalcY(int h)
            {
                int captionBottom = frame.Top + (int)Math.Round(34 * scale);
                int cursorBottom = anchorScreenPt.Y + (int)Math.Round(16 * scale);
                int cy = (anchorScreenPt.Y >= frame.Top && anchorScreenPt.Y <= frame.Top + (int)Math.Round(72 * scale))
                    ? Math.Max(captionBottom, cursorBottom)
                    : cursorBottom;
                if (cy + h > wa.Bottom - 8) cy = wa.Bottom - h - 8;
                if (cy < wa.Top + 4) cy = wa.Top + 4;
                return cy;
            }

            // 1. Geçiş: pencereyi hedef monitöre taşı ki WM_DPICHANGED tetiklenip gerçek DPI ölçeği otursun
            int estW = Math.Max((int)Math.Round(ActualWidth * scale), (int)(360 * scale));
            int estH = Math.Max((int)Math.Round(ActualHeight * scale), (int)(220 * scale));
            NativeSnapSuppressor.DismissActiveNativeFlyout();
            SetWindowPos(hwnd, HWND_TOPMOST, CalcX(estW), CalcY(estH), 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            // 2. Geçiş: WM_DPICHANGED sonrası gerçek fiziksel pencere boyutunu GetWindowRect ile ölç ve ekrana tam sığdır
            UpdateLayout();
            if (GetWindowRect(hwnd, out var realRect) && realRect.Width > 0 && realRect.Height > 0)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, CalcX(realRect.Width), CalcY(realRect.Height), 0, 0,
                    SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        public void HideFlyout()
        {
            _autoCloseTimer.Stop();
            _zonePreview.HidePreview();
            if (IsVisible) Hide();
        }

        private void ResetHoverStatus()
        {
            if (_currentArea == null) return;
            _hoverStatusText.Text = $"{_currentArea.Label}  •  Snappy Layouts {AppInfo.VersionText}";
        }

        private void CheckAutoClose(object sender, EventArgs e)
        {
            if (!IsVisible) { _autoCloseTimer.Stop(); return; }
            NativeSnapSuppressor.DismissActiveNativeFlyout();
            if (IsKeyDown(VK_ESCAPE)) { HideFlyout(); return; }
            if (!GetCursorPos(out var pt)) return;

            var hwnd = new WindowInteropHelper(this).Handle;
            GetWindowRect(hwnd, out var flyRect);
            // Maksimize tuşu Flyout'un hemen üstünde kaldığı için üst geçiş koridorunu geniş tut
            var safeRect = new RECT(flyRect.Left - 36, flyRect.Top - 72, flyRect.Right + 36, flyRect.Bottom + 36);
            if (safeRect.Contains(pt)) return;

            // UI iş parçacığında hedef pencereye bloklayıcı SendMessageTimeout göndermeden kontrol et
            if (_targetHwnd != IntPtr.Zero)
            {
                if (MaximizeHoverWatcher.IsPointInWindowMaxButton(_targetHwnd, pt)) return;
                if (App.Instance?.HoverWatcher != null && App.Instance.HoverWatcher.Current.Window == _targetHwnd) return;
            }

            HideFlyout();
        }

        private Button MakeWin11Pill(string text, bool active, bool isVirtual = false)
        {
            var pill = new Button
            {
                Content = text,
                Padding = isVirtual ? new Thickness(9, 2.5, 9, 2.5) : new Thickness(9, 3, 9, 3),
                Margin = new Thickness(0, 0, 4, 3),
                FontSize = isVirtual ? 10.5 : 11.5,
                FontWeight = isVirtual
                    ? (active ? FontWeights.Bold : FontWeights.Medium)
                    : (active ? FontWeights.Bold : FontWeights.SemiBold),
                Cursor = Cursors.Hand
            };

            // Fiziksel monitör (köşeli mavi kart) vs Sanal monitör (yuvarlatılmış camgöbeği hap) görsel ayrımı
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(isVirtual ? 12 : 5));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(active ? 1.5 : 1));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;
            pill.Template = template;

            if (isVirtual)
            {
                pill.SetResourceReference(Button.BackgroundProperty, active ? "Win11VirtPillActiveBgBrush" : "Win11VirtPillBgBrush");
                pill.SetResourceReference(Button.BorderBrushProperty, active ? "Win11VirtPillActiveBorderBrush" : "Win11VirtPillBorderBrush");
                if (active)
                    pill.Foreground = Brushes.White;
                else
                    pill.SetResourceReference(Button.ForegroundProperty, "VirtMonFgBrush");
            }
            else
            {
                pill.SetResourceReference(Button.BackgroundProperty, active ? "Win11PillActiveBgBrush" : "Win11PillBgBrush");
                pill.SetResourceReference(Button.BorderBrushProperty, active ? "Win11PillActiveBorderBrush" : "Win11PillBorderBrush");
                if (active)
                    pill.Foreground = Brushes.White;
                else
                    pill.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            }
            return pill;
        }

        private void RebuildUI()
        {
            ResetHoverStatus();
            _btnBorders.ToolTip = Loc.T("FlyoutShowBorders");
            _btnOpenMain.ToolTip = Loc.T("FlyoutOpenApp");

            // 1) Fiziksel monitör seçici hapları (tüm fiziksel monitörler)
            _monitorPillsPanel.Children.Clear();
            if (_allMonitors.Count > 1)
            {
                _monitorPillsPanel.Visibility = Visibility.Visible;
                foreach (var monItem in _allMonitors)
                {
                    bool isSelMon = _selectedMonitor != null && monItem.Key == _selectedMonitor.Key;
                    string star = monItem.IsPrimary ? " ★" : "";
                    var mPill = MakeWin11Pill($"🖥 {monItem.Index}. {monItem.EffectiveName}{star}", isSelMon);
                    var capturedMon = monItem;
                    mPill.MouseEnter += (s, e) =>
                    {
                        _hoverStatusText.Text = capturedMon.DisplayName;
                        _zonePreview.ShowRect(capturedMon.WorkArea, true, SelfHwnd);
                    };
                    mPill.MouseLeave += (s, e) =>
                    {
                        ResetHoverStatus();
                        _zonePreview.HidePreview();
                    };
                    mPill.Click += (s, e) =>
                    {
                        _selectedMonitor = capturedMon;
                        _monitorAreas = AreaService.GetAreas(_selectedMonitor);
                        _currentArea = _monitorAreas.First();
                        _zonePreview.HidePreview();
                        RebuildUI();
                        if (IsVisible && _anchorMonitor != null)
                            PositionWithinMonitor(_anchorMonitor, _lastAnchorPt);
                    };
                    _monitorPillsPanel.Children.Add(mPill);
                }
            }
            else
            {
                _monitorPillsPanel.Visibility = Visibility.Collapsed;
            }

            // 2) Gerçek Fiziksel Tam Ekran (en solda) | Sanal monitör seçici hapları | Birleşik tam ekran çipleri
            _areaPillsPanel.Children.Clear();
            _btnPhysicalFull.Content = "⛶ " + Loc.T("FlyoutPhysicalFull");
            _areaPillsPanel.Children.Add(_btnPhysicalFull);

            if (_monitorAreas.Count > 1)
            {
                var physSep = new Border
                {
                    Width = 1,
                    Height = 16,
                    Margin = new Thickness(3, 0, 7, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                physSep.SetResourceReference(Border.BackgroundProperty, "Win11BorderBrush");
                _areaPillsPanel.Children.Add(physSep);

                string vShort = Loc.T("VirtualShort");
                foreach (var area in _monitorAreas)
                {
                    bool active = area.SegmentIndex == _currentArea.SegmentIndex;
                    var pill = MakeWin11Pill($"◫ {vShort} {area.SegmentIndex + 1}", active, isVirtual: true);
                    if (_monitorAreas.Count >= 3) pill.ToolTip = Loc.T("FlyoutSpanHint");
                    var captured = area;
                    pill.MouseEnter += (s, e) =>
                    {
                        if (_monitorAreas.Count >= 3 && IsKeyDown(VK_SHIFT) && captured.SegmentIndex != _currentArea.SegmentIndex)
                        {
                            var spanPreview = AreaService.CombineAreas(_monitorAreas, _currentArea.SegmentIndex, captured.SegmentIndex);
                            _hoverStatusText.Text = $"{Loc.T("FlyoutSpanFull")}: {spanPreview.Label}";
                            _zonePreview.ShowRect(spanPreview.Rect, false, SelfHwnd);
                        }
                        else
                        {
                            _hoverStatusText.Text = captured.Label;
                            _zonePreview.ShowRect(captured.Rect, false, SelfHwnd);
                        }
                    };
                    pill.MouseLeave += (s, e) =>
                    {
                        ResetHoverStatus();
                        _zonePreview.HidePreview();
                    };
                    pill.Click += (s, e) =>
                    {
                        if (_monitorAreas.Count >= 3 && IsKeyDown(VK_SHIFT) && captured.SegmentIndex != _currentArea.SegmentIndex)
                        {
                            var spanArea = AreaService.CombineAreas(_monitorAreas, _currentArea.SegmentIndex, captured.SegmentIndex);
                            if (_targetHwnd != IntPtr.Zero && spanArea != null)
                            {
                                WindowPlacer.VirtualMaximize(_targetHwnd, spanArea, allowToggle: false);
                                HideFlyout();
                                return;
                            }
                        }
                        _currentArea = captured;
                        _zonePreview.HidePreview();
                        RebuildUI();
                    };
                    _areaPillsPanel.Children.Add(pill);
                }

                // 3+ sanal monitör varsa yan yana 2'li birleşik tam ekran (örn. Virtual 1 + Virtual 2) tek tık çipleri
                if (_monitorAreas.Count >= 3)
                {
                    var sep = new Border
                    {
                        Width = 1,
                        Height = 16,
                        Margin = new Thickness(3, 0, 7, 3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    sep.SetResourceReference(Border.BackgroundProperty, "Win11BorderBrush");
                    _areaPillsPanel.Children.Add(sep);

                    for (int i = 0; i < _monitorAreas.Count - 1; i++)
                    {
                        var spanArea = AreaService.CombineAreas(_monitorAreas, i, i + 1);
                        string spanText = $"◫ {vShort} {i + 1} + {vShort} {i + 2}";
                        var spanPill = MakeWin11Pill(spanText, false, isVirtual: true);
                        spanPill.ToolTip = $"{Loc.T("FlyoutSpanFull")} ({vShort} {i + 1} + {vShort} {i + 2})";
                        var capturedSpan = spanArea;
                        spanPill.MouseEnter += (s, e) =>
                        {
                            _hoverStatusText.Text = $"{Loc.T("FlyoutSpanFull")}: {capturedSpan.Label}";
                            _zonePreview.ShowRect(capturedSpan.Rect, false, SelfHwnd);
                        };
                        spanPill.MouseLeave += (s, e) =>
                        {
                            ResetHoverStatus();
                            _zonePreview.HidePreview();
                        };
                        spanPill.Click += (s, e) =>
                        {
                            if (_targetHwnd != IntPtr.Zero && capturedSpan != null)
                            {
                                WindowPlacer.VirtualMaximize(_targetHwnd, capturedSpan, allowToggle: false);
                                HideFlyout();
                            }
                        };
                        _areaPillsPanel.Children.Add(spanPill);
                    }
                }
            }

            // 3) Windows 11 tarzı yerleşim kutuları (her düzen tek kez görünür; yalnızca tam o bölgede bulunan uygulamaların ikonları çizilir)
            _layoutsWrap.Children.Clear();
            double aspect = _currentArea.Aspect;
            double boxH = 62;
            double boxW = Math.Clamp(boxH * Math.Clamp(aspect, 0.55, 2.6), 58, 148);

            var areaLayouts = AreaService.GetLayoutsForArea(_currentArea);
            var layouts = areaLayouts.Where(l => l.IsVisibleFor(aspect)).ToList();
            if (layouts.Count == 0) layouts = areaLayouts.ToList();

            // Sabit genişlik hesapla ki hover sırasında alt metin değişince pencere boyutu/konumu asla oynamasın (sıfır flicker)
            // ve hedef monitörün çalışma alanını asla aşmasın
            int totalTiles = 1 + layouts.Count;
            int colsPerRow = Math.Min(totalTiles, boxW > 115 ? 3 : 4);
            double desiredW = colsPerRow * (boxW + 14) + 32;
            var hostMon = _anchorMonitor ?? _selectedMonitor;
            double maxLogicalW = hostMon != null ? Math.Max(340, (hostMon.WorkArea.Width / Math.Max(1.0, hostMon.Scale)) - 48) : 560;
            _outerBorder.Width = Math.Clamp(desiredW, 360, Math.Min(560, maxLogicalW));

            // Sanal Monitör Tam Ekran (100%) her zaman yerleşimlerin EN BAŞINDA yer alır
            var activeWinIcon = WindowIconProvider.GetWindowIcon(_openWindows, _targetHwnd);

            string vmFullLabel = _currentArea.IsVirtual
                ? $"{Loc.T("FlyoutVirtualFull")} (#{_currentArea.SegmentIndex + 1})"
                : Loc.T("FlyoutVirtualFull");

            var vmFullPreview = new LayoutPreviewControl
            {
                Layout = null,
                MonitorAspect = aspect,
                IsEditor = false,
                IsWin11Style = true,
                ZoneIcons = WindowIconProvider.BuildOccupiedZoneIcons(_currentArea, null, _openWindows, _targetHwnd),
                ActiveZoneIndex = WindowIconProvider.FindActiveWindowZoneIndex(_currentArea, null, _openWindows, _targetHwnd),
                ActiveZoneIcon = activeWinIcon,
                Width = boxW,
                Height = boxH,
                Margin = new Thickness(7, 6, 7, 6),
                ToolTip = vmFullLabel
            };
            vmFullPreview.ZoneHovered += relRect =>
            {
                if (relRect == null || _currentArea == null)
                {
                    ResetHoverStatus();
                    _zonePreview.HidePreview();
                }
                else
                {
                    _hoverStatusText.Text = vmFullLabel;
                    _zonePreview.ShowRect(WindowPlacer.ZoneToPixels(_currentArea, new Rect(0, 0, 1, 1)), false, SelfHwnd);
                }
            };
            vmFullPreview.ZoneClicked += (relRect, cell) =>
            {
                if (_targetHwnd != IntPtr.Zero && _currentArea != null)
                {
                    WindowPlacer.VirtualMaximize(_targetHwnd, _currentArea, allowToggle: false);
                    HideFlyout();
                }
            };
            _layoutsWrap.Children.Add(vmFullPreview);

            foreach (var layout in layouts)
            {
                var capturedLayout = layout;
                var preview = new LayoutPreviewControl
                {
                    Layout = capturedLayout,
                    MonitorAspect = aspect,
                    IsEditor = false,
                    IsWin11Style = true,
                    ZoneIcons = WindowIconProvider.BuildOccupiedZoneIcons(_currentArea, capturedLayout, _openWindows, _targetHwnd),
                    ActiveZoneIndex = WindowIconProvider.FindActiveWindowZoneIndex(_currentArea, capturedLayout, _openWindows, _targetHwnd),
                    ActiveZoneIcon = activeWinIcon,
                    Width = boxW,
                    Height = boxH,
                    Margin = new Thickness(7, 6, 7, 6),
                    ToolTip = capturedLayout.DisplayTitle
                };
                preview.ZoneHovered += relRect =>
                {
                    if (relRect == null || _currentArea == null)
                    {
                        ResetHoverStatus();
                        _zonePreview.HidePreview();
                    }
                    else
                    {
                        _hoverStatusText.Text = capturedLayout.DisplayTitle;
                        _zonePreview.ShowRect(WindowPlacer.ZoneToPixels(_currentArea, relRect.Value), false, SelfHwnd);
                    }
                };
                preview.ZoneClicked += (relRect, cell) =>
                {
                    if (_targetHwnd != IntPtr.Zero && _currentArea != null)
                    {
                        WindowPlacer.SnapToZone(_targetHwnd, _currentArea, relRect);
                        HideFlyout();
                    }
                };
                _layoutsWrap.Children.Add(preview);
            }
        }
    }

    /// <summary>
    /// Flyout veya TopSnapBar içindeki bir kutunun üzerine gelindiğinde gerçek ekranda pencerenin yerleşeceği alanı
    /// Windows 11 tarzı yarı saydam bir çerçeveyle gösteren önizleme katmanı.
    /// </summary>
    internal sealed class SnapZonePreviewOverlay : Window
    {
        private readonly Border _box;

        public SnapZonePreviewOverlay()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Manual;
            MinWidth = 0;
            MinHeight = 0;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;

            _box = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Content = _box;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            AddExStyle(hwnd, WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST);
        }

        public void ShowRect(RECT r, bool isPhysicalFull, IntPtr insertAfterHwnd = default)
        {
            // Daha yumuşak ve daha şeffaf pastel mavi / yeşil önizleme
            var col = isPhysicalFull ? Color.FromRgb(52, 211, 153) : Color.FromRgb(96, 165, 250);
            _box.Background = new SolidColorBrush(Color.FromArgb(18, col.R, col.G, col.B));
            _box.BorderBrush = new SolidColorBrush(Color.FromArgb(115, col.R, col.G, col.B));

            if (!IsVisible) Show();
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // 1. Geçiş: hedef monitöre HWND_TOPMOST olarak taşı (Notepad dahil tüm pencerelerin üstünde olsun!)
                SetWindowPos(hwnd, HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);

                // Pencerenin şu anki gerçek DPI değerini okuyup WPF mantıksal boyutunu sınırsız olarak eşitle
                uint winDpi = GetDpiForWindow(hwnd);
                double actualScale = winDpi >= 96 ? (winDpi / 96.0) : 1.0;
                Width = Math.Max(10, r.Width / actualScale);
                Height = Math.Max(10, r.Height / actualScale);
                UpdateLayout();

                // 2. Geçiş: WM_DPICHANGED sonrası tam fiziksel piksel sınırlarına oturt
                SetWindowPos(hwnd, HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);

                // Flyout / SnapBar penceresini önizlemenin hemen üstünde tut
                if (insertAfterHwnd != IntPtr.Zero)
                {
                    SetWindowPos(insertAfterHwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        public void HidePreview()
        {
            if (IsVisible) Hide();
        }
    }
}
