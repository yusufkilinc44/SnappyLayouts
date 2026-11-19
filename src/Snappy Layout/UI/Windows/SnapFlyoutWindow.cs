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
    /// Bir pencerenin maksimize tuşu üzerine gelindiğinde (veya kısayolla) açılan
    /// özelleştirilebilir yerleşim alternatifleri penceresi (Flyout).
    /// </summary>
    public sealed class SnapFlyoutWindow : Window
    {
        private IntPtr _targetHwnd;
        private MonitorInfo _selectedMonitor;
        private List<MonitorInfo> _allMonitors = new List<MonitorInfo>();
        private SnapArea _currentArea;
        private List<SnapArea> _monitorAreas = new List<SnapArea>();

        private readonly WrapPanel _monitorPillsPanel;
        private readonly WrapPanel _areaPillsPanel;
        private readonly TextBlock _headerTitleText;
        private readonly TextBlock _aspectInfoText;
        private readonly WrapPanel _layoutsWrap;
        private readonly Button _btnPhysicalFull;
        private readonly Button _btnBorders;
        private readonly Button _btnOpenMain;
        private readonly LayoutPreviewControl _permFullPreview;
        private readonly DispatcherTimer _autoCloseTimer;
        private readonly SnapZonePreviewOverlay _zonePreview = new SnapZonePreviewOverlay();

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

            // Uygulama teması ne olursa olsun Flyout her zaman Windows temasında çalışır
            ThemeManager.ApplyWindowsThemeToDictionary(Resources);
            ThemeManager.ThemeChanged += () => ThemeManager.ApplyWindowsThemeToDictionary(Resources);
            ThemeManager.WindowsThemeChanged += () => ThemeManager.ApplyWindowsThemeToDictionary(Resources);

            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1.5),
                // Üst kenar boşluğunu 2px tut ki maksimize tuşunun üstünü kapatmasın
                Margin = new Thickness(12, 2, 12, 12),
                Padding = new Thickness(14),
                MaxWidth = 680,
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 6, Opacity = 0.42, Color = Colors.Black }
            };
            outerBorder.SetResourceReference(Border.BackgroundProperty, "FlyoutBgBrush");
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "FlyoutBorderBrush");

            var rootStack = new StackPanel { Orientation = Orientation.Vertical };

            // 1) Üst başlık
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _headerTitleText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 420
            };
            _headerTitleText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            _aspectInfoText = new TextBlock
            {
                FontSize = 11.5,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _aspectInfoText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

            Grid.SetColumn(_headerTitleText, 0);
            Grid.SetColumn(_aspectInfoText, 1);
            headerGrid.Children.Add(_headerTitleText);
            headerGrid.Children.Add(_aspectInfoText);
            rootStack.Children.Add(headerGrid);

            // 2a) Fiziksel monitör seçici hapları (tüm fiziksel monitörler seçilebilir)
            _monitorPillsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            rootStack.Children.Add(_monitorPillsPanel);

            // 2b) Sanal monitör seçici hapları (sanal monitörler aktifse görünür)
            _areaPillsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            rootStack.Children.Add(_areaPillsPanel);

            // 3) Gerçek Fiziksel Tam Ekran (Silinemez) üst şeridi
            _permFullPreview = new LayoutPreviewControl
            {
                IsPermanentFull = true,
                Width = 44,
                Height = 26,
                Margin = new Thickness(0, 0, 8, 0)
            };

            _btnPhysicalFull = new Button
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            _btnPhysicalFull.MouseEnter += (s, e) =>
            {
                if (_currentArea?.Monitor != null)
                    _zonePreview.ShowRect(_currentArea.Monitor.WorkArea, true);
            };
            _btnPhysicalFull.MouseLeave += (s, e) => _zonePreview.HidePreview();
            _btnPhysicalFull.Click += (s, e) =>
            {
                if (_targetHwnd != IntPtr.Zero && _currentArea != null)
                {
                    WindowPlacer.PhysicalMaximize(_targetHwnd, _currentArea.Monitor);
                    HideFlyout();
                }
            };
            rootStack.Children.Add(_btnPhysicalFull);

            // 4) Yerleşim kutuları (monitör en/boy oranına duyarlı)
            _layoutsWrap = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            rootStack.Children.Add(_layoutsWrap);

            // 5) Alt bilgi ve hızlı butonlar
            var footerBorder = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(0, 8, 0, 0)
            };
            footerBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sigText = new TextBlock
            {
                Text = $"Snappy Layout {AppInfo.VersionText}  •  {AppInfo.Author} ({AppInfo.AuthorDateTr})",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            sigText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            var footerBtns = new StackPanel { Orientation = Orientation.Horizontal };
            _btnBorders = new Button
            {
                Content = "◫ " + Loc.T("FlyoutShowBorders"),
                Padding = new Thickness(8, 3, 8, 3),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _btnBorders.Click += (s, e) => App.Instance?.Overlay?.Flash();

            _btnOpenMain = new Button
            {
                Content = "⚙ " + Loc.T("FlyoutOpenApp"),
                Padding = new Thickness(8, 3, 8, 3),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            _btnOpenMain.Click += (s, e) =>
            {
                HideFlyout();
                App.Instance?.ShowMainWindow();
            };

            footerBtns.Children.Add(_btnBorders);
            footerBtns.Children.Add(_btnOpenMain);

            Grid.SetColumn(sigText, 0);
            Grid.SetColumn(footerBtns, 1);
            footerGrid.Children.Add(sigText);
            footerGrid.Children.Add(footerBtns);
            footerBorder.Child = footerGrid;
            rootStack.Children.Add(footerBorder);

            outerBorder.Child = rootStack;
            Content = outerBorder;

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
            var mon = MonitorService.FromPoint(anchorScreenPt);
            _selectedMonitor = _allMonitors.FirstOrDefault(m => m.Key == mon.Key) ?? mon;
            _monitorAreas = AreaService.GetAreas(_selectedMonitor);
            _currentArea = AreaService.AreaFromPoint(anchorScreenPt);
            if (_currentArea == null || _currentArea.Monitor.Key != _selectedMonitor.Key)
                _currentArea = _monitorAreas.First();

            RebuildUI();

            if (!IsVisible) Show();
            UpdateLayout();

            // Ekran sınırları içinde konumlandır (fiziksel piksel → WPF DIP)
            var hwnd = new WindowInteropHelper(this).Handle;
            double scale = mon.Scale;
            int flyW = (int)Math.Round(ActualWidth * scale);
            int flyH = (int)Math.Round(ActualHeight * scale);
            if (flyW < 260) flyW = (int)(480 * scale);
            if (flyH < 160) flyH = (int)(280 * scale);

            var frame = GetExtendedFrame(_targetHwnd);
            int x = anchorScreenPt.X - flyW / 2;
            // Windows orijinal Snap Layouts gibi maksimize tuşunu kapatmayacak şekilde
            // başlık çubuğunun / maksimize tuşunun hemen altına konumlandır
            int captionBottom = frame.Top + (int)Math.Round(34 * scale);
            int cursorBottom = anchorScreenPt.Y + (int)Math.Round(16 * scale);
            int y = (anchorScreenPt.Y >= frame.Top && anchorScreenPt.Y <= frame.Top + (int)Math.Round(72 * scale))
                ? Math.Max(captionBottom, cursorBottom)
                : cursorBottom;

            var wa = mon.WorkArea;
            if (x + flyW > wa.Right - 8) x = wa.Right - flyW - 8;
            if (x < wa.Left + 8) x = wa.Left + 8;
            if (y + flyH > wa.Bottom - 8) y = wa.Bottom - flyH - 8;
            if (y < wa.Top + 4) y = wa.Top + 4;

            NativeSnapSuppressor.DismissActiveNativeFlyout();
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            _autoCloseTimer.Start();
        }

        public void HideFlyout()
        {
            _autoCloseTimer.Stop();
            _zonePreview.HidePreview();
            if (IsVisible) Hide();
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

            var hitMax = MaximizeHoverWatcher.HitMaxButton(pt);
            if (hitMax == _targetHwnd && _targetHwnd != IntPtr.Zero) return;

            HideFlyout();
        }

        private void RebuildUI()
        {
            string winTitle = GetWindowTitle(_targetHwnd);
            if (string.IsNullOrWhiteSpace(winTitle)) winTitle = Loc.T("FlyoutHeader");
            _headerTitleText.Text = winTitle;
            _aspectInfoText.Text = $"{_currentArea.Label}  •  {_currentArea.Aspect:0.00}:1";
            _btnBorders.Content = "◫ " + Loc.T("FlyoutShowBorders");
            _btnOpenMain.Content = "⚙ " + Loc.T("FlyoutOpenApp");

            // Fiziksel monitör seçici hapları (tüm fiziksel monitörler)
            _monitorPillsPanel.Children.Clear();
            if (_allMonitors.Count > 1)
            {
                _monitorPillsPanel.Visibility = Visibility.Visible;
                var mLbl = new TextBlock
                {
                    Text = Loc.T("FlyoutPhysicalMonitor"),
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 4)
                };
                mLbl.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                _monitorPillsPanel.Children.Add(mLbl);

                foreach (var monItem in _allMonitors)
                {
                    bool isSelMon = _selectedMonitor != null && monItem.Key == _selectedMonitor.Key;
                    string star = monItem.IsPrimary ? " ★" : "";
                    var mPill = new Button
                    {
                        Content = $"🖥 {monItem.Index}. {monItem.FriendlyName}{star}",
                        Padding = new Thickness(9, 3, 9, 3),
                        Margin = new Thickness(0, 0, 5, 4),
                        FontSize = 11.5,
                        FontWeight = isSelMon ? FontWeights.Bold : FontWeights.Normal,
                        Cursor = Cursors.Hand
                    };
                    if (isSelMon)
                    {
                        mPill.SetResourceReference(Button.BackgroundProperty, "AccentSubtleBrush");
                        mPill.SetResourceReference(Button.BorderBrushProperty, "AccentBrush");
                    }
                    var capturedMon = monItem;
                    mPill.MouseEnter += (s, e) => _zonePreview.ShowRect(capturedMon.WorkArea, true);
                    mPill.MouseLeave += (s, e) => _zonePreview.HidePreview();
                    mPill.Click += (s, e) =>
                    {
                        _selectedMonitor = capturedMon;
                        _monitorAreas = AreaService.GetAreas(_selectedMonitor);
                        _currentArea = _monitorAreas.First();
                        _zonePreview.HidePreview();
                        RebuildUI();
                    };
                    _monitorPillsPanel.Children.Add(mPill);
                }
            }
            else
            {
                _monitorPillsPanel.Visibility = Visibility.Collapsed;
            }

            // Sanal monitör hapları
            _areaPillsPanel.Children.Clear();
            if (_monitorAreas.Count > 1)
            {
                _areaPillsPanel.Visibility = Visibility.Visible;
                var lbl = new TextBlock
                {
                    Text = Loc.T("FlyoutTargetArea"),
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 4)
                };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                _areaPillsPanel.Children.Add(lbl);

                foreach (var area in _monitorAreas)
                {
                    bool active = area.SegmentIndex == _currentArea.SegmentIndex;
                    var pill = new Button
                    {
                        Content = $"{Loc.T("VirtualShort")} {area.SegmentIndex + 1} ({area.Rect.Width}px)",
                        Padding = new Thickness(9, 3, 9, 3),
                        Margin = new Thickness(0, 0, 5, 4),
                        FontSize = 11.5,
                        FontWeight = active ? FontWeights.Bold : FontWeights.Normal,
                        Cursor = Cursors.Hand
                    };
                    if (active)
                    {
                        pill.SetResourceReference(Button.BackgroundProperty, "AccentSubtleBrush");
                        pill.SetResourceReference(Button.BorderBrushProperty, "AccentBrush");
                    }
                    var captured = area;
                    pill.MouseEnter += (s, e) => _zonePreview.ShowRect(captured.Rect, false);
                    pill.MouseLeave += (s, e) => _zonePreview.HidePreview();
                    pill.Click += (s, e) =>
                    {
                        _currentArea = captured;
                        _zonePreview.HidePreview();
                        RebuildUI();
                    };
                    _areaPillsPanel.Children.Add(pill);
                }
            }
            else
            {
                _areaPillsPanel.Visibility = Visibility.Collapsed;
            }

            // Gerçek Fiziksel Tam Ekran (Silinemez) üst düğmesi
            _permFullPreview.MonitorAspect = _currentArea.Monitor.Aspect;
            var physContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var physPreview = new LayoutPreviewControl
            {
                IsPermanentFull = true,
                MonitorAspect = _currentArea.Monitor.Aspect,
                Width = 42,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0),
                IsHitTestVisible = false
            };
            var physText = new TextBlock
            {
                Text = "⛶  " + Loc.T("FlyoutPhysicalFull"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            physContent.Children.Add(physPreview);
            physContent.Children.Add(physText);
            _btnPhysicalFull.Content = physContent;

            // Yerleşim düzeni kutuları (seçili fiziksel veya sanal monitöre özel düzenler ve gerçek en/boy oranı!)
            _layoutsWrap.Children.Clear();
            double aspect = _currentArea.Aspect;
            double boxH = 74;
            double boxW = Math.Clamp(boxH * Math.Clamp(aspect, 0.55, 3.2), 68, 220);

            // Sanal Monitör Tam Ekran (100%) her zaman alttaki yerleşim düzenlerinin EN BAŞINDA bir yerleşim kartı olarak gösterilir
            var vmFullCard = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(5),
                Padding = new Thickness(6, 6, 6, 4)
            };
            vmFullCard.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            vmFullCard.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");

            var vmFullSp = new StackPanel { Orientation = Orientation.Vertical };
            var vmFullPreview = new LayoutPreviewControl
            {
                Layout = null, // tek parça %100 sanal monitör tam ekran kutusu çizer
                MonitorAspect = aspect,
                IsEditor = false,
                Width = boxW,
                Height = boxH
            };
            vmFullPreview.ZoneHovered += relRect =>
            {
                if (relRect == null || _currentArea == null) _zonePreview.HidePreview();
                else _zonePreview.ShowRect(WindowPlacer.ZoneToPixels(_currentArea, new Rect(0, 0, 1, 1)), false);
            };
            vmFullPreview.ZoneClicked += (relRect, cell) =>
            {
                if (_targetHwnd != IntPtr.Zero && _currentArea != null)
                {
                    WindowPlacer.VirtualMaximize(_targetHwnd, _currentArea, allowToggle: false);
                    HideFlyout();
                }
            };

            string vmFullLabel = _currentArea.IsVirtual
                ? $"{Loc.T("FlyoutVirtualFull")} (#{_currentArea.SegmentIndex + 1})"
                : Loc.T("FlyoutVirtualFull");
            var vmFullText = new TextBlock
            {
                Text = vmFullLabel,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                MaxWidth = Math.Max(boxW, 115),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            vmFullText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

            vmFullSp.Children.Add(vmFullPreview);
            vmFullSp.Children.Add(vmFullText);
            vmFullCard.Child = vmFullSp;
            _layoutsWrap.Children.Add(vmFullCard);

            var areaLayouts = AreaService.GetLayoutsForArea(_currentArea);
            var layouts = areaLayouts.Where(l => l.IsVisibleFor(aspect)).ToList();
            if (layouts.Count == 0) layouts = areaLayouts.ToList();

            foreach (var layout in layouts)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(5),
                    Padding = new Thickness(6, 6, 6, 4)
                };
                card.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
                card.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

                var sp = new StackPanel { Orientation = Orientation.Vertical };
                var preview = new LayoutPreviewControl
                {
                    Layout = layout,
                    MonitorAspect = aspect,
                    IsEditor = false,
                    Width = boxW,
                    Height = boxH
                };
                preview.ZoneHovered += relRect =>
                {
                    if (relRect == null || _currentArea == null) _zonePreview.HidePreview();
                    else _zonePreview.ShowRect(WindowPlacer.ZoneToPixels(_currentArea, relRect.Value), false);
                };
                preview.ZoneClicked += (relRect, cell) =>
                {
                    if (_targetHwnd != IntPtr.Zero && _currentArea != null)
                    {
                        WindowPlacer.SnapToZone(_targetHwnd, _currentArea, relRect);
                        HideFlyout();
                    }
                };

                var nameText = new TextBlock
                {
                    Text = layout.DisplayTitle,
                    FontSize = 11,
                    MaxWidth = Math.Max(boxW, 110),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

                sp.Children.Add(preview);
                sp.Children.Add(nameText);
                card.Child = sp;
                _layoutsWrap.Children.Add(card);
            }
        }
    }

    /// <summary>
    /// Flyout içindeki bir kutunun üzerine gelindiğinde gerçek ekranda pencerenin yerleşeceği alanı
    /// yarı saydam bir çerçeveyle gösteren önizleme katmanı.
    /// </summary>
    internal sealed class SnapZonePreviewOverlay : Window
    {
        private readonly Border _box;

        public SnapZonePreviewOverlay()
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

            _box = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(3),
                Margin = new Thickness(4)
            };
            Content = _box;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            AddExStyle(hwnd, WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST);
        }

        public void ShowRect(RECT r, bool isPhysicalFull)
        {
            var col = isPhysicalFull ? Color.FromRgb(16, 185, 129) : Color.FromRgb(59, 130, 246);
            _box.Background = new SolidColorBrush(Color.FromArgb(65, col.R, col.G, col.B));
            _box.BorderBrush = new SolidColorBrush(Color.FromArgb(230, col.R, col.G, col.B));

            if (!IsVisible) Show();
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public void HidePreview()
        {
            if (IsVisible) Hide();
        }
    }
}
