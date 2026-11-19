using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SnappyLayout.Core;
using SnappyLayout.Models;
using SnappyLayout.Native;
using SnappyLayout.Services;

namespace SnappyLayout.UI.Windows
{
    /// <summary>
    /// Çalışma alanı kaydetme iletişim kutusu:
    /// - Fiziksel monitör ve kapsam (tüm fiziksel monitör veya belirli sanal monitör) seçimi
    /// - Kapsam içindeki açık uygulamaları, konumlarını ve tarayıcı URL adreslerini otomatik algılama + düzenleme
    /// - Manuel uygulama / tarayıcı URL'si ekleme
    /// </summary>
    public sealed class WorkspaceCaptureDialog : Window
    {
        public Workspace ResultWorkspace { get; private set; }

        private readonly List<MonitorInfo> _monitors;
        private readonly TextBox _nameBox;
        private readonly ComboBox _monitorCombo;
        private readonly ComboBox _scopeCombo;
        private readonly CheckBox _includeAppsCheck;
        private readonly StackPanel _appsListPanel;

        private sealed class EditableAppRow
        {
            public CheckBox EnabledCheck;
            public SavedApp App;
            public TextBox UrlOrArgsBox;
        }

        private readonly List<EditableAppRow> _rows = new List<EditableAppRow>();

