using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SnappyLayout.Core;
using SnappyLayout.Models;
using SnappyLayout.Native;
using SnappyLayout.Services;
using SnappyLayout.UI.Controls;
using SnappyLayout.UI.Windows;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.UI
{
    public partial class MainWindow : Window
    {
        private int _activeTabIndex = 0;
        private SnapArea _selectedPreviewArea;
        private SnapLayout _selectedLayout;
        private bool _selectedVirtualFull = false;
        private bool _filterLandscape = true;
        private bool _filterUltrawide = true;
        private bool _filterPortrait = true;
        private MonitorInfo _selectedPhysicalMonitor;
        private bool _suppressQuickEvents;
        private bool _sidebarCollapsed = false;
        private bool? _manualSidebarCollapse = null;

        private static readonly (string Icon, string LocKey, string AccentBrushKey, string BgBrushKey)[] NavMeta =
        {
            ("⊞", "NavLayouts", "NavLayoutsBrush", "NavLayoutsBgBrush"),
            ("◫", "NavVirtualMonitors", "NavVirtualBrush", "NavVirtualBgBrush"),
            ("🚀", "NavWorkspaces", "NavWorkspacesBrush", "NavWorkspacesBgBrush"),
            ("⌨", "NavHotkeys", "NavHotkeysBrush", "NavHotkeysBgBrush"),
            ("⚙", "NavSettings", "NavSettingsBrush", "NavSettingsBgBrush"),
            ("❓", "NavHelp", "NavHelpBrush", "NavHelpBgBrush"),
        };

        public MainWindow()
        {
            InitializeComponent();

            SidebarVersionText.Text = AppInfo.VersionText;
            Title = $"Snappy Layouts {AppInfo.VersionText}";

            var mons = MonitorService.GetMonitors(true);
            _selectedPhysicalMonitor = mons.FirstOrDefault(m => m.IsPrimary) ?? mons.First();
            _selectedPreviewArea = AreaService.GetAreas(_selectedPhysicalMonitor).First();
            _selectedLayout = AreaService.GetLayoutsForArea(_selectedPreviewArea).FirstOrDefault();

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                WindowPlacer.MainWindowHandle = hwnd;
                HwndSource.FromHwnd(hwnd)?.AddHook(MainWindowWndProc);
                ThemeManager.ApplyWindowChrome(this);
            };

            Loaded += (s, e) =>
            {
                WindowPlacer.MainWindowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                ThemeManager.ApplyWindowChrome(this);
                UpdateResponsiveSidebar();
                RefreshSidebarChrome();
                RebuildAllPages();
                SelectTab(0);
            };

            SizeChanged += (s, e) => UpdateResponsiveSidebar();

            Loc.LanguageChanged += () => Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshSidebarChrome();
                RebuildAllPages();
            }));

            ThemeManager.ThemeChanged += () => Dispatcher.BeginInvoke(new Action(() =>
            {
                ThemeManager.ApplyWindowChrome(this);
                RefreshSidebarChrome();
                RebuildAllPages();
            }));
        }

        private IntPtr MainWindowWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                IntPtr hit = DefWindowProc(hwnd, msg, wParam, lParam);
                if (hit.ToInt64() == HTMAXBUTTON)
                {
                    MaximizeHoverWatcher.NotifySelfMaxHover(hwnd);
                    bool flyoutActive = (Storage.Data?.Settings?.FlyoutEnabled ?? true) && !(App.Instance?.Paused ?? false);
                    if (flyoutActive && !IsKeyDown(VK_LBUTTON) && !IsKeyDown(VK_RBUTTON))
                    {
                        // Fare tuşu basılı değilken Windows 11'in HTMAXBUTTON hover algılayıp
                        // kendi orijinal Snap Layouts menüsünü açmasını tamamen engelle!
                        handled = true;
                        return new IntPtr(HTBORDER);
                    }
                    handled = true;
                    return hit;
                }
            }
            return IntPtr.Zero;
        }

        private void OnToggleSidebarClick(object sender, RoutedEventArgs e)
        {
            _manualSidebarCollapse = !_sidebarCollapsed;
            ApplySidebarCollapsed(_manualSidebarCollapse.Value);
        }

        private void UpdateResponsiveSidebar()
        {
            bool shouldCollapse = _manualSidebarCollapse ?? (ActualWidth > 0 && ActualWidth < 960);
            if (ActualWidth > 0 && ActualWidth < 820) shouldCollapse = true;
            if (shouldCollapse != _sidebarCollapsed)
            {
                ApplySidebarCollapsed(shouldCollapse);
            }
        }

        private void ApplySidebarCollapsed(bool collapsed)
        {
            _sidebarCollapsed = collapsed;
            // Sabit sol/sağ padding (12px) + 1px border + 53px ikon sütunu = 78px -> tüm ikonların X,Y konumu sabit ve kırpılmasız!
            SidebarCol.Width = new GridLength(collapsed ? 78 : 265);
            SidebarBrandText.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            BtnToggleSidebar.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            QuickLangCombo.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            QuickThemeCombo.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            RefreshNavButtonContents();
            RefreshFooterButtons();
        }

        private void RefreshNavButtonContents()
        {
            var btns = new[] { BtnNavLayouts, BtnNavVirtual, BtnNavWorkspaces, BtnNavHotkeys, BtnNavSettings, BtnNavHelp };
            for (int i = 0; i < btns.Length; i++)
            {
                var meta = NavMeta[i];
                string title = Loc.T(meta.LocKey);
                btns[i].ToolTip = title;
                btns[i].Height = 46;
                btns[i].Padding = new Thickness(9, 6, 9, 6);
                btns[i].HorizontalContentAlignment = HorizontalAlignment.Left;

                var iconBadge = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBadge.SetResourceReference(Border.BackgroundProperty, meta.BgBrushKey);
                iconBadge.SetResourceReference(Border.BorderBrushProperty, meta.AccentBrushKey);
                iconBadge.BorderThickness = new Thickness(1);

                var iconText = new TextBlock
                {
                    Text = meta.Icon,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconText.SetResourceReference(TextBlock.ForegroundProperty, meta.AccentBrushKey);
                iconBadge.Child = iconText;

                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(iconBadge);
                var lbl = new TextBlock
                {
                    Text = title,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible
                };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                sp.Children.Add(lbl);
                btns[i].Content = sp;
            }
        }

        private void RefreshFooterButtons()
        {
            bool paused = App.Instance?.Paused ?? false;

            // 1) Dil bayrağı ikonu (System seçiliyse tespit edilen aktif dilin bayrağını göster)
            var activeLang = Storage.Data.Settings.Language;
            var flagLang = activeLang == LanguageChoice.System ? ResolveLanguageChoiceFromCode(Loc.CurrentCode) : activeLang;
            BtnFooterLangIcon.Content = BuildFlagIcon(flagLang);
            BtnFooterLangIcon.ToolTip = Loc.T("SetLanguage");

            // 2) Tema ikonu (System / Light / Dark)
            var activeTheme = Storage.Data.Settings.Theme;
            BtnFooterThemeIcon.Content = activeTheme switch
            {
                ThemeChoice.Light => "☀",
                ThemeChoice.Dark => "🌙",
                _ => ThemeManager.IsDark ? "🌙" : "☀"
            };
            BtnFooterThemeIcon.ToolTip = Loc.T("SetTheme");
            BtnFooterThemeIcon.SetResourceReference(Control.ForegroundProperty, activeTheme == ThemeChoice.Light ? "NavHotkeysBrush" : "NavWorkspacesBrush");

            // 3) Sınır Çizgileri butonu (ikon her iki modda aynı X,Y konumunda)
            var bPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bPanel.Children.Add(new TextBlock
            {
                Text = "◫",
                Width = 26,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            bPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("VmShowBordersNow"),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible
            });
            BtnQuickShowBorders.Content = bPanel;
            BtnQuickShowBorders.ToolTip = Loc.T("VmShowBordersNow");
            BtnQuickShowBorders.SetResourceReference(Control.BackgroundProperty, "NavVirtualBgBrush");
            BtnQuickShowBorders.SetResourceReference(Control.BorderBrushProperty, "NavVirtualBrush");
            BtnQuickShowBorders.SetResourceReference(Control.ForegroundProperty, "NavVirtualBrush");

            // 4) Duraklat / Devam Et butonu (ikon her iki modda aynı X,Y konumunda)
            var pPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            pPanel.Children.Add(new TextBlock
            {
                Text = paused ? "▶" : "⏸",
                Width = 26,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            pPanel.Children.Add(new TextBlock
            {
                Text = paused ? Loc.T("Resume") : Loc.T("Pause"),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible
            });
            BtnPauseResume.Content = pPanel;
            BtnPauseResume.ToolTip = paused ? Loc.T("Resume") : Loc.T("Pause");
            BtnPauseResume.SetResourceReference(Control.BackgroundProperty, paused ? "NavSettingsBgBrush" : "NavHotkeysBgBrush");
            BtnPauseResume.SetResourceReference(Control.BorderBrushProperty, paused ? "NavSettingsBrush" : "NavHotkeysBrush");
            BtnPauseResume.SetResourceReference(Control.ForegroundProperty, paused ? "NavSettingsBrush" : "NavHotkeysBrush");
        }

        private static LanguageChoice ResolveLanguageChoiceFromCode(string code) => code switch
        {
            "tr" => LanguageChoice.Turkish,
            "es" => LanguageChoice.Spanish,
            "pt" => LanguageChoice.Portuguese,
            "zh" => LanguageChoice.Chinese,
            "fr" => LanguageChoice.French,
            "de" => LanguageChoice.German,
            "ru" => LanguageChoice.Russian,
            _ => LanguageChoice.English
        };

        private void OnFooterLangIconClick(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            foreach (var (choice, nativeName) in LanguageEntries)
            {
                var captured = choice;
                string label = choice == LanguageChoice.System ? Loc.T("LangSystem") : nativeName;
                var mi = new MenuItem
                {
                    Header = label,
                    Icon = BuildFlagIcon(choice),
                    IsChecked = Storage.Data.Settings.Language == choice
                };
                mi.Click += (s, ev) =>
                {
                    Storage.Data.Settings.Language = captured;
                    Storage.Save();
                    Loc.Apply(captured);
                };
                menu.Items.Add(mi);
            }
            menu.PlacementTarget = BtnFooterLangIcon;
            menu.IsOpen = true;
        }

        private void OnFooterThemeIconClick(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var themes = new[]
            {
                (ThemeChoice.System, "🖥  " + Loc.T("ThemeSystem")),
                (ThemeChoice.Light, "☀  " + Loc.T("ThemeLight")),
                (ThemeChoice.Dark, "🌙  " + Loc.T("ThemeDark"))
            };
            foreach (var (choice, label) in themes)
            {
                var captured = choice;
                var mi = new MenuItem
                {
                    Header = label,
                    IsChecked = Storage.Data.Settings.Theme == choice
                };
                mi.Click += (s, ev) =>
                {
                    Storage.Data.Settings.Theme = captured;
                    Storage.Save();
                    ThemeManager.Apply(captured);
                };
                menu.Items.Add(mi);
            }
            menu.PlacementTarget = BtnFooterThemeIcon;
            menu.IsOpen = true;
        }

        public void RefreshSidebarChrome()
        {
            _suppressQuickEvents = true;
            SidebarVersionText.Text = AppInfo.VersionText;
            bool paused = App.Instance?.Paused ?? false;
            SidebarStatusText.Text = paused ? Loc.T("Paused") : Loc.T("Active");
            SidebarStatusText.SetResourceReference(TextBlock.ForegroundProperty, paused ? "WarningBrush" : "PermanentBrush");

            RefreshNavButtonContents();
            PopulateLanguageCombo(QuickLangCombo, Storage.Data.Settings.Language);

            QuickThemeCombo.Items.Clear();
            QuickThemeCombo.Items.Add(Loc.T("ThemeSystem"));
            QuickThemeCombo.Items.Add(Loc.T("ThemeLight"));
            QuickThemeCombo.Items.Add(Loc.T("ThemeDark"));
            QuickThemeCombo.SelectedIndex = (int)Storage.Data.Settings.Theme;

            RefreshFooterButtons();

            _suppressQuickEvents = false;
            UpdateNavButtonHighlight();
        }

        private static readonly (LanguageChoice Choice, string NativeName)[] LanguageEntries =
        {
            (LanguageChoice.System, ""),
            (LanguageChoice.English, "English"),
            (LanguageChoice.Turkish, "Türkçe"),
            (LanguageChoice.Spanish, "Español"),
            (LanguageChoice.Portuguese, "Português"),
            (LanguageChoice.Chinese, "中文 (简体)"),
            (LanguageChoice.French, "Français"),
            (LanguageChoice.German, "Deutsch"),
            (LanguageChoice.Russian, "Русский"),
        };

        private static void PopulateLanguageCombo(ComboBox combo, LanguageChoice selected)
        {
            combo.Items.Clear();
            int selIdx = 1; // Default English
            for (int i = 0; i < LanguageEntries.Length; i++)
            {
                var (choice, nativeName) = LanguageEntries[i];
                string label = choice == LanguageChoice.System ? Loc.T("LangSystem") : nativeName;

                var itemPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                itemPanel.Children.Add(BuildFlagIcon(choice));
                var txt = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(7, 0, 0, 0)
                };
                txt.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                itemPanel.Children.Add(txt);

                var cbi = new ComboBoxItem { Content = itemPanel, Tag = choice };
                combo.Items.Add(cbi);
                if (choice == selected) selIdx = i;
            }
            combo.SelectedIndex = selIdx;
        }

        private static UIElement BuildFlagIcon(LanguageChoice lang)
        {
            const double W = 22.0, H = 15.0;
            var border = new Border
            {
                Width = W,
                Height = H,
                CornerRadius = new CornerRadius(2.5),
                BorderThickness = new Thickness(0.6),
                BorderBrush = new SolidColorBrush(Color.FromArgb(130, 128, 128, 128)),
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (lang == LanguageChoice.System)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                border.Child = new TextBlock
                {
                    Text = "🌐",
                    FontSize = 10,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return border;
            }

            static SolidColorBrush B(string hex)
            {
                var br = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                br.Freeze();
                return br;
            }

            static void AddRect(Canvas c, double x, double y, double w, double h, Brush fill)
            {
                var r = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = fill };
                Canvas.SetLeft(r, x);
                Canvas.SetTop(r, y);
                c.Children.Add(r);
            }

            static void AddEllipse(Canvas c, double x, double y, double w, double h, Brush fill)
            {
                var el = new System.Windows.Shapes.Ellipse { Width = w, Height = h, Fill = fill };
                Canvas.SetLeft(el, x);
                Canvas.SetTop(el, y);
                c.Children.Add(el);
            }

            static void AddStar(Canvas c, double cx, double cy, double outerR, double innerR, Brush fill, double rotDeg = -90)
            {
                var pts = new PointCollection();
                for (int i = 0; i < 10; i++)
                {
                    double ang = (rotDeg + i * 36.0) * Math.PI / 180.0;
                    double rad = (i % 2 == 0) ? outerR : innerR;
                    pts.Add(new Point(cx + Math.Cos(ang) * rad, cy + Math.Sin(ang) * rad));
                }
                c.Children.Add(new System.Windows.Shapes.Polygon { Points = pts, Fill = fill });
            }

            var canvas = new Canvas { Width = W, Height = H };

            switch (lang)
            {
                case LanguageChoice.English: // United Kingdom (Union Jack) Flag
                {
                    var navy = B("#012169");
                    var red = B("#C8102E");
                    AddRect(canvas, 0, 0, W, H, navy);
                    // White diagonal saltire (St Andrew's Cross)
                    canvas.Children.Add(new System.Windows.Shapes.Line { X1 = 0, Y1 = 0, X2 = W, Y2 = H, Stroke = Brushes.White, StrokeThickness = 3.2 });
                    canvas.Children.Add(new System.Windows.Shapes.Line { X1 = W, Y1 = 0, X2 = 0, Y2 = H, Stroke = Brushes.White, StrokeThickness = 3.2 });
                    // Red diagonal saltire (St Patrick's Cross)
                    canvas.Children.Add(new System.Windows.Shapes.Line { X1 = 0, Y1 = 0, X2 = W, Y2 = H, Stroke = red, StrokeThickness = 1.3 });
                    canvas.Children.Add(new System.Windows.Shapes.Line { X1 = W, Y1 = 0, X2 = 0, Y2 = H, Stroke = red, StrokeThickness = 1.3 });
                    // White fimbriation for St George's Cross
                    AddRect(canvas, (W - 5.0) / 2.0, 0, 5.0, H, Brushes.White);
                    AddRect(canvas, 0, (H - 5.0) / 2.0, W, 5.0, Brushes.White);
                    // Red St George's Cross
                    AddRect(canvas, (W - 2.8) / 2.0, 0, 2.8, H, red);
                    AddRect(canvas, 0, (H - 2.8) / 2.0, W, 2.8, red);
                    break;
                }
                case LanguageChoice.Turkish: // Turkey Flag (official proportions + 5-pointed star)
                {
                    var red = B("#E30A17");
                    AddRect(canvas, 0, 0, W, H, red);
                    AddEllipse(canvas, 4.2, 3.3, 8.4, 8.4, Brushes.White);
                    AddEllipse(canvas, 5.9, 4.1, 6.8, 6.8, red);
                    AddStar(canvas, 13.8, 7.5, 2.3, 0.95, Brushes.White, -18);
                    break;
                }
                case LanguageChoice.Spanish: // Spain Flag (1:2:1 + Coat of Arms / Escudo)
                {
                    var red = B("#AA151B");
                    var gold = B("#F1BF00");
                    AddRect(canvas, 0, 0, W, H * 0.25, red);
                    AddRect(canvas, 0, H * 0.25, W, H * 0.50, gold);
                    AddRect(canvas, 0, H * 0.75, W, H * 0.25, red);
                    // Coat of arms (Escudo de España) on left-center of yellow band
                    AddRect(canvas, 4.3, 5.0, 0.8, 4.6, B("#B8860B")); // Left pillar
                    AddRect(canvas, 8.4, 5.0, 0.8, 4.6, B("#B8860B")); // Right pillar
                    AddRect(canvas, 5.3, 5.4, 2.9, 3.8, red);          // Shield body
                    AddRect(canvas, 6.7, 5.4, 1.5, 1.9, B("#E5E7EB")); // Shield quarter
                    AddRect(canvas, 5.3, 7.3, 1.4, 1.9, gold);         // Shield quarter
                    AddEllipse(canvas, 6.2, 6.8, 1.2, 1.2, B("#1D4ED8")); // Center escutcheon
                    AddRect(canvas, 5.4, 4.3, 2.7, 1.0, B("#B45309")); // Crown
                    break;
                }
                case LanguageChoice.Portuguese: // Portugal Flag (2:3 Green/Red + Armillary Sphere & Shield)
                {
                    AddRect(canvas, 0, 0, W * 0.40, H, B("#006600"));
                    AddRect(canvas, W * 0.40, 0, W * 0.60, H, B("#FF0000"));
                    double cx = W * 0.40;
                    AddEllipse(canvas, cx - 3.1, H / 2.0 - 3.1, 6.2, 6.2, B("#FFCC00"));
                    AddRect(canvas, cx - 1.6, H / 2.0 - 1.9, 3.2, 3.6, B("#DC2626"));
                    AddRect(canvas, cx - 1.0, H / 2.0 - 1.2, 2.0, 2.2, Brushes.White);
                    AddEllipse(canvas, cx - 0.5, H / 2.0 - 0.5, 1.0, 1.0, B("#1D4ED8"));
                    break;
                }
                case LanguageChoice.Chinese: // China Flag (Red + 5 official yellow stars)
                {
                    var gold = B("#FFDE00");
                    AddRect(canvas, 0, 0, W, H, B("#DE2910"));
                    AddStar(canvas, 4.5, 4.5, 2.5, 1.0, gold);
                    AddStar(canvas, 8.6, 1.9, 0.95, 0.4, gold);
                    AddStar(canvas, 10.1, 3.6, 0.95, 0.4, gold);
                    AddStar(canvas, 10.1, 5.9, 0.95, 0.4, gold);
                    AddStar(canvas, 8.6, 7.5, 0.95, 0.4, gold);
                    break;
                }
                case LanguageChoice.French: // France Flag (Blue - White - Red vertical)
                {
                    double tw = W / 3.0;
                    AddRect(canvas, 0, 0, tw + 0.2, H, B("#002395"));
                    AddRect(canvas, tw, 0, tw + 0.2, H, Brushes.White);
                    AddRect(canvas, tw * 2, 0, tw + 0.2, H, B("#ED2939"));
                    break;
                }
                case LanguageChoice.German: // Germany Flag (Black - Red - Gold horizontal)
                {
                    double th = H / 3.0;
                    AddRect(canvas, 0, 0, W, th + 0.2, B("#111111"));
                    AddRect(canvas, 0, th, W, th + 0.2, B("#DD0000"));
                    AddRect(canvas, 0, th * 2, W, th + 0.2, B("#FFCE00"));
                    break;
                }
                case LanguageChoice.Russian: // Russia Flag (White - Blue - Red horizontal)
                {
                    double th = H / 3.0;
                    AddRect(canvas, 0, 0, W, th + 0.2, Brushes.White);
                    AddRect(canvas, 0, th, W, th + 0.2, B("#0039A6"));
                    AddRect(canvas, 0, th * 2, W, th + 0.2, B("#D52B1E"));
                    break;
                }
            }

            border.Child = canvas;
            return border;
        }

        private void OnNavClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && int.TryParse(b.Tag?.ToString(), out int idx))
                SelectTab(idx);
        }

        public void SelectTab(int index)
        {
            _activeTabIndex = index;
            PageLayouts.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            PageVirtual.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            PageWorkspaces.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            PageHotkeys.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
            PageHelp.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
            UpdateNavButtonHighlight();
        }

        private void UpdateNavButtonHighlight()
        {
            var btns = new[] { BtnNavLayouts, BtnNavVirtual, BtnNavWorkspaces, BtnNavHotkeys, BtnNavSettings, BtnNavHelp };
            for (int i = 0; i < btns.Length; i++)
            {
                bool active = i == _activeTabIndex;
                btns[i].FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
                if (active)
                {
                    btns[i].SetResourceReference(Control.BackgroundProperty, NavMeta[i].BgBrushKey);
                    btns[i].SetResourceReference(Control.BorderBrushProperty, NavMeta[i].AccentBrushKey);
                }
                else
                {
                    btns[i].SetResourceReference(Control.BackgroundProperty, "ControlBgBrush");
                    btns[i].SetResourceReference(Control.BorderBrushProperty, "ControlBorderBrush");
                }
            }
        }

        private void OnQuickLangChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressQuickEvents || QuickLangCombo.SelectedIndex < 0) return;
            if (QuickLangCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is LanguageChoice choice)
            {
                Storage.Data.Settings.Language = choice;
                Storage.Save();
                Loc.Apply(choice);
            }
        }

        private void OnQuickThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressQuickEvents || QuickThemeCombo.SelectedIndex < 0) return;
            Storage.Data.Settings.Theme = (ThemeChoice)QuickThemeCombo.SelectedIndex;
            Storage.Save();
            ThemeManager.Apply(Storage.Data.Settings.Theme);
        }

        private void OnQuickShowBorders(object sender, RoutedEventArgs e) => App.Instance?.Overlay?.Flash();

        private void OnPauseResumeClick(object sender, RoutedEventArgs e)
        {
            if (App.Instance == null) return;
            App.Instance.SetPaused(!App.Instance.Paused);
            RefreshSidebarChrome();
        }

        public void RebuildAllPages()
        {
            HostLayouts.Content = BuildLayoutsPage();
            HostVirtual.Content = BuildVirtualMonitorsPage();
            HostWorkspaces.Content = BuildWorkspacesPage();
            HostHotkeys.Content = BuildHotkeysPage();
            HostSettings.Content = BuildSettingsPage();
            HostHelp.Content = BuildHelpPage();
        }

        // =========================================================================
        // SAYFA 0: YERLEŞİM DÜZENLERİ (SNAP LAYOUTS) — HER FİZİKSEL / SANAL MONİTÖRE ÖZEL
        // =========================================================================
        private UIElement BuildLayoutsPage()
        {
            var root = new StackPanel();
            root.Children.Add(MakePageHeader(Loc.T("LayoutsTitle"), Loc.T("LayoutsSubtitle")));

            var monitors = MonitorService.GetMonitors(true);
            if (_selectedPhysicalMonitor == null || !monitors.Any(m => m.Key == _selectedPhysicalMonitor.Key))
                _selectedPhysicalMonitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.First();
            else
                _selectedPhysicalMonitor = monitors.First(m => m.Key == _selectedPhysicalMonitor.Key);

            // Seçili fiziksel monitördeki aktif hedef alanlar:
            // Sanal monitörler oluşturulmuşsa yalnızca sanal monitörler (#1, #2, ...); yoksa fiziksel monitör.
            var monAreas = AreaService.GetAreas(_selectedPhysicalMonitor);
            if (_selectedPreviewArea == null ||
                _selectedPreviewArea.Monitor.Key != _selectedPhysicalMonitor.Key ||
                !monAreas.Any(a => a.SegmentIndex == _selectedPreviewArea.SegmentIndex && a.SegmentCount == _selectedPreviewArea.SegmentCount))
            {
                _selectedPreviewArea = monAreas.First();
            }
            else
            {
                _selectedPreviewArea = monAreas.First(a => a.SegmentIndex == _selectedPreviewArea.SegmentIndex);
            }

            var currentAreaLayouts = AreaService.GetLayoutsForArea(_selectedPreviewArea);

            // Üst Kart: 1. Adım Fiziksel Monitör Seçimi + 2. Adım Sanal Monitör Seçimi
            var topBar = MakeCard();
            var topStack = new StackPanel();

            var step1Header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            step1Header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            step1Header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            step1Header.Children.Add(MakeSectionLabel(Loc.T("Step1PhysicalMonitor")));
            var btnTestFlyout = new Button
            {
                Content = "✨ " + Loc.T("TestFlyoutBtn"),
                Padding = new Thickness(12, 6, 12, 6),
                FontWeight = FontWeights.SemiBold
            };
            btnTestFlyout.SetResourceReference(Control.BackgroundProperty, "NavWorkspacesBgBrush");
            btnTestFlyout.SetResourceReference(Control.BorderBrushProperty, "NavWorkspacesBrush");
            btnTestFlyout.SetResourceReference(Control.ForegroundProperty, "NavWorkspacesBrush");
            btnTestFlyout.Click += (s, e) => App.Instance?.ShowFlyoutForForegroundOrSelf();
            Grid.SetColumn(btnTestFlyout, 1);
            step1Header.Children.Add(btnTestFlyout);
            topStack.Children.Add(step1Header);

            var monWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var m in monitors)
            {
                bool isSelMon = m.Key == _selectedPhysicalMonitor.Key;
                var mCard = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(isSelMon ? 2.2 : 1.2),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 10, 6),
                    Cursor = Cursors.Hand
                };
                mCard.SetResourceReference(Border.BackgroundProperty, isSelMon ? "PhysMonSelectedBgBrush" : "PhysMonBgBrush");
                mCard.SetResourceReference(Border.BorderBrushProperty, isSelMon ? "PhysMonSelectedBorderBrush" : "PhysMonBorderBrush");

                int vmCnt = AreaService.GetAreas(m).Count;
                var mGrid = new Grid();
                mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var mIconBadge = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(7),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = isSelMon ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)) : Brushes.Transparent,
                    BorderThickness = new Thickness(isSelMon ? 0 : 1)
                };
                if (!isSelMon)
                    mIconBadge.SetResourceReference(Border.BorderBrushProperty, "PhysMonBorderBrush");
                var mIconTb = new TextBlock
                {
                    Text = "🖥",
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                mIconTb.SetResourceReference(TextBlock.ForegroundProperty, isSelMon ? "PhysMonSelectedFgBrush" : "PhysMonAccentBrush");
                mIconBadge.Child = mIconTb;

                var mSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var mTitle = new TextBlock { Text = m.DisplayName, FontWeight = FontWeights.Bold, FontSize = 13 };
                mTitle.SetResourceReference(TextBlock.ForegroundProperty, isSelMon ? "PhysMonSelectedFgBrush" : "TextPrimaryBrush");
                var mSub = new TextBlock
                {
                    Text = vmCnt > 1
                        ? $"◫ {vmCnt} {Loc.T("VirtualMonitor")}"
                        : $"{Loc.T("FullPhysicalMonitor")} ({m.AspectLabel})",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                mSub.SetResourceReference(TextBlock.ForegroundProperty, isSelMon ? "PhysMonSelectedSubFgBrush" : "PhysMonAccentBrush");
                mSp.Children.Add(mTitle);
                mSp.Children.Add(mSub);

                var capturedM = m;
                var btnRenM = new Button
                {
                    Content = "✎",
                    ToolTip = Loc.T("RenameMonitor"),
                    Padding = new Thickness(6, 2, 6, 2),
                    FontSize = 11.5,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                btnRenM.Click += (s, e) =>
                {
                    e.Handled = true;
                    ShowRenameMonitorDialog(capturedM);
                };

                Grid.SetColumn(mIconBadge, 0);
                Grid.SetColumn(mSp, 1);
                Grid.SetColumn(btnRenM, 2);
                mGrid.Children.Add(mIconBadge);
                mGrid.Children.Add(mSp);
                mGrid.Children.Add(btnRenM);
                mCard.Child = mGrid;

                mCard.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedPhysicalMonitor = capturedM;
                    _selectedPreviewArea = AreaService.GetAreas(capturedM).First();
                    _selectedVirtualFull = false;
                    _selectedLayout = AreaService.GetLayoutsForArea(_selectedPreviewArea).FirstOrDefault();
                    HostLayouts.Content = BuildLayoutsPage();
                };
                monWrap.Children.Add(mCard);
            }
            topStack.Children.Add(monWrap);

            // 2. Adım: Bu fiziksel monitörde sanal monitörler varsa hangi sanal monitör için pencere düzeni ayarlandığını seçtir
            if (monAreas.Count > 1)
            {
                var step2Header = new Grid { Margin = new Thickness(0, 4, 0, 6) };
                step2Header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                step2Header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                step2Header.Children.Add(MakeSectionLabel(Loc.T("Step2VirtualMonitor")));

                var btnCopySiblings = new Button
                {
                    Content = Loc.T("CopyLayoutsToSiblingsBtn"),
                    Padding = new Thickness(10, 4, 10, 4),
                    FontSize = 11.5
                };
                btnCopySiblings.Click += (s, e) =>
                {
                    AreaService.CopyLayoutsToSiblingVirtualMonitors(_selectedPreviewArea);
                    MessageBox.Show(this, Loc.T("CopiedToSiblingsMsg"), "Snappy Layouts", MessageBoxButton.OK, MessageBoxImage.Information);
                };
                Grid.SetColumn(btnCopySiblings, 1);
                step2Header.Children.Add(btnCopySiblings);
                topStack.Children.Add(step2Header);

                var vmWrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 8) };
                foreach (var area in monAreas)
                {
                    bool isSelArea = area.SegmentIndex == _selectedPreviewArea.SegmentIndex;
                    var aCard = new Border
                    {
                        CornerRadius = new CornerRadius(18),
                        BorderThickness = new Thickness(isSelArea ? 2 : 1.2),
                        Padding = new Thickness(14, 6, 14, 6),
                        Margin = new Thickness(0, 0, 8, 6),
                        Cursor = Cursors.Hand
                    };
                    aCard.SetResourceReference(Border.BackgroundProperty, isSelArea ? "VirtMonSelectedBgBrush" : "VirtMonBgBrush");
                    aCard.SetResourceReference(Border.BorderBrushProperty, isSelArea ? "VirtMonSelectedBorderBrush" : "VirtMonBorderBrush");

                    var aRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var aIcon = new TextBlock
                    {
                        Text = "◫",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    aIcon.SetResourceReference(TextBlock.ForegroundProperty, isSelArea ? "VirtMonSelectedFgBrush" : "VirtMonAccentBrush");

                    var aTxt = new TextBlock
                    {
                        Text = area.ShortSegmentLabel,
                        FontWeight = isSelArea ? FontWeights.Bold : FontWeights.Medium,
                        FontSize = 11.5,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    aTxt.SetResourceReference(TextBlock.ForegroundProperty, isSelArea ? "VirtMonSelectedFgBrush" : "VirtMonFgBrush");

                    aRow.Children.Add(aIcon);
                    aRow.Children.Add(aTxt);
                    aCard.Child = aRow;

                    var capturedArea = area;
                    aCard.MouseLeftButtonDown += (s, e) =>
                    {
                        _selectedPreviewArea = capturedArea;
                        _selectedVirtualFull = false;
                        _selectedLayout = AreaService.GetLayoutsForArea(capturedArea).FirstOrDefault();
                        HostLayouts.Content = BuildLayoutsPage();
                    };
                    vmWrap.Children.Add(aCard);
                }
                topStack.Children.Add(vmWrap);
            }
            else
            {
                var noVmRow = new Grid { Margin = new Thickness(0, 2, 0, 4) };
                noVmRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                noVmRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var noVmTb = new TextBlock
                {
                    Text = Loc.T("NoVirtualOnThisMonitor"),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                noVmTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

                var btnGoVm = new Button
                {
                    Content = Loc.T("GoToVirtualMonitorsBtn"),
                    Padding = new Thickness(10, 4, 10, 4),
                    FontSize = 11.5
                };
                btnGoVm.Click += (s, e) => SelectTab(1);

                Grid.SetColumn(noVmTb, 0);
                Grid.SetColumn(btnGoVm, 1);
                noVmRow.Children.Add(noVmTb);
                noVmRow.Children.Add(btnGoVm);
                topStack.Children.Add(noVmRow);
            }

            // Aktif hedef bilgi şeridi
            var targetBanner = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 4, 0, 0)
            };
            targetBanner.SetResourceReference(Border.BackgroundProperty, _selectedPreviewArea.IsVirtual ? "VirtMonBgBrush" : "PhysMonBgBrush");
            targetBanner.SetResourceReference(Border.BorderBrushProperty, _selectedPreviewArea.IsVirtual ? "VirtMonBorderBrush" : "PhysMonBorderBrush");

            var bannerTb = new TextBlock
            {
                Text = $"🎯 {Loc.T("ActiveTargetBanner")}  {_selectedPreviewArea.Label}   •   {Loc.T("AspectRatio")}: {_selectedPreviewArea.AspectLabel}   •   {Loc.T("DpiScale")}: {Math.Round(_selectedPreviewArea.Monitor.Scale * 100)}%",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            };
            bannerTb.SetResourceReference(TextBlock.ForegroundProperty, _selectedPreviewArea.IsVirtual ? "VirtMonAccentBrush" : "PhysMonAccentBrush");
            targetBanner.Child = bannerTb;
            topStack.Children.Add(targetBanner);

            topBar.Child = topStack;
            root.Children.Add(topBar);

            // Ana 2 sütunlu alan: Sol = Liste + Silinemez Tam Ekran, Sağ = Etkileşimli Izgara Düzenleyici
            var mainGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // --- SOL SÜTUN: SİLİNEMEZ GERÇEK TAM EKRAN + SEÇİLİ ALANIN PENCERE DÜZENİ LİSTESİ ---
            var leftStack = new StackPanel();

            // 1) Silinemez Gerçek Fiziksel Tam Ekran Kartı
            var permCard = MakeCard();
            permCard.SetResourceReference(Border.BorderBrushProperty, "PermanentBrush");
            permCard.BorderThickness = new Thickness(1.5);
            var permGrid = new Grid();
            permGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            permGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            permGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var permPreview = new LayoutPreviewControl
            {
                IsPermanentFull = true,
                MonitorAspect = _selectedPreviewArea.Monitor.Aspect,
                Width = 76,
                Height = 46,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var permInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var permTitleRow = new WrapPanel { Orientation = Orientation.Horizontal };
            var permTitle = new TextBlock
            {
                Text = Loc.T("PermFullTitle"),
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 6, 2)
            };
            permTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var permBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 0, 2)
            };
            permBadge.SetResourceReference(Border.BackgroundProperty, "PermanentSubtleBrush");
            var permBadgeTxt = new TextBlock { Text = "🔒 " + Loc.T("PermFullBadge"), FontSize = 10, FontWeight = FontWeights.Bold };
            permBadgeTxt.SetResourceReference(TextBlock.ForegroundProperty, "PermanentBrush");
            permBadge.Child = permBadgeTxt;

            permTitleRow.Children.Add(permTitle);
            permTitleRow.Children.Add(permBadge);
            permInfo.Children.Add(permTitleRow);

            var permDesc = new TextBlock
            {
                Text = Loc.T("PermFullDesc"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            permDesc.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            permInfo.Children.Add(permDesc);

            var btnLockedDel = new Button
            {
                Content = "🔒",
                ToolTip = Loc.T("CannotDeletePermanent"),
                Padding = new Thickness(7, 4, 7, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            btnLockedDel.Click += (s, e) => MessageBox.Show(this, Loc.T("CannotDeletePermanent"), "Snappy Layouts", MessageBoxButton.OK, MessageBoxImage.Information);

            Grid.SetColumn(permPreview, 0);
            Grid.SetColumn(permInfo, 1);
            Grid.SetColumn(btnLockedDel, 2);
            permGrid.Children.Add(permPreview);
            permGrid.Children.Add(permInfo);
            permGrid.Children.Add(btnLockedDel);
            permCard.Child = permGrid;
            leftStack.Children.Add(permCard);

            // 2) Liste Araç Çubuğu (+ Yeni Ekle, Varsayılanları Yükle, Tümünü Sil) — Yalnızca seçili alana etki eder!
            var listBtnWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var btnNew = new Button { Content = Loc.T("NewLayout"), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 6, 6), FontWeight = FontWeights.SemiBold };
            btnNew.SetResourceReference(Control.BackgroundProperty, "NavLayoutsBgBrush");
            btnNew.SetResourceReference(Control.BorderBrushProperty, "NavLayoutsBrush");
            btnNew.SetResourceReference(Control.ForegroundProperty, "NavLayoutsBrush");
            btnNew.Click += (s, e) =>
            {
                string prefix = Loc.IsTurkish ? "Özel Pencere Düzeni " : "Custom Window Layout ";
                var nl = SnapLayout.Grid(prefix + (currentAreaLayouts.Count + 1), new double[] { 1, 1, 1 }, new double[] { 1, 1 });
                currentAreaLayouts.Add(nl);
                _selectedVirtualFull = false;
                _selectedLayout = nl;
                Storage.Save();
                HostLayouts.Content = BuildLayoutsPage();
            };

            var btnRestoreDef = new Button { Content = Loc.T("RestoreDefaults"), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 6, 6) };
            btnRestoreDef.Click += (s, e) =>
            {
                AreaService.ResetLayoutsForArea(_selectedPreviewArea);
                _selectedVirtualFull = false;
                _selectedLayout = AreaService.GetLayoutsForArea(_selectedPreviewArea).FirstOrDefault();
                HostLayouts.Content = BuildLayoutsPage();
            };

            var btnDeleteAll = new Button { Content = Loc.T("DeleteAllCustom"), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6) };
            btnDeleteAll.Click += (s, e) =>
            {
                currentAreaLayouts.Clear();
                _selectedLayout = null;
                _selectedVirtualFull = true;
                Storage.Save();
                HostLayouts.Content = BuildLayoutsPage();
            };

            listBtnWrap.Children.Add(btnNew);
            listBtnWrap.Children.Add(btnRestoreDef);
            listBtnWrap.Children.Add(btnDeleteAll);
            leftStack.Children.Add(listBtnWrap);

            // 2.5) Çoklu Seçimli Ekran Tipi Filtre Çubuğu (Show on Screen Types)
            var visibleLayouts = currentAreaLayouts.Where(l =>
                (_filterLandscape && l.ShowOnLandscape) ||
                (_filterUltrawide && l.ShowOnUltrawide) ||
                (_filterPortrait && l.ShowOnPortrait)).ToList();

            var filterCard = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 6),
                Margin = new Thickness(0, 0, 0, 10)
            };
            filterCard.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            filterCard.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var filterStack = new StackPanel();
            var filterHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            filterHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var filterLbl = new TextBlock
            {
                Text = "🔎 " + Loc.T("FilterByScreenTypes"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            filterLbl.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            var filterCountTb = new TextBlock
            {
                Text = $"{visibleLayouts.Count} / {currentAreaLayouts.Count}",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            filterCountTb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

            Grid.SetColumn(filterLbl, 0);
            Grid.SetColumn(filterCountTb, 1);
            filterHeader.Children.Add(filterLbl);
            filterHeader.Children.Add(filterCountTb);
            filterStack.Children.Add(filterHeader);

            var filterWrap = new WrapPanel();
            Button MakeFilterChip(string label, bool isOn, Action onToggle)
            {
                var chip = new Button
                {
                    Content = (isOn ? "✓ " : "○ ") + label,
                    FontSize = 11,
                    FontWeight = isOn ? FontWeights.Bold : FontWeights.Normal,
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 5, 4),
                    Cursor = Cursors.Hand
                };
                if (isOn)
                {
                    chip.SetResourceReference(Control.BackgroundProperty, "NavLayoutsBgBrush");
                    chip.SetResourceReference(Control.BorderBrushProperty, "NavLayoutsBrush");
                    chip.SetResourceReference(Control.ForegroundProperty, "NavLayoutsBrush");
                }
                else
                {
                    chip.SetResourceReference(Control.BackgroundProperty, "InputBgBrush");
                    chip.SetResourceReference(Control.BorderBrushProperty, "CardBorderBrush");
                    chip.SetResourceReference(Control.ForegroundProperty, "TextSecondaryBrush");
                }
                chip.Click += (s, e) =>
                {
                    onToggle();
                    HostLayouts.Content = BuildLayoutsPage();
                };
                return chip;
            }

            filterWrap.Children.Add(MakeFilterChip(Loc.T("ShowOnLandscape"), _filterLandscape, () => _filterLandscape = !_filterLandscape));
            filterWrap.Children.Add(MakeFilterChip(Loc.T("ShowOnUltrawide"), _filterUltrawide, () => _filterUltrawide = !_filterUltrawide));
            filterWrap.Children.Add(MakeFilterChip(Loc.T("ShowOnPortrait"), _filterPortrait, () => _filterPortrait = !_filterPortrait));
            filterStack.Children.Add(filterWrap);
            filterCard.Child = filterStack;
            leftStack.Children.Add(filterCard);

            // 3) Seçili Fiziksel / Sanal Monitörün Pencere Yerleşim Kartları
            double previewAspect = _selectedPreviewArea.Aspect;
            double thumbH = 54;
            double thumbW = Math.Clamp(thumbH * Math.Clamp(previewAspect, 0.55, 3.0), 54, 150);

            if (!_selectedVirtualFull && (_selectedLayout == null || !visibleLayouts.Contains(_selectedLayout)))
            {
                _selectedLayout = visibleLayouts.FirstOrDefault();
                if (_selectedLayout == null) _selectedVirtualFull = true;
            }

            // Sanal Monitör Tam Ekran (100%) her zaman yerleşim listesinin EN BAŞINDA sabit (silinemez / taşınamaz) kart olarak gösterilir
            var vmFullCard = MakeCard();
            if (_selectedVirtualFull)
            {
                vmFullCard.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                vmFullCard.BorderThickness = new Thickness(2);
            }
            vmFullCard.Cursor = Cursors.Hand;

            var vmFullGrid = new Grid();
            vmFullGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vmFullGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vmFullGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var vmFullThumb = new LayoutPreviewControl
            {
                Layout = null,
                MonitorAspect = previewAspect,
                IsEditor = false,
                Width = thumbW,
                Height = thumbH,
                Margin = new Thickness(0, 0, 12, 0),
                IsHitTestVisible = false
            };

            var vmFullInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var vmFullTitleRow = new WrapPanel { Orientation = Orientation.Horizontal };
            string vmFullCardTitle = _selectedPreviewArea.IsVirtual
                ? $"{Loc.T("VirtualFullTitle")} (#{_selectedPreviewArea.SegmentIndex + 1} — 100%)"
                : $"{Loc.T("VirtualFullTitle")} (100%)";
            var vmFullTitleTb = new TextBlock
            {
                Text = "⛶  " + vmFullCardTitle,
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 6, 2)
            };
            vmFullTitleTb.SetResourceReference(TextBlock.ForegroundProperty, _selectedVirtualFull ? "AccentBrush" : "TextPrimaryBrush");

            var vmPermBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 0, 2)
            };
            vmPermBadge.SetResourceReference(Border.BackgroundProperty, "PermanentSubtleBrush");
            var vmPermBadgeTxt = new TextBlock { Text = "🔒 " + Loc.T("PermFullBadge"), FontSize = 9.5, FontWeight = FontWeights.Bold };
            vmPermBadgeTxt.SetResourceReference(TextBlock.ForegroundProperty, "PermanentBrush");
            vmPermBadge.Child = vmPermBadgeTxt;

            vmFullTitleRow.Children.Add(vmFullTitleTb);
            vmFullTitleRow.Children.Add(vmPermBadge);

            var vmFullDescTb = new TextBlock
            {
                Text = Loc.T("VirtualFullDesc"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };
            vmFullDescTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            vmFullInfo.Children.Add(vmFullTitleRow);
            vmFullInfo.Children.Add(vmFullDescTb);

            var btnVmFullLocked = new Button
            {
                Content = "🔒",
                ToolTip = Loc.T("CannotDeleteVirtualFull"),
                Padding = new Thickness(7, 4, 7, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            btnVmFullLocked.Click += (s, e) =>
            {
                e.Handled = true;
                MessageBox.Show(this, Loc.T("CannotDeleteVirtualFull"), "Snappy Layouts", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            Grid.SetColumn(vmFullThumb, 0);
            Grid.SetColumn(vmFullInfo, 1);
            Grid.SetColumn(btnVmFullLocked, 2);
            vmFullGrid.Children.Add(vmFullThumb);
            vmFullGrid.Children.Add(vmFullInfo);
            vmFullGrid.Children.Add(btnVmFullLocked);
            vmFullCard.Child = vmFullGrid;
            vmFullCard.MouseLeftButtonDown += (s, e) =>
            {
                _selectedVirtualFull = true;
                _selectedLayout = null;
                HostLayouts.Content = BuildLayoutsPage();
            };
            leftStack.Children.Add(vmFullCard);

            if (visibleLayouts.Count == 0 && currentAreaLayouts.Count > 0)
            {
                var noMatchCard = MakeCard();
                var noMatchTb = new TextBlock
                {
                    Text = Loc.T("NoMatchingLayouts"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(6, 8, 6, 8)
                };
                noMatchTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                noMatchCard.Child = noMatchTb;
                leftStack.Children.Add(noMatchCard);
            }

            for (int vIdx = 0; vIdx < visibleLayouts.Count; vIdx++)
            {
                var layout = visibleLayouts[vIdx];
                bool isSel = !_selectedVirtualFull && layout == _selectedLayout;
                var lCard = MakeCard();
                if (isSel)
                {
                    lCard.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                    lCard.BorderThickness = new Thickness(2);
                }
                lCard.Cursor = Cursors.Hand;

                var lGrid = new Grid();
                lGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                lGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                lGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var thumb = new LayoutPreviewControl
                {
                    Layout = layout,
                    MonitorAspect = previewAspect,
                    IsEditor = false,
                    Width = thumbW,
                    Height = thumbH,
                    Margin = new Thickness(0, 0, 12, 0),
                    IsHitTestVisible = false
                };

                var infoSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var nameTb = new TextBlock
                {
                    Text = layout.DisplayTitle,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12.5,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                nameTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                var metaTb = new TextBlock
                {
                    Text = $"{layout.Zones.Count} {Loc.T("ZonesCount")}  •  {layout.Columns.Count}×{layout.Rows.Count}"
                           + (layout.DefaultKey != null ? "  (Windows)" : ""),
                    FontSize = 11.5,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                metaTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                infoSp.Children.Add(nameTb);
                infoSp.Children.Add(metaTb);

                var actSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var captured = layout;
                int capturedVisIdx = vIdx;

                // Yukarı / Aşağı sıralama butonları
                var orderSp = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
                var btnMoveUp = new Button
                {
                    Content = "▲",
                    ToolTip = Loc.T("MoveUp"),
                    FontSize = 8.5,
                    Padding = new Thickness(5, 0, 5, 0),
                    Height = 16,
                    Margin = new Thickness(0, 0, 0, 2),
                    IsEnabled = capturedVisIdx > 0
                };
                btnMoveUp.Click += (s, e) =>
                {
                    e.Handled = true;
                    if (capturedVisIdx > 0)
                    {
                        var prevLayout = visibleLayouts[capturedVisIdx - 1];
                        int idxA = currentAreaLayouts.IndexOf(captured);
                        int idxB = currentAreaLayouts.IndexOf(prevLayout);
                        if (idxA >= 0 && idxB >= 0)
                        {
                            currentAreaLayouts[idxA] = prevLayout;
                            currentAreaLayouts[idxB] = captured;
                            Storage.Save();
                            HostLayouts.Content = BuildLayoutsPage();
                        }
                    }
                };

                var btnMoveDown = new Button
                {
                    Content = "▼",
                    ToolTip = Loc.T("MoveDown"),
                    FontSize = 8.5,
                    Padding = new Thickness(5, 0, 5, 0),
                    Height = 16,
                    IsEnabled = capturedVisIdx < visibleLayouts.Count - 1
                };
                btnMoveDown.Click += (s, e) =>
                {
                    e.Handled = true;
                    if (capturedVisIdx < visibleLayouts.Count - 1)
                    {
                        var nextLayout = visibleLayouts[capturedVisIdx + 1];
                        int idxA = currentAreaLayouts.IndexOf(captured);
                        int idxB = currentAreaLayouts.IndexOf(nextLayout);
                        if (idxA >= 0 && idxB >= 0)
                        {
                            currentAreaLayouts[idxA] = nextLayout;
                            currentAreaLayouts[idxB] = captured;
                            Storage.Save();
                            HostLayouts.Content = BuildLayoutsPage();
                        }
                    }
                };

                orderSp.Children.Add(btnMoveUp);
                orderSp.Children.Add(btnMoveDown);
                actSp.Children.Add(orderSp);

                var btnDup = new Button { Content = "⧉", ToolTip = Loc.T("Duplicate"), Padding = new Thickness(7, 4, 7, 4), Margin = new Thickness(0, 0, 4, 0) };
                btnDup.Click += (s, e) =>
                {
                    e.Handled = true;
                    var copy = captured.Clone();
                    copy.DefaultKey = null;
                    copy.Name = captured.DisplayTitle + " (Kopya)";
                    int insertAt = currentAreaLayouts.IndexOf(captured);
                    if (insertAt >= 0) currentAreaLayouts.Insert(insertAt + 1, copy);
                    else currentAreaLayouts.Add(copy);
                    _selectedVirtualFull = false;
                    _selectedLayout = copy;
                    Storage.Save();
                    HostLayouts.Content = BuildLayoutsPage();
                };

                var btnDel = new Button { Content = "🗑", ToolTip = Loc.T("Delete"), Padding = new Thickness(7, 4, 7, 4) };
                btnDel.Click += (s, e) =>
                {
                    e.Handled = true;
                    currentAreaLayouts.Remove(captured);
                    if (_selectedLayout == captured) _selectedLayout = currentAreaLayouts.FirstOrDefault();
                    Storage.Save();
                    HostLayouts.Content = BuildLayoutsPage();
                };

                actSp.Children.Add(btnDup);
                actSp.Children.Add(btnDel);

                Grid.SetColumn(thumb, 0);
                Grid.SetColumn(infoSp, 1);
                Grid.SetColumn(actSp, 2);
                lGrid.Children.Add(thumb);
                lGrid.Children.Add(infoSp);
                lGrid.Children.Add(actSp);
                lCard.Child = lGrid;

                lCard.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedVirtualFull = false;
                    _selectedLayout = captured;
                    HostLayouts.Content = BuildLayoutsPage();
                };

                leftStack.Children.Add(lCard);
            }

            Grid.SetColumn(leftStack, 0);
            mainGrid.Children.Add(leftStack);

            // --- SAĞ SÜTUN: ETKİLEŞİMLİ YERLEŞİM DÜZENLEYİCİ (SINIRSIZ SÜTUN & SATIR) ---
            var editorCard = MakeCard();
            if (_selectedVirtualFull)
            {
                editorCard.Child = BuildVirtualFullDetailsPanel(_selectedPreviewArea, previewAspect);
            }
            else if (_selectedLayout != null)
            {
                editorCard.Child = BuildLayoutEditorPanel(_selectedLayout, previewAspect);
            }
            else
            {
                var emptyTb = new TextBlock
                {
                    Text = Loc.T("NewLayout"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(40)
                };
                emptyTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                editorCard.Child = emptyTb;
            }
            Grid.SetColumn(editorCard, 2);
            mainGrid.Children.Add(editorCard);

            root.Children.Add(mainGrid);
            return root;
        }

        private UIElement BuildVirtualFullDetailsPanel(SnapArea area, double aspect)
        {
            var sp = new StackPanel();

            // Başlık + Sabit / Silinemez rozeti
            var headerWrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            string titleStr = area.IsVirtual
                ? $"⛶  {Loc.T("VirtualFullTitle")} (#{area.SegmentIndex + 1} — 100%)"
                : $"⛶  {Loc.T("VirtualFullTitle")} (100%)";
            var titleTb = new TextBlock
            {
                Text = titleStr,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 4)
            };
            titleTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var badge = new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            badge.SetResourceReference(Border.BackgroundProperty, "PermanentSubtleBrush");
            var badgeTxt = new TextBlock { Text = "🔒 " + Loc.T("PermFullBadge"), FontSize = 10.5, FontWeight = FontWeights.Bold };
            badgeTxt.SetResourceReference(TextBlock.ForegroundProperty, "PermanentBrush");
            badge.Child = badgeTxt;

            headerWrap.Children.Add(titleTb);
            headerWrap.Children.Add(badge);
            sp.Children.Add(headerWrap);

            var descTb = new TextBlock
            {
                Text = Loc.T("VirtualFullDesc"),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            descTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            sp.Children.Add(descTb);

            // Büyük %100 Önizleme
            var canvasBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 14)
            };
            canvasBorder.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
            canvasBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
            canvasBorder.Child = new LayoutPreviewControl
            {
                Layout = null,
                MonitorAspect = aspect,
                IsEditor = false,
                Height = 260,
                IsHitTestVisible = false
            };
            sp.Children.Add(canvasBorder);

            // Teknik Bilgiler Kartı
            var infoBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 14)
            };
            infoBorder.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
            infoBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var infoGrid = new Grid();
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void AddInfoRow(string k, string v)
            {
                int r = infoGrid.RowDefinitions.Count;
                infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var kTb = new TextBlock { Text = k, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 6) };
                kTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                var vTb = new TextBlock { Text = v, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
                vTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                Grid.SetRow(kTb, r); Grid.SetColumn(kTb, 0);
                Grid.SetRow(vTb, r); Grid.SetColumn(vTb, 1);
                infoGrid.Children.Add(kTb);
                infoGrid.Children.Add(vTb);
            }

            Storage.Data.Hotkeys.TryGetValue(HotkeyActions.VirtualMaximize, out var vMaxHk);
            AddInfoRow(Loc.T("TargetMonitor"), area.Label);
            AddInfoRow(Loc.T("Resolution") + ":", $"{area.Rect.Width} × {area.Rect.Height} px");
            AddInfoRow(Loc.T("AspectRatio") + ":", area.AspectLabel);
            AddInfoRow(Loc.T("DpiScale") + ":", $"{Math.Round(area.Monitor.Scale * 100)}%");
            AddInfoRow(Loc.T("Coverage"), "100% (1 × 1)");
            AddInfoRow(Loc.T("VirtualFullShortcutLabel"), vMaxHk == null || vMaxHk.IsEmpty ? "—" : vMaxHk.ToString());

            infoBorder.Child = infoGrid;
            sp.Children.Add(infoBorder);

            // İlgili Davranış Ayarları (Doğrudan düzenlenebilir!)
            sp.Children.Add(MakeSectionLabel("⚙  " + Loc.T("VirtualFullSettingsTitle")));
            var settings = Storage.Data.Settings;

            var cbVMax = new CheckBox
            {
                Content = Loc.T("SetVirtualMax"),
                IsChecked = settings.VirtualMaximize,
                Margin = new Thickness(0, 6, 0, 8)
            };
            cbVMax.Click += (s, e) => { settings.VirtualMaximize = cbVMax.IsChecked == true; Storage.Save(); };

            var cbShift = new CheckBox
            {
                Content = Loc.T("SetShiftBypass"),
                IsChecked = settings.ShiftBypassesVirtualMaximize,
                Margin = new Thickness(0, 0, 0, 12)
            };
            cbShift.Click += (s, e) => { settings.ShiftBypassesVirtualMaximize = cbShift.IsChecked == true; Storage.Save(); };

            sp.Children.Add(cbVMax);
            sp.Children.Add(cbShift);

            // Sabit / Silinemez Bilgi Şeridi
            var lockNote = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };
            lockNote.SetResourceReference(Border.BackgroundProperty, "PermanentSubtleBrush");
            lockNote.SetResourceReference(Border.BorderBrushProperty, "PermanentBrush");
            var lockNoteTb = new TextBlock
            {
                Text = "🔒  " + Loc.T("CannotDeleteVirtualFull"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            lockNoteTb.SetResourceReference(TextBlock.ForegroundProperty, "PermanentBrush");
            lockNote.Child = lockNoteTb;
            sp.Children.Add(lockNote);

            return sp;
        }

        private UIElement BuildLayoutEditorPanel(SnapLayout layout, double aspect)
        {
            var sp = new StackPanel();

            // 1) Yerleşim adı
            var nameRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lblName = new TextBlock
            {
                Text = Loc.T("LayoutName"),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            lblName.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var nameBox = new TextBox
            {
                Text = layout.DisplayTitle,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 5, 8, 5)
            };
            nameBox.TextChanged += (s, e) =>
            {
                layout.Name = nameBox.Text;
                Storage.Save();
            };

            Grid.SetColumn(lblName, 0);
            Grid.SetColumn(nameBox, 1);
            nameRow.Children.Add(lblName);
            nameRow.Children.Add(nameBox);
            sp.Children.Add(nameRow);

            // 2) Sütun & Satır & Birleştirme Araç Çubuğu
            var toolsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };

            var editorCanvas = new LayoutPreviewControl
            {
                Layout = layout,
                MonitorAspect = aspect,
                IsEditor = true,
                Height = 300,
                Margin = new Thickness(0, 4, 0, 10)
            };

            Button MakeToolBtn(string text, Action onClick)
            {
                var b = new Button { Content = text, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 6), FontSize = 12 };
                b.Click += (s, e) =>
                {
                    onClick();
                    Storage.Save();
                    HostLayouts.Content = BuildLayoutsPage();
                };
                return b;
            }

            toolsWrap.Children.Add(MakeToolBtn($"{Loc.T("AddCol")} ({layout.Columns.Count})", () => layout.AddColumn()));
            toolsWrap.Children.Add(MakeToolBtn(Loc.T("RemCol"), () => layout.RemoveColumn()));
            toolsWrap.Children.Add(MakeToolBtn(Loc.T("EqualizeCols"), () =>
            {
                for (int i = 0; i < layout.Columns.Count; i++) layout.Columns[i] = 1;
            }));

            toolsWrap.Children.Add(MakeToolBtn($"{Loc.T("AddRow")} ({layout.Rows.Count})", () => layout.AddRow()));
            toolsWrap.Children.Add(MakeToolBtn(Loc.T("RemRow"), () => layout.RemoveRow()));
            toolsWrap.Children.Add(MakeToolBtn(Loc.T("EqualizeRows"), () =>
            {
                for (int i = 0; i < layout.Rows.Count; i++) layout.Rows[i] = 1;
            }));

            var btnMerge = new Button
            {
                Content = "⊞ " + Loc.T("MergeSelected"),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 6, 6),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            btnMerge.Click += (s, e) =>
            {
                var r = editorCanvas.SelectedCellRange;
                if (r != null)
                {
                    layout.Merge(r.Value.c0, r.Value.r0, r.Value.c1, r.Value.r1);
                    editorCanvas.ClearSelection();
                    Storage.Save();
                    HostLayouts.Content = BuildLayoutsPage();
                }
            };
            toolsWrap.Children.Add(btnMerge);
            toolsWrap.Children.Add(MakeToolBtn("⊟ " + Loc.T("SplitAll"), () => layout.ResetZonesToCells()));

            sp.Children.Add(toolsWrap);

            // 3) Büyük Etkileşimli Izgara Önizleme (seçili monitörün en/boy oranında!)
            var canvasBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };
            canvasBorder.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
            canvasBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
            canvasBorder.Child = editorCanvas;
            sp.Children.Add(canvasBorder);

            var hintTb = new TextBlock
            {
                Text = Loc.T("EditorHint"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            hintTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            sp.Children.Add(hintTb);

            // 4) Sütun ve Satır Genişlik Oranı Kutuları
            var colWeightsPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };
            var rowWeightsPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 12) };

            void RebuildWeightBoxes()
            {
                colWeightsPanel.Children.Clear();
                double colTotal = layout.Columns.Sum();
                if (colTotal <= 0) colTotal = 1;
                for (int i = 0; i < layout.Columns.Count; i++)
                {
                    int idx = i;
                    double pct = layout.Columns[i] / colTotal * 100.0;
                    var box = MakeWeightInputBox($"Sütun {i + 1}", pct, newPct =>
                    {
                        ApplyExactPercentage(layout.Columns, idx, newPct);
                        Storage.Save();
                        editorCanvas.InvalidateVisual();
                        RebuildWeightBoxes();
                    });
                    colWeightsPanel.Children.Add(box);
                }

                rowWeightsPanel.Children.Clear();
                double rowTotal = layout.Rows.Sum();
                if (rowTotal <= 0) rowTotal = 1;
                for (int i = 0; i < layout.Rows.Count; i++)
                {
                    int idx = i;
                    double pct = layout.Rows[i] / rowTotal * 100.0;
                    var box = MakeWeightInputBox($"Satır {i + 1}", pct, newPct =>
                    {
                        ApplyExactPercentage(layout.Rows, idx, newPct);
                        Storage.Save();
                        editorCanvas.InvalidateVisual();
                        RebuildWeightBoxes();
                    });
                    rowWeightsPanel.Children.Add(box);
                }
            }

            RebuildWeightBoxes();
            editorCanvas.LayoutEdited += () =>
            {
                Storage.Save();
                RebuildWeightBoxes();
            };

            sp.Children.Add(MakeSectionLabel(Loc.T("ColWeights")));
            sp.Children.Add(colWeightsPanel);
            sp.Children.Add(MakeSectionLabel(Loc.T("RowWeights")));
            sp.Children.Add(rowWeightsPanel);

            // 5) Ekran tipi görünürlük filtreleri
            sp.Children.Add(MakeSectionLabel(Loc.T("VisibilityFilter")));
            var visWrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            var cbLand = new CheckBox { Content = Loc.T("ShowOnLandscape"), IsChecked = layout.ShowOnLandscape, Margin = new Thickness(0, 0, 16, 4) };
            cbLand.Click += (s, e) => { layout.ShowOnLandscape = cbLand.IsChecked == true; Storage.Save(); };
            var cbUltra = new CheckBox { Content = Loc.T("ShowOnUltrawide"), IsChecked = layout.ShowOnUltrawide, Margin = new Thickness(0, 0, 16, 4) };
            cbUltra.Click += (s, e) => { layout.ShowOnUltrawide = cbUltra.IsChecked == true; Storage.Save(); };
            var cbPort = new CheckBox { Content = Loc.T("ShowOnPortrait"), IsChecked = layout.ShowOnPortrait, Margin = new Thickness(0, 0, 16, 4) };
            cbPort.Click += (s, e) => { layout.ShowOnPortrait = cbPort.IsChecked == true; Storage.Save(); };

            visWrap.Children.Add(cbLand);
            visWrap.Children.Add(cbUltra);
            visWrap.Children.Add(cbPort);
            sp.Children.Add(visWrap);

            return sp;
        }

        private static void ApplyExactPercentage(List<double> weights, int index, double targetPct)
        {
            if (weights == null || index < 0 || index >= weights.Count) return;
            if (weights.Count == 1)
            {
                weights[0] = 100.0;
                return;
            }

            // 33 -> 33.3333 (1/3) ve 66 -> 66.6667 (2/3) tam kesir desteği
            if (Math.Abs(targetPct - 33.0) < 0.05) targetPct = 100.0 / 3.0;
            else if (Math.Abs(targetPct - 66.0) < 0.05 || Math.Abs(targetPct - 67.0) < 0.05) targetPct = 200.0 / 3.0;

            double minEach = 5.0;
            double maxTarget = Math.Max(minEach, 100.0 - (weights.Count - 1) * minEach);
            targetPct = Math.Clamp(targetPct, minEach, maxTarget);

            double total = weights.Sum();
            if (total <= 0) total = 1.0;
            for (int i = 0; i < weights.Count; i++)
                weights[i] = weights[i] / total * 100.0;

            double othersSum = 0;
            for (int i = 0; i < weights.Count; i++)
                if (i != index) othersSum += weights[i];

            double remaining = Math.Max((weights.Count - 1) * minEach, 100.0 - targetPct);
            weights[index] = targetPct;

            if (othersSum <= 0.0001)
            {
                double share = remaining / (weights.Count - 1);
                for (int i = 0; i < weights.Count; i++)
                    if (i != index) weights[i] = share;
            }
            else
            {
                for (int i = 0; i < weights.Count; i++)
                {
                    if (i != index)
                        weights[i] = Math.Max(minEach, weights[i] / othersSum * remaining);
                }
            }
        }

        private UIElement MakeWeightInputBox(string label, double pct, Action<double> onChanged)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 10, 8)
            };
            b.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
            b.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var rootStack = new StackPanel { Orientation = Orientation.Vertical };

            // Üst satır: Etiket + Sayı Kutusu + Alt/Üst Arttırma/Azaltma Butonları + %
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var lbl = new TextBlock
            {
                Text = label + ": ",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var tb = new TextBox
            {
                Text = pct.ToString("0.#", CultureInfo.InvariantCulture),
                Width = 54,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(5, 3, 5, 3),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };

            void CommitValue(double val)
            {
                if (val > 0) onChanged(val);
            }

            tb.LostFocus += (s, e) =>
            {
                if (double.TryParse(tb.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0)
                    CommitValue(v);
            };
            tb.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && double.TryParse(tb.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0)
                {
                    CommitValue(v);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    CommitValue(Math.Round(pct) + 1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    CommitValue(Math.Max(5, Math.Round(pct) - 1));
                    e.Handled = true;
                }
            };
            tb.PreviewMouseWheel += (s, e) =>
            {
                double delta = e.Delta > 0 ? 1 : -1;
                CommitValue(Math.Max(5, Math.Round(pct) + delta));
                e.Handled = true;
            };

            // Alt-Üst sayı arttırma / azaltma (Spinner) butonları
            var spinnerStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 2, 0)
            };

            var btnUp = new Button
            {
                Content = "▲",
                FontSize = 8.5,
                Padding = new Thickness(6, 0, 6, 0),
                Height = 14,
                Margin = new Thickness(0, 0, 0, 1),
                ToolTip = "+1%"
            };
            btnUp.Click += (s, e) => CommitValue(Math.Round(pct) + 1);

            var btnDown = new Button
            {
                Content = "▼",
                FontSize = 8.5,
                Padding = new Thickness(6, 0, 6, 0),
                Height = 14,
                ToolTip = "-1%"
            };
            btnDown.Click += (s, e) => CommitValue(Math.Max(5, Math.Round(pct) - 1));

            spinnerStack.Children.Add(btnUp);
            spinnerStack.Children.Add(btnDown);

            var unit = new TextBlock
            {
                Text = "%",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            unit.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            topRow.Children.Add(lbl);
            topRow.Children.Add(tb);
            topRow.Children.Add(spinnerStack);
            topRow.Children.Add(unit);
            rootStack.Children.Add(topRow);

            // Alt satır: Hazır sabit oran çipleri (25%, 33%, 50%, 66%, 75%, 100%)
            var presetsRow = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };

            int[] quickPresets = new[] { 25, 33, 50, 66, 75, 100 };
            foreach (int qp in quickPresets)
            {
                int capturedQp = qp;
                bool isMatch = (qp == 33 && Math.Abs(pct - 33.33) < 0.8) ||
                               (qp == 66 && Math.Abs(pct - 66.67) < 0.8) ||
                               Math.Abs(pct - qp) < 0.6;

                var chip = new Button
                {
                    Content = $"{qp}%",
                    FontSize = 10.5,
                    FontWeight = isMatch ? FontWeights.Bold : FontWeights.Normal,
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 4, 2)
                };
                if (isMatch)
                {
                    chip.SetResourceReference(Button.BackgroundProperty, "AccentSubtleBrush");
                    chip.SetResourceReference(Button.BorderBrushProperty, "AccentBrush");
                }
                chip.Click += (s, e) => CommitValue(capturedQp);
                presetsRow.Children.Add(chip);
            }

            rootStack.Children.Add(presetsRow);
            b.Child = rootStack;
            return b;
        }

        // =========================================================================
        // SAYFA 1: SANAL MONİTÖRLER (YAN YANA EŞİT / EŞİT OLMAYAN BÖLME)
        // =========================================================================
        private UIElement BuildVirtualMonitorsPage()
        {
            var root = new StackPanel();
            root.Children.Add(MakePageHeader(Loc.T("VmTitle"), Loc.T("VmSubtitle")));

            var monitors = MonitorService.GetMonitors(true);
            if (_selectedPhysicalMonitor == null || !monitors.Any(m => m.Key == _selectedPhysicalMonitor.Key))
                _selectedPhysicalMonitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.First();
            else
                _selectedPhysicalMonitor = monitors.First(m => m.Key == _selectedPhysicalMonitor.Key);

            // 1) Fiziksel Monitör Seçici Kartı
            var monCard = MakeCard();
            var monStack = new StackPanel();
            monStack.Children.Add(MakeSectionLabel(Loc.T("SelectPhysicalMonitor")));

            var monCardsWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var m in monitors)
            {
                bool isSel = m.Key == _selectedPhysicalMonitor.Key;
                var mBtn = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(isSel ? 2.2 : 1.2),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 10, 6),
                    Cursor = Cursors.Hand
                };
                mBtn.SetResourceReference(Border.BackgroundProperty, isSel ? "PhysMonSelectedBgBrush" : "PhysMonBgBrush");
                mBtn.SetResourceReference(Border.BorderBrushProperty, isSel ? "PhysMonSelectedBorderBrush" : "PhysMonBorderBrush");

                var mGrid = new Grid();
                mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                mGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var mIconBadge = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(7),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = isSel ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)) : Brushes.Transparent,
                    BorderThickness = new Thickness(isSel ? 0 : 1)
                };
                if (!isSel)
                    mIconBadge.SetResourceReference(Border.BorderBrushProperty, "PhysMonBorderBrush");
                var mIconTb = new TextBlock
                {
                    Text = "🖥",
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                mIconTb.SetResourceReference(TextBlock.ForegroundProperty, isSel ? "PhysMonSelectedFgBrush" : "PhysMonAccentBrush");
                mIconBadge.Child = mIconTb;

                var mSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var t1 = new TextBlock { Text = m.DisplayName, FontWeight = FontWeights.Bold, FontSize = 13 };
                t1.SetResourceReference(TextBlock.ForegroundProperty, isSel ? "PhysMonSelectedFgBrush" : "TextPrimaryBrush");
                var vmCount = AreaService.GetStoredWeights(m).Count;
                var t2 = new TextBlock
                {
                    Text = $"{Loc.T("AspectRatio")}: {m.AspectLabel}   •   ◫ {Loc.T("VirtualMonitor")}: {vmCount}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                t2.SetResourceReference(TextBlock.ForegroundProperty, isSel ? "PhysMonSelectedSubFgBrush" : "PhysMonAccentBrush");
                mSp.Children.Add(t1);
                mSp.Children.Add(t2);

                var capturedMon = m;
                var btnRenMon = new Button
                {
                    Content = "✎",
                    ToolTip = Loc.T("RenameMonitor"),
                    Padding = new Thickness(6, 2, 6, 2),
                    FontSize = 11.5,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                btnRenMon.Click += (s, e) =>
                {
                    e.Handled = true;
                    ShowRenameMonitorDialog(capturedMon);
                };

                Grid.SetColumn(mIconBadge, 0);
                Grid.SetColumn(mSp, 1);
                Grid.SetColumn(btnRenMon, 2);
                mGrid.Children.Add(mIconBadge);
                mGrid.Children.Add(mSp);
                mGrid.Children.Add(btnRenMon);
                mBtn.Child = mGrid;

                mBtn.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedPhysicalMonitor = capturedMon;
                    HostVirtual.Content = BuildVirtualMonitorsPage();
                };
                monCardsWrap.Children.Add(mBtn);
            }
            monStack.Children.Add(monCardsWrap);
            monCard.Child = monStack;
            root.Children.Add(monCard);

            // 2) Sanal Monitör Etkileşimli Bölücü (Yan Yana, Eşit Olmayan Genişlikler)
            var weights = AreaService.GetStoredWeights(_selectedPhysicalMonitor);
            var barCard = MakeCard();
            var barStack = new StackPanel();

            var barToolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };

            void ApplyWeightsToMonitor(List<double> newW)
            {
                AreaService.SetWeights(_selectedPhysicalMonitor, newW);
                App.Instance?.Overlay?.Refresh();
                App.Instance?.Overlay?.Flash();
                HostVirtual.Content = BuildVirtualMonitorsPage();
                HostLayouts.Content = BuildLayoutsPage();
            }

            var btnAddVm = new Button { Content = Loc.T("VmAdd"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 6), FontWeight = FontWeights.SemiBold };
            btnAddVm.SetResourceReference(Control.BackgroundProperty, "NavVirtualBgBrush");
            btnAddVm.SetResourceReference(Control.BorderBrushProperty, "NavVirtualBrush");
            btnAddVm.SetResourceReference(Control.ForegroundProperty, "NavVirtualBrush");
            btnAddVm.Click += (s, e) =>
            {
                var w = AreaService.GetStoredWeights(_selectedPhysicalMonitor);
                w.Add(w.Average());
                ApplyWeightsToMonitor(w);
            };

            var btnRemVm = new Button { Content = Loc.T("VmRemove"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 6), IsEnabled = weights.Count > 1 };
            btnRemVm.Click += (s, e) =>
            {
                var w = AreaService.GetStoredWeights(_selectedPhysicalMonitor);
                if (w.Count > 1) w.RemoveAt(w.Count - 1);
                ApplyWeightsToMonitor(w);
            };

            var btnEqVm = new Button { Content = Loc.T("VmEqualize"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 6), IsEnabled = weights.Count > 1 };
            btnEqVm.Click += (s, e) =>
            {
                var w = AreaService.GetStoredWeights(_selectedPhysicalMonitor);
                for (int i = 0; i < w.Count; i++) w[i] = 1;
                ApplyWeightsToMonitor(w);
            };

            var btnDisableVm = new Button { Content = Loc.T("VmDisable"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 6), IsEnabled = weights.Count > 1 };
            btnDisableVm.Click += (s, e) => ApplyWeightsToMonitor(new List<double> { 1 });

            var btnFlash = new Button { Content = "◫  " + Loc.T("VmShowBordersNow"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 0, 6) };
            btnFlash.Click += (s, e) => App.Instance?.Overlay?.Flash();

            barToolbar.Children.Add(btnAddVm);
            barToolbar.Children.Add(btnRemVm);
            barToolbar.Children.Add(btnEqVm);
            barToolbar.Children.Add(btnDisableVm);
            barToolbar.Children.Add(btnFlash);
            barStack.Children.Add(barToolbar);

            var hintText = new TextBlock
            {
                Text = Loc.T("VmBarHint"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            hintText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            barStack.Children.Add(hintText);

            var vmBar = new VirtualMonitorBarControl
            {
                MonitorAspect = _selectedPhysicalMonitor.Aspect,
                PhysicalWidthPx = _selectedPhysicalMonitor.WorkArea.Width,
                PhysicalHeightPx = _selectedPhysicalMonitor.WorkArea.Height,
                Weights = weights,
                Height = 250,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var inputsWrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            void RebuildSegmentInputs()
            {
                inputsWrap.Children.Clear();
                double total = vmBar.Weights.Sum();
                if (total <= 0) total = 1;
                for (int i = 0; i < vmBar.Weights.Count; i++)
                {
                    int idx = i;
                    double pct = vmBar.Weights[i] / total * 100.0;
                    int px = (int)Math.Round(pct / 100.0 * _selectedPhysicalMonitor.WorkArea.Width);
                    var box = MakeWeightInputBox($"{Loc.T("VirtualMonitor")} #{i + 1} ({px}px)", pct, newPct =>
                    {
                        ApplyExactPercentage(vmBar.Weights, idx, newPct);
                        AreaService.SetWeights(_selectedPhysicalMonitor, vmBar.Weights);
                        vmBar.InvalidateVisual();
                        App.Instance?.Overlay?.Refresh();
                        App.Instance?.Overlay?.Flash();
                        RebuildSegmentInputs();
                    });
                    inputsWrap.Children.Add(box);
                }
            }

            RebuildSegmentInputs();
            vmBar.WeightsChanged += () =>
            {
                AreaService.SetWeights(_selectedPhysicalMonitor, vmBar.Weights);
                App.Instance?.Overlay?.Refresh();
                App.Instance?.Overlay?.Flash();
                RebuildSegmentInputs();
            };

            barStack.Children.Add(vmBar);
            barStack.Children.Add(MakeSectionLabel(Loc.T("VmWidthsTitle")));
            barStack.Children.Add(inputsWrap);
            barCard.Child = barStack;
            root.Children.Add(barCard);

            // 3) Sınır Çizgisi Görünümü + Kayıtlı Sanal Monitör Şablonları (2 sütun)
            var bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Sol: Sınır Çizgisi Ayarları
            var borderCard = MakeCard();
            var borderSp = new StackPanel();
            borderSp.Children.Add(MakeSectionLabel("◫  " + Loc.T("VmShowBordersNow")));

            var cbOnDrag = new CheckBox
            {
                Content = Loc.T("VmShowOnDrag"),
                IsChecked = Storage.Data.Settings.ShowBordersWhileDragging,
                Margin = new Thickness(0, 8, 0, 8)
            };
            cbOnDrag.Click += (s, e) => { Storage.Data.Settings.ShowBordersWhileDragging = cbOnDrag.IsChecked == true; Storage.Save(); };

            var cbAlways = new CheckBox
            {
                Content = Loc.T("VmAlwaysShowBorders"),
                IsChecked = Storage.Data.Settings.AlwaysShowBorders,
                Margin = new Thickness(0, 0, 0, 12)
            };
            cbAlways.Click += (s, e) =>
            {
                Storage.Data.Settings.AlwaysShowBorders = cbAlways.IsChecked == true;
                Storage.Save();
                App.Instance?.Overlay?.Refresh();
            };

            var thickGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            thickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            thickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            thickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblThick = new TextBlock { Text = Loc.T("VmBorderThickness"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            lblThick.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            var valThick = new TextBlock { Text = Storage.Data.Settings.BorderThickness + " px", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            valThick.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var sldThick = new Slider
            {
                Minimum = 2,
                Maximum = 16,
                Value = Storage.Data.Settings.BorderThickness,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            sldThick.ValueChanged += (s, e) =>
            {
                Storage.Data.Settings.BorderThickness = (int)sldThick.Value;
                valThick.Text = Storage.Data.Settings.BorderThickness + " px";
                Storage.Save();
                vmBar.InvalidateVisual();
                App.Instance?.Overlay?.Refresh();
            };

            Grid.SetColumn(lblThick, 0);
            Grid.SetColumn(sldThick, 1);
            Grid.SetColumn(valThick, 2);
            thickGrid.Children.Add(lblThick);
            thickGrid.Children.Add(sldThick);
            thickGrid.Children.Add(valThick);

            var colorRow = new Grid();
            colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lblCol = new TextBlock { Text = Loc.T("VmBorderColor"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            lblCol.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            var colorKeys = new[] { ("Accent", Loc.T("ColorAccent")), ("Cyan", Loc.T("ColorCyan")), ("Gold", Loc.T("ColorGold")), ("Coral", Loc.T("ColorCoral")), ("Emerald", Loc.T("ColorEmerald")) };
            var colCombo = new ComboBox { Padding = new Thickness(8, 5, 8, 5) };
            int colIdx = 0;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                colCombo.Items.Add(colorKeys[i].Item2);
                if (colorKeys[i].Item1 == Storage.Data.Settings.BorderColor) colIdx = i;
            }
            colCombo.SelectedIndex = colIdx;
            colCombo.SelectionChanged += (s, e) =>
            {
                if (colCombo.SelectedIndex >= 0)
                {
                    Storage.Data.Settings.BorderColor = colorKeys[colCombo.SelectedIndex].Item1;
                    Storage.Save();
                    vmBar.InvalidateVisual();
                    App.Instance?.Overlay?.Refresh();
                    App.Instance?.Overlay?.Flash();
                }
            };

            Grid.SetColumn(lblCol, 0);
            Grid.SetColumn(colCombo, 1);
            colorRow.Children.Add(lblCol);
            colorRow.Children.Add(colCombo);

            borderSp.Children.Add(cbOnDrag);
            borderSp.Children.Add(cbAlways);
            borderSp.Children.Add(thickGrid);
            borderSp.Children.Add(colorRow);
            borderCard.Child = borderSp;

            // Sağ: Kayıtlı Sanal Monitör Düzenleri (Presets)
            var presetsCard = MakeCard();
            var presetsSp = new StackPanel();
            presetsSp.Children.Add(MakeSectionLabel(Loc.T("VmPresetsTitle")));

            var savePresetRow = new Grid { Margin = new Thickness(0, 8, 0, 10) };
            savePresetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            savePresetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var presetNameBox = new TextBox
            {
                Text = string.Join(" | ", VirtualPreset.Percentages(weights).Select(p => p.ToString("0"))),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var btnSavePreset = new Button
            {
                Content = Loc.T("VmPresetSaveNew"),
                Padding = new Thickness(12, 6, 12, 6),
                FontWeight = FontWeights.SemiBold
            };
            btnSavePreset.Click += (s, e) =>
            {
                var p = new VirtualPreset
                {
                    Name = string.IsNullOrWhiteSpace(presetNameBox.Text) ? "Özel Şablon" : presetNameBox.Text.Trim(),
                    Weights = AreaService.GetStoredWeights(_selectedPhysicalMonitor),
                    Hotkey = Storage.Data.FindNextAvailableHotkey(forWorkspace: false)
                };
                Storage.Data.VirtualPresets.Add(p);
                Storage.Save();
                App.Instance?.Hotkeys?.ReRegister();
                HostVirtual.Content = BuildVirtualMonitorsPage();
                HostHotkeys.Content = BuildHotkeysPage();
            };

            Grid.SetColumn(presetNameBox, 0);
            Grid.SetColumn(btnSavePreset, 1);
            savePresetRow.Children.Add(presetNameBox);
            savePresetRow.Children.Add(btnSavePreset);
            presetsSp.Children.Add(savePresetRow);

            double presetMonAspect = _selectedPhysicalMonitor.Aspect;
            double presetThumbH = 48;
            double presetThumbW = Math.Clamp(presetThumbH * Math.Clamp(presetMonAspect, 0.6, 3.2), 68, 150);

            foreach (var preset in Storage.Data.VirtualPresets.ToList())
            {
                var pBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                pBorder.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
                pBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

                var pGrid = new Grid();
                pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var pThumb = new VirtualMonitorBarControl
                {
                    IsMiniPreview = true,
                    MonitorAspect = presetMonAspect,
                    Weights = preset.Weights.ToList(),
                    Width = presetThumbW,
                    Height = presetThumbH,
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };

                var pInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var pTitle = new TextBlock { Text = preset.Name, FontWeight = FontWeights.Bold, FontSize = 12.5 };
                pTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                var pSub = new TextBlock
                {
                    Text = preset.Description + (preset.Hotkey != null && !preset.Hotkey.IsEmpty ? $"   •   ⌨ {preset.Hotkey}" : ""),
                    FontSize = 11
                };
                pSub.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                pInfo.Children.Add(pTitle);
                pInfo.Children.Add(pSub);

                var pBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var capturedP = preset;
                var btnApplyP = new Button { Content = Loc.T("Apply"), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 5, 0), FontWeight = FontWeights.SemiBold };
                btnApplyP.Click += (s, e) => ApplyWeightsToMonitor(capturedP.Weights.ToList());

                var btnHkP = new Button { Content = "⌨", ToolTip = Loc.T("HkChange"), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 5, 0) };
                btnHkP.Click += (s, e) =>
                {
                    var dlg = new HotkeyCaptureDialog("vp:" + capturedP.Id, capturedP.Hotkey) { Owner = this };
                    if (dlg.ShowDialog() == true)
                    {
                        capturedP.Hotkey = dlg.ResultHotkey;
                        Storage.Save();
                        App.Instance?.Hotkeys?.ReRegister();
                        HostVirtual.Content = BuildVirtualMonitorsPage();
                    }
                };

                var btnDelP = new Button { Content = "🗑", ToolTip = Loc.T("Delete"), Padding = new Thickness(8, 4, 8, 4) };
                btnDelP.Click += (s, e) =>
                {
                    Storage.Data.VirtualPresets.Remove(capturedP);
                    Storage.Save();
                    App.Instance?.Hotkeys?.ReRegister();
                    HostVirtual.Content = BuildVirtualMonitorsPage();
                };

                pBtns.Children.Add(btnApplyP);
                pBtns.Children.Add(btnHkP);
                pBtns.Children.Add(btnDelP);

                Grid.SetColumn(pThumb, 0);
                Grid.SetColumn(pInfo, 1);
                Grid.SetColumn(pBtns, 2);
                pGrid.Children.Add(pThumb);
                pGrid.Children.Add(pInfo);
                pGrid.Children.Add(pBtns);
                pBorder.Child = pGrid;
                presetsSp.Children.Add(pBorder);
            }

            presetsCard.Child = presetsSp;
            Grid.SetColumn(borderCard, 0);
            Grid.SetColumn(presetsCard, 2);
            bottomGrid.Children.Add(borderCard);
            bottomGrid.Children.Add(presetsCard);
            root.Children.Add(bottomGrid);

            return root;
        }

        // =========================================================================
        // SAYFA 2: ÇALIŞMA ALANLARI & UYGULAMA / TARAYICI URL KAYITLARI
        // =========================================================================
        private UIElement BuildWorkspacesPage()
        {
            var root = new StackPanel();
            root.Children.Add(MakePageHeader(Loc.T("WsTitle"), Loc.T("WsSubtitle")));

            var topBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            var btnCapture = new Button
            {
                Content = Loc.T("WsCaptureNew"),
                Padding = new Thickness(16, 9, 16, 9),
                FontSize = 13,
                FontWeight = FontWeights.Bold
            };
            btnCapture.Click += (s, e) =>
            {
                var dlg = new WorkspaceCaptureDialog(_selectedPhysicalMonitor) { Owner = this };
                if (dlg.ShowDialog() == true && dlg.ResultWorkspace != null)
                {
                    if (dlg.ResultWorkspace.Hotkey == null || dlg.ResultWorkspace.Hotkey.IsEmpty)
                        dlg.ResultWorkspace.Hotkey = Storage.Data.FindNextAvailableHotkey(forWorkspace: true);
                    Storage.Data.Workspaces.Add(dlg.ResultWorkspace);
                    Storage.Save();
                    App.Instance?.Hotkeys?.ReRegister();
                    HostWorkspaces.Content = BuildWorkspacesPage();
                    HostHotkeys.Content = BuildHotkeysPage();
                }
            };
            topBar.Children.Add(btnCapture);
            root.Children.Add(topBar);

            if (Storage.Data.Workspaces.Count == 0)
            {
                var emptyCard = MakeCard();
                var emptyTb = new TextBlock
                {
                    Text = Loc.T("WsNoWorkspaces"),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12)
                };
                emptyTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                emptyCard.Child = emptyTb;
                root.Children.Add(emptyCard);
                return root;
            }

            foreach (var ws in Storage.Data.Workspaces.ToList())
            {
                var card = MakeCard();
                var sp = new StackPanel();

                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var titleSp = new StackPanel();
                var nameTb = new TextBlock { Text = ws.Name, FontSize = 15, FontWeight = FontWeights.Bold };
                nameTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

                string scopeLabel = ws.ScopeSegment < 0
                    ? Loc.T("WsScopeFull")
                    : $"{Loc.T("VirtualMonitor")} #{ws.ScopeSegment + 1}";
                string ratios = ws.Weights != null && ws.Weights.Count > 1
                    ? string.Join(" | ", VirtualPreset.Percentages(ws.Weights).Select(p => p.ToString("0.#") + "%"))
                    : "100%";

                var metaTb = new TextBlock
                {
                    Text = $"{ws.MonitorName}  •  {scopeLabel}  •  {ratios}"
                           + (ws.Hotkey != null && !ws.Hotkey.IsEmpty ? $"  •  ⌨ {ws.Hotkey}" : ""),
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                metaTb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

                titleSp.Children.Add(nameTb);
                titleSp.Children.Add(metaTb);

                var actBtns = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                var capturedWs = ws;

                var btnOpenCurrent = new Button
                {
                    Content = "🖥  " + Loc.T("WsApplyCurrent"),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 6, 0),
                    FontWeight = FontWeights.SemiBold
                };
                btnOpenCurrent.Click += async (s, e) =>
                {
                    btnOpenCurrent.IsEnabled = false;
                    await WorkspaceService.ApplyAsync(capturedWs, newDesktop: false);
                    btnOpenCurrent.IsEnabled = true;
                };

                var btnOpenNew = new Button
                {
                    Content = "✨  " + Loc.T("WsApplyNewDesktop"),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 6, 0),
                    FontWeight = FontWeights.SemiBold
                };
                btnOpenNew.Click += async (s, e) =>
                {
                    btnOpenNew.IsEnabled = false;
                    await WorkspaceService.ApplyAsync(capturedWs, newDesktop: true);
                    btnOpenNew.IsEnabled = true;
                };

                var btnHk = new Button
                {
                    Content = "⌨ " + Loc.T("HkChange"),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 6, 0)
                };
                btnHk.Click += (s, e) =>
                {
                    var dlg = new HotkeyCaptureDialog("ws:" + capturedWs.Id, capturedWs.Hotkey) { Owner = this };
                    if (dlg.ShowDialog() == true)
                    {
                        capturedWs.Hotkey = dlg.ResultHotkey;
                        Storage.Save();
                        App.Instance?.Hotkeys?.ReRegister();
                        HostWorkspaces.Content = BuildWorkspacesPage();
                    }
                };

                var btnDel = new Button { Content = "🗑", ToolTip = Loc.T("Delete"), Padding = new Thickness(9, 6, 9, 6) };
                btnDel.Click += (s, e) =>
                {
                    Storage.Data.Workspaces.Remove(capturedWs);
                    Storage.Save();
                    App.Instance?.Hotkeys?.ReRegister();
                    HostWorkspaces.Content = BuildWorkspacesPage();
                };

                actBtns.Children.Add(btnOpenCurrent);
                actBtns.Children.Add(btnOpenNew);
                actBtns.Children.Add(btnHk);
                actBtns.Children.Add(btnDel);

                Grid.SetColumn(titleSp, 0);
                Grid.SetColumn(actBtns, 1);
                headerGrid.Children.Add(titleSp);
                headerGrid.Children.Add(actBtns);
                sp.Children.Add(headerGrid);

                if (!ws.IncludeApps || ws.Apps == null || ws.Apps.Count == 0)
                {
                    var layoutOnlyTb = new TextBlock { Text = Loc.T("WsLayoutOnly"), FontSize = 11.5 };
                    layoutOnlyTb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                    sp.Children.Add(layoutOnlyTb);
                }
                else
                {
                    var appsWrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                    foreach (var a in ws.Apps)
                    {
                        var pill = new Border
                        {
                            CornerRadius = new CornerRadius(5),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(8, 4, 8, 4),
                            Margin = new Thickness(0, 0, 6, 6)
                        };
                        pill.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
                        pill.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

                        var pillTxt = new TextBlock
                        {
                            Text = (a.IsBrowser ? "🌐 " : "🗔 ") + a.DisplayName + $"  [{Math.Round(a.W * 100)}%×{Math.Round(a.H * 100)}%]",
                            FontSize = 11.5,
                            MaxWidth = 380,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        pillTxt.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                        pill.Child = pillTxt;
                        appsWrap.Children.Add(pill);
                    }
                    sp.Children.Add(appsWrap);
                }

                card.Child = sp;
                root.Children.Add(card);
            }

            return root;
        }

        // =========================================================================
        // SAYFA 3: KISAYOL TUŞ KOMBİNASYONLARI
        // =========================================================================
        private UIElement BuildHotkeysPage()
        {
            var root = new StackPanel();
            root.Children.Add(MakePageHeader(Loc.T("HkTitle"), Loc.T("HkSubtitle")));

            var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var btnDef = new Button
            {
                Content = Loc.T("HkRestoreDefaults"),
                Padding = new Thickness(12, 6, 12, 6)
            };
            btnDef.Click += (s, e) =>
            {
                Storage.Data.Hotkeys = HotkeyActions.Defaults();
                foreach (var vp in Storage.Data.VirtualPresets) vp.Hotkey = null;
                foreach (var ws in Storage.Data.Workspaces) ws.Hotkey = null;
                Storage.Data.AssignMissingHotkeys();
                Storage.Save();
                App.Instance?.Hotkeys?.ReRegister();
                HostHotkeys.Content = BuildHotkeysPage();
                HostVirtual.Content = BuildVirtualMonitorsPage();
                HostWorkspaces.Content = BuildWorkspacesPage();
            };
            topRow.Children.Add(btnDef);
            root.Children.Add(topRow);

            var card = MakeCard();
            var listSp = new StackPanel();

            void AddHotkeyRow(string actionId, string label, Hotkey hk, Action<Hotkey> setter)
            {
                var rowBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                rowBorder.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
                rowBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

                bool failed = App.Instance?.Hotkeys?.Failed.Contains(actionId) ?? false;
                var keyBadge = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(10, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                keyBadge.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
                keyBadge.SetResourceReference(Border.BorderBrushProperty, failed ? "DangerBrush" : "AccentBrush");

                var keyTxt = new TextBlock
                {
                    Text = hk == null || hk.IsEmpty ? "—" : hk.ToString(),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12
                };
                keyTxt.SetResourceReference(TextBlock.ForegroundProperty, failed ? "DangerBrush" : "TextPrimaryBrush");
                keyBadge.Child = keyTxt;

                var btns = new StackPanel { Orientation = Orientation.Horizontal };
                var btnSet = new Button { Content = Loc.T("HkChange"), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 5, 0) };
                btnSet.Click += (s, e) =>
                {
                    var dlg = new HotkeyCaptureDialog(actionId, hk) { Owner = this };
                    if (dlg.ShowDialog() == true)
                    {
                        setter(dlg.ResultHotkey);
                        Storage.Save();
                        App.Instance?.Hotkeys?.ReRegister();
                        HostHotkeys.Content = BuildHotkeysPage();
                    }
                };

                var btnClr = new Button { Content = Loc.T("HkClear"), Padding = new Thickness(10, 4, 10, 4) };
                btnClr.Click += (s, e) =>
                {
                    setter(new Hotkey());
                    Storage.Save();
                    App.Instance?.Hotkeys?.ReRegister();
                    HostHotkeys.Content = BuildHotkeysPage();
                };

                btns.Children.Add(btnSet);
                btns.Children.Add(btnClr);

                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(keyBadge, 1);
                Grid.SetColumn(btns, 2);
                g.Children.Add(lbl);
                g.Children.Add(keyBadge);
                g.Children.Add(btns);
                rowBorder.Child = g;
                listSp.Children.Add(rowBorder);
            }

            foreach (var act in HotkeyActions.All)
            {
                string capturedAct = act;
                Storage.Data.Hotkeys.TryGetValue(act, out var hk);
                AddHotkeyRow(act, Loc.T("HkAction_" + act), hk, newHk => Storage.Data.Hotkeys[capturedAct] = newHk);
            }

            foreach (var p in Storage.Data.VirtualPresets)
            {
                var capturedP = p;
                AddHotkeyRow("vp:" + p.Id, Loc.T("HkPresetPrefix") + p.Name + $" ({p.Description})", p.Hotkey, newHk => capturedP.Hotkey = newHk);
            }

            foreach (var ws in Storage.Data.Workspaces)
            {
                var capturedW = ws;
                AddHotkeyRow("ws:" + ws.Id, Loc.T("HkWorkspacePrefix") + ws.Name, ws.Hotkey, newHk => capturedW.Hotkey = newHk);
            }

            card.Child = listSp;
            root.Children.Add(card);
            return root;
        }

        // =========================================================================
        // SAYFA 4: AYARLAR & ÇOKLU MONİTÖR / DİNAMİK DPI YÖNETİMİ
        // =========================================================================
        private UIElement BuildSettingsPage()
        {
            var root = new StackPanel();
            root.Children.Add(MakePageHeader(Loc.T("SetTitle"), Loc.T("SetSubtitle")));

            var s = Storage.Data.Settings;

            // 1) Dil & Tema & Genel Davranış Kartı
            var genCard = MakeCard();
            var genSp = new StackPanel();

            var formGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            void AddFormRow(int r, string label, UIElement ctrl)
            {
                var tb = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 8), FontWeight = FontWeights.SemiBold };
                tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                if (ctrl is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 0, 8);
                Grid.SetRow(tb, r); Grid.SetColumn(tb, 0);
                Grid.SetRow(ctrl, r); Grid.SetColumn(ctrl, 1);
                formGrid.Children.Add(tb);
                formGrid.Children.Add(ctrl);
            }

            var langCombo = new ComboBox { Padding = new Thickness(8, 5, 8, 5) };
            PopulateLanguageCombo(langCombo, s.Language);
            langCombo.SelectionChanged += (sender, e) =>
            {
                if (langCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is LanguageChoice choice)
                {
                    s.Language = choice;
                    Storage.Save();
                    Loc.Apply(choice);
                }
            };
            AddFormRow(0, Loc.T("SetLanguage"), langCombo);

            var themeCombo = new ComboBox { Padding = new Thickness(8, 5, 8, 5) };
            themeCombo.Items.Add(Loc.T("ThemeSystem"));
            themeCombo.Items.Add(Loc.T("ThemeLight"));
            themeCombo.Items.Add(Loc.T("ThemeDark"));
            themeCombo.SelectedIndex = (int)s.Theme;
            themeCombo.SelectionChanged += (sender, e) =>
            {
                s.Theme = (ThemeChoice)themeCombo.SelectedIndex;
                Storage.Save();
                ThemeManager.Apply(s.Theme);
            };
            AddFormRow(1, Loc.T("SetTheme"), themeCombo);

            var delayBox = new TextBox { Text = s.HoverDelayMs.ToString(), Padding = new Thickness(8, 5, 8, 5) };
            delayBox.LostFocus += (sender, e) =>
            {
                if (int.TryParse(delayBox.Text, out int ms)) s.HoverDelayMs = Math.Clamp(ms, 100, 2500);
                delayBox.Text = s.HoverDelayMs.ToString();
                Storage.Save();
            };
            AddFormRow(2, Loc.T("SetHoverDelay"), delayBox);

            var gapBox = new TextBox { Text = s.ZoneGap.ToString(), Padding = new Thickness(8, 5, 8, 5) };
            gapBox.LostFocus += (sender, e) =>
            {
                if (int.TryParse(gapBox.Text, out int g)) s.ZoneGap = Math.Clamp(g, 0, 32);
                gapBox.Text = s.ZoneGap.ToString();
                Storage.Save();
            };
            AddFormRow(3, Loc.T("SetZoneGap"), gapBox);

            var wsModeCombo = new ComboBox { Padding = new Thickness(8, 5, 8, 5) };
            wsModeCombo.Items.Add(Loc.T("WsModeAsk"));
            wsModeCombo.Items.Add(Loc.T("WsModeCurrent"));
            wsModeCombo.Items.Add(Loc.T("WsModeNew"));
            wsModeCombo.SelectedIndex = (int)s.WorkspaceOpenMode;
            wsModeCombo.SelectionChanged += (sender, e) =>
            {
                s.WorkspaceOpenMode = (WorkspaceOpenMode)wsModeCombo.SelectedIndex;
                Storage.Save();
            };
            AddFormRow(4, Loc.T("SetWsOpenMode"), wsModeCombo);

            genSp.Children.Add(formGrid);

            var cbFly = new CheckBox { Content = Loc.T("SetFlyoutEnabled"), IsChecked = s.FlyoutEnabled, Margin = new Thickness(0, 4, 0, 6) };
            cbFly.Click += (sender, e) =>
            {
                s.FlyoutEnabled = cbFly.IsChecked == true;
                if (s.FlyoutEnabled) NativeSnapSuppressor.SuppressWindowsNativeFlyout();
                else NativeSnapSuppressor.RestoreWindowsNativeFlyout();
                Storage.Save();
            };

            var cbVMax = new CheckBox { Content = Loc.T("SetVirtualMax"), IsChecked = s.VirtualMaximize, Margin = new Thickness(0, 4, 0, 6) };
            cbVMax.Click += (sender, e) => { s.VirtualMaximize = cbVMax.IsChecked == true; Storage.Save(); };

            var cbShift = new CheckBox { Content = Loc.T("SetShiftBypass"), IsChecked = s.ShiftBypassesVirtualMaximize, Margin = new Thickness(0, 4, 0, 6) };
            cbShift.Click += (sender, e) => { s.ShiftBypassesVirtualMaximize = cbShift.IsChecked == true; Storage.Save(); };

            var cbStart = new CheckBox { Content = Loc.T("SetStartWithWin"), IsChecked = StartupService.IsEnabled(), Margin = new Thickness(0, 4, 0, 10) };
            cbStart.Click += (sender, e) =>
            {
                s.StartWithWindows = cbStart.IsChecked == true;
                StartupService.SetEnabled(s.StartWithWindows);
                Storage.Save();
            };

            genSp.Children.Add(cbFly);
            genSp.Children.Add(cbVMax);
            genSp.Children.Add(cbShift);
            genSp.Children.Add(cbStart);

            var ioRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var btnExp = new Button { Content = "📤 " + Loc.T("ExportSettings"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
            btnExp.Click += (sender, e) =>
            {
                var sfd = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "SnappyLayout-Settings.json" };
                if (sfd.ShowDialog(this) == true) Storage.Export(sfd.FileName);
            };
            var btnImp = new Button { Content = "📥 " + Loc.T("ImportSettings"), Padding = new Thickness(12, 6, 12, 6) };
            btnImp.Click += (sender, e) =>
            {
                var ofd = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
                if (ofd.ShowDialog(this) == true)
                {
                    try
                    {
                        Storage.Import(ofd.FileName);
                        Loc.Apply(Storage.Data.Settings.Language);
                        ThemeManager.Apply(Storage.Data.Settings.Theme);
                        App.Instance?.Hotkeys?.ReRegister();
                        App.Instance?.Overlay?.Refresh();
                        RebuildAllPages();
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Snappy Layouts", MessageBoxButton.OK, MessageBoxImage.Error); }
                }
            };
            ioRow.Children.Add(btnExp);
            ioRow.Children.Add(btnImp);
            genSp.Children.Add(ioRow);

            genCard.Child = genSp;
            root.Children.Add(genCard);

            // 2) Çoklu Monitör ve Dinamik DPI Tablosu
            var dpiCard = MakeCard();
            var dpiSp = new StackPanel();
            dpiSp.Children.Add(MakeSectionLabel("🖥  " + Loc.T("DpiTableTitle")));

            var dpiDesc = new TextBlock
            {
                Text = Loc.T("DpiDesc"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            };
            dpiDesc.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            dpiSp.Children.Add(dpiDesc);

            foreach (var m in MonitorService.GetMonitors(true))
            {
                var mRow = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    BorderThickness = new Thickness(1.2),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                mRow.SetResourceReference(Border.BackgroundProperty, "PhysMonBgBrush");
                mRow.SetResourceReference(Border.BorderBrushProperty, "PhysMonBorderBrush");

                var mRowGrid = new Grid();
                mRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                mRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var mSp = new StackPanel();
                var t1 = new TextBlock
                {
                    Text = m.DisplayName + (m.HasCustomName ? $"  ({Loc.T("HardwareName")}: {m.HardwareName})" : ""),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13
                };
                t1.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

                var vmWeights = AreaService.GetStoredWeights(m);
                var t2 = new TextBlock
                {
                    Text = $"({m.Bounds.Left}, {m.Bounds.Top})  •  {m.WorkArea.Width}×{m.WorkArea.Height} px  •  DPI: {m.Dpi} ({Math.Round(m.Scale * 100)}%)  •  {Loc.T("AspectRatio")}: {m.AspectLabel}  •  {Loc.T("VirtualMonitor")}: {vmWeights.Count}",
                    FontSize = 11.5,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                t2.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

                mSp.Children.Add(t1);
                mSp.Children.Add(t2);

                var capturedMonDpi = m;
                var btnRenDpi = new Button
                {
                    Content = "✎  " + Loc.T("RenameMonitor"),
                    Padding = new Thickness(10, 5, 10, 5),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                btnRenDpi.Click += (sender, e) => ShowRenameMonitorDialog(capturedMonDpi);

                Grid.SetColumn(mSp, 0);
                Grid.SetColumn(btnRenDpi, 1);
                mRowGrid.Children.Add(mSp);
                mRowGrid.Children.Add(btnRenDpi);
                mRow.Child = mRowGrid;
                dpiSp.Children.Add(mRow);
            }

            dpiCard.Child = dpiSp;
            root.Children.Add(dpiCard);

            return root;
        }

        private void ShowRenameMonitorDialog(MonitorInfo mon)
        {
            if (mon == null) return;

            var dlg = new Window
            {
                Title = Loc.T("RenameMonitor"),
                Width = 440,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            dlg.SetResourceReference(Window.BackgroundProperty, "AppBgBrush");
            dlg.SourceInitialized += (s, e) => ThemeManager.ApplyWindowChrome(dlg);

            var sp = new StackPanel { Margin = new Thickness(18) };

            var hwInfo = new TextBlock
            {
                Text = $"🖥  {mon.Index}. {mon.HardwareName}  ({mon.Bounds.Width}×{mon.Bounds.Height} @ {Math.Round(mon.Scale * 100)}%)",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 6)
            };
            hwInfo.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            sp.Children.Add(hwInfo);

            var prompt = new TextBlock
            {
                Text = Loc.T("RenameMonitorPrompt"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            prompt.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            sp.Children.Add(prompt);

            var nameBox = new TextBox
            {
                Text = mon.HasCustomName ? mon.EffectiveName : "",
                FontSize = 13.5,
                Padding = new Thickness(9, 6, 9, 6),
                Margin = new Thickness(0, 0, 0, 14)
            };
            sp.Children.Add(nameBox);

            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnReset = new Button
            {
                Content = Loc.T("RenameMonitorReset"),
                Padding = new Thickness(10, 6, 10, 6),
                IsEnabled = mon.HasCustomName
            };
            btnReset.Click += (s, e) =>
            {
                MonitorService.SetCustomName(mon, null);
                dlg.DialogResult = true;
                dlg.Close();
            };

            var rightBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var btnCancel = new Button
            {
                Content = Loc.T("Cancel"),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };
            var btnSave = new Button
            {
                Content = Loc.T("Save"),
                Padding = new Thickness(16, 6, 16, 6),
                FontWeight = FontWeights.SemiBold,
                IsDefault = true
            };
            btnSave.SetResourceReference(Button.BackgroundProperty, "AccentSubtleBrush");
            btnSave.SetResourceReference(Button.BorderBrushProperty, "AccentBrush");
            btnSave.Click += (s, e) =>
            {
                MonitorService.SetCustomName(mon, nameBox.Text);
                dlg.DialogResult = true;
                dlg.Close();
            };

            rightBtns.Children.Add(btnCancel);
            rightBtns.Children.Add(btnSave);

            Grid.SetColumn(btnReset, 0);
            Grid.SetColumn(rightBtns, 2);
            btnGrid.Children.Add(btnReset);
            btnGrid.Children.Add(rightBtns);
            sp.Children.Add(btnGrid);

            dlg.Content = sp;
            dlg.Loaded += (s, e) =>
            {
                nameBox.Focus();
                nameBox.SelectAll();
            };

            if (dlg.ShowDialog() == true)
            {
                RebuildAllPages();
            }
        }

        // =========================================================================
        // SAYFA 5: YARDIM & HAKKINDA (YUSUF KILINÇ — KASIM 2026 & VERSİYON)
        // =========================================================================
        private UIElement BuildHelpPage()
        {
            var root = new StackPanel();
            root.Children.Add(MakePageHeader(Loc.T("HelpTitle"), Loc.T("HelpSubtitle")));

            // 1) Yazar & Versiyon Kimlik Kartı
            var heroCard = MakeCard();
            heroCard.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
            heroCard.BorderThickness = new Thickness(2);

            var heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var heroLeft = new StackPanel();
            var appTitle = new TextBlock
            {
                Text = $"Snappy Layouts {AppInfo.VersionText}",
                FontSize = 22,
                FontWeight = FontWeights.Bold
            };
            appTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var authorLine = new TextBlock
            {
                Text = $"Yazılım Analizi, Tasarım ve Programlama:  {AppInfo.Author}  —  {AppInfo.AuthorDateTr}",
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 4, 0, 4)
            };
            authorLine.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

            var subLine = new TextBlock
            {
                Text = Loc.IsTurkish
                    ? "Bu sürüm v0.1'den başlayarak her geliştirmede artırılır; canlıya geçişte v1.0 olarak güncellenir."
                    : "Version starts at v0.1 and increments with every change; updated to v1.0 upon production release.",
                FontSize = 12
            };
            subLine.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            heroLeft.Children.Add(appTitle);
            heroLeft.Children.Add(authorLine);
            heroLeft.Children.Add(subLine);

            var verBadge = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 12, 18, 12),
                VerticalAlignment = VerticalAlignment.Center
            };
            verBadge.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            var verBadgeTxt = new TextBlock
            {
                Text = $"{AppInfo.VersionText}\n{AppInfo.Author}\n{AppInfo.AuthorDateTr}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                FontSize = 12.5
            };
            verBadge.Child = verBadgeTxt;

            Grid.SetColumn(heroLeft, 0);
            Grid.SetColumn(verBadge, 1);
            heroGrid.Children.Add(heroLeft);
            heroGrid.Children.Add(verBadge);
            heroCard.Child = heroGrid;
            root.Children.Add(heroCard);

            // 2) Adım Adım Kullanım Kılavuzu Kartları
            var sections = Loc.IsTurkish
                ? new (string Title, string Body)[]
                {
                    ("1. Pencerelerin Maksimize Tuşu Üzerinde Yerleşim Menüsü (Snap Flyout)",
                     "• Herhangi bir pencerenin sağ üstteki Büyüt (Maximize) tuşu üzerine fareyle gelip kısa süre beklediğinizde (veya Ctrl+Alt+Z kısayoluna bastığınızda) Snappy Layouts'ın özelleştirilmiş yerleşim menüsü açılır.\n" +
                     "• Açılan menüdeki kutulardan birine tıkladığınızda pencere anında o bölgeye yerleştirilir.\n" +
                     "• Tüm yerleşim kutuları o anki monitörün (veya sanal monitörün) gerçek en/boy oranına (16:9, 21:9 ultra-wide, dikey vb.) birebir uygun çizilir."),
                    ("2. Yerleşim Düzenlerini Silme, Ekleme ve Sınırsız Sütun/Satır Bölme",
                     "• 'Yerleşim Düzenleri' sekmesinde başlangıçta Windows'un varsayılan düzenleri yer alır. Bunları istediğiniz gibi silebilir, çoğaltabilir veya değiştirebilirsiniz.\n" +
                     "• '+ Yeni Yerleşim Ekle' ile yeni şablon oluşturabilir; '+ Sütun' ve '+ Satır' düğmeleriyle ekranı istediğiniz kadar sütun ve satıra bölebilirsiniz.\n" +
                     "• Izgara üzerindeki iç çizgileri fareyle sürükleyerek veya altındaki yüzde kutularına değer girerek eşit olmayan sütun/satır oranları verebilirsiniz.\n" +
                     "• Birden fazla hücreyi fareyle seçip 'Seçili Hücreleri Birleştir' ile L veya özel şekilli bölgeler oluşturabilirsiniz."),
                    ("3. Sanal Monitörler (Tek Fiziksel Monitörü Yan Yana Bölme)",
                     "• 'Sanal Monitörler' sekmesinde önce işlem yapmak istediğiniz fiziksel monitörü seçin.\n" +
                     "• '+ Sanal Monitör Ekle' ile monitörü yan yana 2, 3, 4 veya istediğiniz sayıda sanal monitöre bölün.\n" +
                     "• Sanal monitörlerin genişlikleri sabit/eşit olmak zorunda değildir: Şema üzerindeki kalın ayırıcı çizgileri sola/sağa sürükleyerek veya yüzde kutularından (ör. %25 | %50 | %25 veya %35 | %65) istediğiniz gibi ayarlayabilirsiniz.\n" +
                     "• Sanal monitörler oluşturulduktan sonra yerleşim düzenleri artık gerçek fiziksel monitöre göre değil, doğrudan ilgili sanal monitörün sınırlarına ve en/boy oranına göre çalışır."),
                    ("4. Sanal Monitörde Tam Ekran ve Silinemez Gerçek Fiziksel Tam Ekran",
                     "• Sanal monitör aktifken bir pencerede Tam Ekran (Maximize) tuşuna bastığınızda (veya Ctrl+Alt+↑ kısayoluyla) pencere yalnızca bulunduğu sanal monitörün sınırları içinde tam ekran olur.\n" +
                     "• Ancak her zaman sanal olmayan gerçek fiziksel monitörü tamamen kaplayacak 'Gerçek Fiziksel Tam Ekran (Silinemez)' yerleşimi/tuşu da mevcuttur ve silinemez. Ayrıca Shift tuşuna basılı tutarak Maximize'a basarsanız veya Ctrl+Alt+Shift+↑ kısayolunu kullanırsanız pencere tüm fiziksel monitörü kaplar."),
                    ("5. Sanal Monitör Sınır Çizgileri",
                     "• Sanal monitörlerin sınırları; genişlik ayarlama sırasında, pencere sürüklerken veya 'Ctrl+Alt+B' kısayoluna / 'Sınır Çizgilerini Göster' düğmesine basıldığında farklı renkte ve kalın çizgilerle ekranda gösterilir.\n" +
                     "• İsterseniz çizgi kalınlığını (px), rengini ve sürekli görünür kalmasını ayarlayabilirsiniz."),
                    ("6. Çalışma Alanları, Uygulama ve Tarayıcı URL Kaydı (Mevcut / Yeni Masaüstü)",
                     "• 'Çalışma Alanları' sekmesinde sanal monitör oranlarını ve seçtiğiniz sanal monitör (veya tam fiziksel monitör) içindeki açık uygulamaları kaydedebilirsiniz.\n" +
                     "• Kaydedilen bir çalışma alanı açıldığında, içindeki uygulamalar açık değilse otomatik olarak başlatılır ve kaydedilen konumlarına yerleştirilir.\n" +
                     "• Tarayıcılar (Chrome, Edge, Firefox, Brave, Opera, Vivaldi) için açık olan URL adresleri de kaydedilir ve geri yüklenirken o URL adresleriyle açılır.\n" +
                     "• Kayıtlı bir düzeni ister 'Mevcut Masaüstünde Aç' ister 'Yeni Masaüstünde Aç' (otomatik yeni Windows sanal masaüstü oluşturarak) seçeneğiyle başlatabilirsiniz."),
                    ("7. Çoklu Monitör ve Dinamik DPI Desteği",
                     "• Farklı çözünürlük ve ölçeklendirme (%100, %125, %150, %200 vb.) ayarlarına sahip monitörler arasında pencere taşırken Per-Monitor V2 dinamik DPI hesaplaması ve DWM görünmez çerçeve telafisi sayesinde pencerelerde kayma veya taşma olmaz.")
                }
                : new (string Title, string Body)[]
                {
                    ("1. Maximize Button Hover Flyout (Custom Snap Layouts)",
                     "• Hover over any window's Maximize button (or press Ctrl+Alt+Z) to open Snappy Layouts's custom layout flyout.\n" +
                     "• Click any zone box in the flyout to immediately snap the window into that zone.\n" +
                     "• All layout boxes and diagrams match the true aspect ratio (16:9, 21:9 ultra-wide, portrait, or virtual monitor ratio) of the target screen."),
                    ("2. Deleting Defaults, Adding Layouts & Unlimited Columns/Rows",
                     "• Snappy Layouts starts with default Windows layouts, which can be freely edited or deleted.\n" +
                     "• Click '+ Add New Layout' and use '+ Column' / '+ Row' to divide the screen into as many columns and rows as you want.\n" +
                     "• Drag inner grid lines or enter custom percentages for unequal column/row sizes, and drag-select cells to merge them."),
                    ("3. Virtual Monitors (Side-by-Side Equal or Unequal Splits)",
                     "• In the 'Virtual Monitors' tab, select which physical monitor to configure.\n" +
                     "• Split the monitor into any number of side-by-side virtual monitors with equal or custom unequal widths by dragging the dividers or typing exact percentages.\n" +
                     "• Once virtual monitors are active, snap layouts operate relative to each virtual monitor."),
                    ("4. Virtual Fullscreen & Indeletable Full Physical Screen",
                     "• When virtual monitors are active, clicking Maximize on a window (or pressing Ctrl+Alt+↑) maximizes it within its virtual monitor.\n" +
                     "• A permanent, indeletable 'Full Physical Screen' button/layout is always available in the flyout and layout list (or via Shift+Maximize / Ctrl+Alt+Shift+↑) to fill the entire real physical monitor."),
                    ("5. Virtual Monitor Boundary Lines",
                     "• Virtual monitor borders are displayed with thick, customizable lines when adjusting ratios, while dragging windows, or when pressing Ctrl+Alt+B."),
                    ("6. Saved Workspaces, Apps, Browser URLs & New Desktop Support",
                     "• Save virtual monitor ratios along with open applications, window positions, and browser URLs inside a virtual or full physical monitor.\n" +
                     "• Restore a workspace anytime on the Current Desktop or on a brand-new Windows Virtual Desktop."),
                    ("7. Multi-Monitor & Dynamic Per-Monitor V2 DPI Support",
                     "• Two-pass Per-Monitor V2 DPI and DWM extended frame compensation ensures pixel-perfect window placement across monitors with 100%, 125%, 150%, etc. scaling.")
                };

            foreach (var sec in sections)
            {
                var c = MakeCard();
                var sp = new StackPanel();
                var t = new TextBlock { Text = sec.Title, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
                t.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                var b = new TextBlock { Text = sec.Body, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
                b.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                sp.Children.Add(t);
                sp.Children.Add(b);
                c.Child = sp;
                root.Children.Add(c);
            }

            // 3) Sürüm Geçmişi
            var histCard = MakeCard();
            var histSp = new StackPanel();
            histSp.Children.Add(MakeSectionLabel(Loc.T("VersionLabel") + " Geçmişi / Changelog"));
            foreach (var h in AppInfo.History)
            {
                var row = new TextBlock
                {
                    Text = $"• v{h.Version} ({h.Date}) — {(Loc.IsTurkish ? h.Tr : h.En)}",
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 2)
                };
                row.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                histSp.Children.Add(row);
            }
            histCard.Child = histSp;
            root.Children.Add(histCard);

            return root;
        }

        // =========================================================================
        // UI YARDIMCILARI
        // =========================================================================
        private Border MakeCard()
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };
            b.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            b.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
            return b;
        }

        private UIElement MakePageHeader(string title, string subtitle)
        {
            // Aktif sekmeye göre renkli başlık şeridi
            var meta = NavMeta[Math.Clamp(_activeTabIndex, 0, NavMeta.Length - 1)];
            if (title == Loc.T("LayoutsTitle")) meta = NavMeta[0];
            else if (title == Loc.T("VmTitle")) meta = NavMeta[1];
            else if (title == Loc.T("WsTitle")) meta = NavMeta[2];
            else if (title == Loc.T("HkTitle")) meta = NavMeta[3];
            else if (title == Loc.T("SetTitle")) meta = NavMeta[4];
            else if (title == Loc.T("HelpTitle")) meta = NavMeta[5];

            var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var badge = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.SetResourceReference(Border.BackgroundProperty, meta.BgBrushKey);
            badge.SetResourceReference(Border.BorderBrushProperty, meta.AccentBrushKey);

            var ico = new TextBlock
            {
                Text = meta.Icon,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ico.SetResourceReference(TextBlock.ForegroundProperty, meta.AccentBrushKey);
            badge.Child = ico;

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var t = new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.Bold };
            t.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            var s = new TextBlock { Text = subtitle, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            s.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            textStack.Children.Add(t);
            textStack.Children.Add(s);

            row.Children.Add(badge);
            row.Children.Add(textStack);
            outer.Children.Add(row);

            var accentBar = new Border
            {
                Height = 2.5,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 10, 0, 0),
                Opacity = 0.7
            };
            accentBar.SetResourceReference(Border.BackgroundProperty, meta.AccentBrushKey);
            outer.Children.Add(accentBar);

            return outer;
        }

        private TextBlock MakeSectionLabel(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 4)
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            return tb;
        }
    }
}