        public WorkspaceCaptureDialog(MonitorInfo initialMonitor)
        {
            _monitors = MonitorService.GetMonitors(true);
            Title = Loc.T("WsCaptureTitle");
            Width = 700;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "CardBgBrush");

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1) Ad ve Monitör
            var formGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            AddFormLabel(formGrid, 0, Loc.T("WsNameLabel"));
            _nameBox = new TextBox
            {
                Text = $"{Loc.T("NavWorkspaces")} {Storage.Data.Workspaces.Count + 1}",
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8, 5, 8, 5)
            };
            Grid.SetRow(_nameBox, 0); Grid.SetColumn(_nameBox, 1);
            formGrid.Children.Add(_nameBox);

            AddFormLabel(formGrid, 1, Loc.T("WsMonitorLabel"));
            _monitorCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(8, 5, 8, 5) };
            foreach (var m in _monitors) _monitorCombo.Items.Add(m.DisplayName);
            int initIdx = initialMonitor != null ? Math.Max(0, _monitors.FindIndex(x => x.Key == initialMonitor.Key)) : 0;
            _monitorCombo.SelectedIndex = initIdx;
            _monitorCombo.SelectionChanged += (s, e) => { PopulateScopes(); DetectAppsInScope(); };
            Grid.SetRow(_monitorCombo, 1); Grid.SetColumn(_monitorCombo, 1);
            formGrid.Children.Add(_monitorCombo);

            AddFormLabel(formGrid, 2, Loc.T("WsScopeLabel"));
            _scopeCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(8, 5, 8, 5) };
            _scopeCombo.SelectionChanged += (s, e) => DetectAppsInScope();
            Grid.SetRow(_scopeCombo, 2); Grid.SetColumn(_scopeCombo, 1);
            formGrid.Children.Add(_scopeCombo);

            Grid.SetRow(formGrid, 0);
            root.Children.Add(formGrid);

            // 2) Uygulamaları dahil et onay kutusu
            _includeAppsCheck = new CheckBox
            {
                Content = Loc.T("WsIncludeAppsCheck"),
                IsChecked = true,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _includeAppsCheck.Checked += (s, e) => UpdateAppsPanelState();
            _includeAppsCheck.Unchecked += (s, e) => UpdateAppsPanelState();
            Grid.SetRow(_includeAppsCheck, 1);
            root.Children.Add(_includeAppsCheck);

            // 3) Başlık + Manuel Uygulama Ekle
            var listHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblDet = new TextBlock
            {
                Text = Loc.T("WsDetectedWindows"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            lblDet.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            var btnManual = new Button
            {
                Content = Loc.T("WsAddManualApp"),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11.5
            };
            btnManual.Click += OnAddManualApp;

            Grid.SetColumn(lblDet, 0);
            Grid.SetColumn(btnManual, 1);
            listHeader.Children.Add(lblDet);
            listHeader.Children.Add(btnManual);
            Grid.SetRow(listHeader, 2);
            root.Children.Add(listHeader);

            // 4) Uygulama listesi
            var scrollBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10)
            };
            scrollBorder.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
            scrollBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            _appsListPanel = new StackPanel();
            scrollBorder.Child = new ScrollViewer
            {
                Content = _appsListPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scrollBorder, 3);
            root.Children.Add(scrollBorder);

            // 5) Alt düğmeler
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var btnCancel = new Button { Content = Loc.T("Cancel"), Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(0, 0, 8, 0) };
            btnCancel.Click += (s, e) => DialogResult = false;

            var btnSave = new Button { Content = Loc.T("Save"), Padding = new Thickness(22, 7, 22, 7), FontWeight = FontWeights.SemiBold };
            btnSave.Click += OnSaveClicked;

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnSave);
            Grid.SetRow(btnRow, 4);
            root.Children.Add(btnRow);

            Content = root;

            Loaded += (s, e) =>
            {
                ThemeManager.ApplyWindowChrome(this);
                PopulateScopes();
                DetectAppsInScope();
            };
        }

        private void AddFormLabel(Grid g, int row, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 8),
                FontWeight = FontWeights.SemiBold
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetRow(tb, row); Grid.SetColumn(tb, 0);
            g.Children.Add(tb);
        }

        private MonitorInfo SelectedMonitor => _monitors[Math.Clamp(_monitorCombo.SelectedIndex, 0, _monitors.Count - 1)];

        private int SelectedSegment => _scopeCombo.SelectedIndex <= 0 ? -1 : _scopeCombo.SelectedIndex - 1;

        private void PopulateScopes()
        {
            _scopeCombo.Items.Clear();
            var m = SelectedMonitor;
            _scopeCombo.Items.Add($"{Loc.T("WsScopeFull")} ({m.WorkArea.Width}×{m.WorkArea.Height})");
            var weights = AreaService.GetStoredWeights(m);
            if (weights.Count > 1)
            {
                var rects = AreaService.SplitRect(m.WorkArea, weights);
                for (int i = 0; i < rects.Count; i++)
                    _scopeCombo.Items.Add($"{Loc.T("VirtualMonitor")} #{i + 1} ({rects[i].Width}×{rects[i].Height})");
            }
            _scopeCombo.SelectedIndex = 0;
        }

        private void UpdateAppsPanelState()
        {
            bool inc = _includeAppsCheck.IsChecked == true;
            _appsListPanel.IsEnabled = inc;
            _appsListPanel.Opacity = inc ? 1.0 : 0.45;
        }

        private void DetectAppsInScope()
        {
            if (_appsListPanel == null) return;
            _appsListPanel.Children.Clear();
            _rows.Clear();

            var m = SelectedMonitor;
            int seg = SelectedSegment;
            var weights = AreaService.GetStoredWeights(m);
            var scope = WorkspaceService.GetScopeRect(m, weights, seg);
            var wins = WorkspaceService.GetWindowsInScope(m, seg);
            var folders = wins.Any(w => WindowCatalog.IsExplorer(w.ExePath)) ? WindowCatalog.GetExplorerFolders() : new Dictionary<IntPtr, string>();

            foreach (var w in Enumerable.Reverse(wins))
            {
                var app = new SavedApp
                {
                    ExePath = w.ExePath,
                    Title = w.Title,
                    ClassName = w.ClassName,
                    IsMaximized = w.Maximized,
                    X = Math.Clamp((double)(w.Frame.Left - scope.Left) / scope.Width, 0, 1),
                    Y = Math.Clamp((double)(w.Frame.Top - scope.Top) / scope.Height, 0, 1),
                    W = Math.Clamp((double)w.Frame.Width / scope.Width, 0.05, 1),
                    H = Math.Clamp((double)w.Frame.Height / scope.Height, 0.05, 1)
                };
                if (WindowCatalog.NeedsAumidLaunch(w.ExePath))
                    app.AppUserModelId = WindowCatalog.GetAppUserModelId(w.Hwnd);
                if (WindowCatalog.IsBrowser(w.ExePath))
                {
                    app.IsBrowser = true;
                    app.Url = WindowCatalog.GetBrowserUrl(w.Hwnd, w.ExePath) ?? "https://";
                }
                else if (WindowCatalog.IsExplorer(w.ExePath) && folders.TryGetValue(w.Hwnd, out var f))
                {
                    app.FolderPath = f;
                }
                else
                {
                    var args = WindowCatalog.ExtractArguments(WindowCatalog.GetCommandLine(w.Pid));
                    if (args != null && !args.Contains("--type=")) app.Arguments = args;
                }

                AddRowToUI(app);
            }
        }

        private void AddRowToUI(SavedApp app)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            card.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var sp = new StackPanel();
            var cb = new CheckBox
            {
                IsChecked = true,
                Content = $"{Path.GetFileNameWithoutExtension(app.ExePath ?? "App")} — {app.Title}  [{Math.Round(app.W * 100)}%×{Math.Round(app.H * 100)}%]",
                FontWeight = FontWeights.SemiBold
            };
            sp.Children.Add(cb);

            TextBox extraBox = null;
            if (app.IsBrowser || !string.IsNullOrEmpty(app.FolderPath) || !string.IsNullOrEmpty(app.Arguments))
            {
                var subGrid = new Grid { Margin = new Thickness(22, 5, 0, 0) };
                subGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                subGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = app.IsBrowser ? "URL Adresi:" : (!string.IsNullOrEmpty(app.FolderPath) ? "Klasör Yolu:" : "Argümanlar:"),
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

                extraBox = new TextBox
                {
                    Text = app.IsBrowser ? (app.Url ?? "") : (app.FolderPath ?? app.Arguments ?? ""),
                    FontSize = 11.5,
                    Padding = new Thickness(6, 3, 6, 3)
                };

                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(extraBox, 1);
                subGrid.Children.Add(lbl);
                subGrid.Children.Add(extraBox);
                sp.Children.Add(subGrid);
            }

            card.Child = sp;
            _appsListPanel.Children.Add(card);
            _rows.Add(new EditableAppRow { EnabledCheck = cb, App = app, UrlOrArgsBox = extraBox });
        }

        private void OnAddManualApp(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Programs (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = Loc.T("WsAddManualApp")
            };
            if (ofd.ShowDialog(this) == true)
            {
                bool isBr = WindowCatalog.IsBrowser(ofd.FileName);
                var app = new SavedApp
                {
                    ExePath = ofd.FileName,
                    Title = Path.GetFileNameWithoutExtension(ofd.FileName),
                    IsBrowser = isBr,
                    Url = isBr ? "https://www.google.com" : null,
                    Arguments = isBr ? null : "",
                    X = 0.0, Y = 0.0, W = 0.5, H = 1.0
                };
                AddRowToUI(app);
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            var m = SelectedMonitor;
            var ws = new Workspace
            {
                Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? "Workspace" : _nameBox.Text.Trim(),
                MonitorKey = m.Key,
                MonitorName = m.ShortName,
                MonitorIndex = m.Index,
                Weights = AreaService.GetStoredWeights(m),
                ScopeSegment = SelectedSegment,
                IncludeApps = _includeAppsCheck.IsChecked == true,
                Created = DateTime.Now
            };

            if (ws.IncludeApps)
            {
                foreach (var r in _rows)
                {
                    if (r.EnabledCheck.IsChecked != true) continue;
                    if (r.UrlOrArgsBox != null)
                    {
                        string val = r.UrlOrArgsBox.Text.Trim();
                        if (r.App.IsBrowser) r.App.Url = val;
                        else if (WindowCatalog.IsExplorer(r.App.ExePath)) r.App.FolderPath = val;
                        else r.App.Arguments = val;
                    }
                    ws.Apps.Add(r.App);
                }
            }

            ResultWorkspace = ws;
            DialogResult = true;
        }
    }

    /// <summary>
    /// Çalışma alanı uygulanırken "Mevcut Masaüstünde Aç" veya "Yeni Masaüstünde Aç" seçimini soran pencere.
    /// </summary>
    public sealed class WorkspaceApplyDialog : Window
    {
        public bool OpenInNewDesktop { get; private set; }

        public WorkspaceApplyDialog(Workspace ws)
        {
            Title = Loc.T("WsAskTitle") + " — " + ws.Name;
            Width = 460;
            Height = 230;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            SetResourceReference(BackgroundProperty, "CardBgBrush");

            var root = new StackPanel { Margin = new Thickness(22) };
            var title = new TextBlock
            {
                Text = ws.Name,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var prompt = new TextBlock
            {
                Text = Loc.T("WsAskPrompt"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };
            prompt.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnCurrent = new Button
            {
                Content = "🖥  " + Loc.T("WsApplyCurrent"),
                Padding = new Thickness(12, 12, 12, 12),
                Margin = new Thickness(0, 0, 6, 0),
                FontWeight = FontWeights.SemiBold
            };
            btnCurrent.Click += (s, e) => { OpenInNewDesktop = false; DialogResult = true; };

            var btnNew = new Button
            {
                Content = "✨  " + Loc.T("WsApplyNewDesktop"),
                Padding = new Thickness(12, 12, 12, 12),
                Margin = new Thickness(6, 0, 0, 0),
                FontWeight = FontWeights.SemiBold
            };
            btnNew.Click += (s, e) => { OpenInNewDesktop = true; DialogResult = true; };

            Grid.SetColumn(btnCurrent, 0);
            Grid.SetColumn(btnNew, 1);
            btnGrid.Children.Add(btnCurrent);
            btnGrid.Children.Add(btnNew);

            root.Children.Add(title);
            root.Children.Add(prompt);
            root.Children.Add(btnGrid);
            Content = root;

            Loaded += (s, e) => ThemeManager.ApplyWindowChrome(this);
        }
    }
}
