using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using SnappyLayout.Core;
using SnappyLayout.Models;
using SnappyLayout.Native;
using SnappyLayout.Services;
using SnappyLayout.UI;
using SnappyLayout.UI.Windows;
using static SnappyLayout.Native.NativeMethods;
using Forms = System.Windows.Forms;

namespace SnappyLayout
{
    public partial class App : Application
    {
        public static App Instance => (App)Current;

        public VirtualBorderOverlayManager Overlay { get; private set; }
        public HotkeyService Hotkeys { get; private set; }
        public WindowEventService WindowEvents { get; private set; }
        public MaximizeHoverWatcher HoverWatcher { get; private set; }
        public MaximizeClickInterceptor MaxClickInterceptor { get; private set; }
        public SnapFlyoutWindow Flyout { get; private set; }
        public TopSnapBarWindow TopSnapBar { get; private set; }
        public new MainWindow MainWindow { get; private set; }

        public bool Paused { get; set; }

        private Mutex _singleInstanceMutex;
        private EventWaitHandle _shutdownEvent;
        private Forms.NotifyIcon _trayIcon;
        private DispatcherTimer _hoverUiTimer;
        private IntPtr _hoverCandidateHwnd;
        private long _hoverStartTick;
        private long _lastSeenHoverTick;
        private IntPtr _lastNonSelfForeground;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _shutdownEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "SnappyLayout_YusufKilinc_ShutdownEvent");
            _singleInstanceMutex = new Mutex(true, "SnappyLayout_YusufKilinc_SingleInstance", out bool isNew);
            if (!isNew)
            {
                // Önceki çalışan örneğe kapanma sinyali gönder ve yeni sürümü başlat
                _shutdownEvent.Set();
                try { _singleInstanceMutex.WaitOne(2000); } catch (AbandonedMutexException) { }
            }

            ThreadPool.RegisterWaitForSingleObject(_shutdownEvent, (state, timedOut) =>
            {
                Dispatcher.BeginInvoke(new Action(ExitApplication));
            }, null, -1, true);

            Storage.Load();
            if (Storage.Data.Settings.HoverDelayMs == 450 || Storage.Data.Settings.HoverDelayMs == 220)
                Storage.Data.Settings.HoverDelayMs = 140;
            Loc.Apply(Storage.Data.Settings.Language);
            ThemeManager.Apply(Storage.Data.Settings.Theme);
            if (Storage.Data.Settings.FlyoutEnabled)
                NativeSnapSuppressor.SuppressWindowsNativeFlyout();

            Overlay = new VirtualBorderOverlayManager();
            Flyout = new SnapFlyoutWindow();
            TopSnapBar = new TopSnapBarWindow();

            WindowEvents = new WindowEventService();
            WindowEvents.MoveSizeChanged += dragging => Dispatcher.BeginInvoke(new Action(() => Overlay.SetDragging(dragging)));
            WindowEvents.DragStateChanged += (dragging, hwnd) => Dispatcher.BeginInvoke(new Action(() => TopSnapBar.OnDragStateChanged(dragging, hwnd)));
            WindowEvents.ForegroundChanged += hwnd =>
            {
                if (WindowPlacer.IsValidTarget(hwnd)) _lastNonSelfForeground = hwnd;
            };

            HoverWatcher = new MaximizeHoverWatcher();
            MaxClickInterceptor = new MaximizeClickInterceptor();
            _hoverUiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            _hoverUiTimer.Tick += OnHoverCheckTick;
            _hoverUiTimer.Start();

            Hotkeys = new HotkeyService();
            Hotkeys.Pressed += OnHotkeyPressed;
            Hotkeys.ReRegister();

            SystemEvents.DisplaySettingsChanged += (s, ev) => Dispatcher.BeginInvoke(new Action(() =>
            {
                MonitorService.Refresh();
                Overlay.Refresh();
                MainWindow?.RebuildAllPages();
            }));

            SetupTrayIcon();
            Overlay.Refresh();

            bool startMinimized = e.Args != null && e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            if (!startMinimized)
            {
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            if (MainWindow == null || !MainWindow.IsLoaded)
            {
                MainWindow = new MainWindow();
                MainWindow.Closing += (s, e) =>
                {
                    // Kapat tuşuna basıldığında tepsiye küçült (tam çıkış tepsi menüsünden veya Alt+F4 ile)
                    e.Cancel = true;
                    MainWindow.Hide();
                };
            }
            MainWindow.Show();
            if (MainWindow.WindowState == WindowState.Minimized)
                MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private void OnHoverCheckTick(object sender, EventArgs e)
        {
            if (Paused || !Storage.Data.Settings.FlyoutEnabled)
            {
                _hoverCandidateHwnd = IntPtr.Zero;
                return;
            }

            if (MaximizeHoverWatcher.IsRecentlyHoveringMaxButton())
            {
                NativeSnapSuppressor.DismissActiveNativeFlyout();
            }

            var snap = HoverWatcher.Current;
            if (snap.MouseDown)
            {
                _hoverCandidateHwnd = IntPtr.Zero;
                return;
            }

            if (snap.Window == IntPtr.Zero)
            {
                if (Environment.TickCount64 - _lastSeenHoverTick > 120)
                    _hoverCandidateHwnd = IntPtr.Zero;
                return;
            }

            _lastSeenHoverTick = Environment.TickCount64;
            NativeSnapSuppressor.DismissActiveNativeFlyout();

            if (snap.Window != _hoverCandidateHwnd)
            {
                _hoverCandidateHwnd = snap.Window;
                _hoverStartTick = Environment.TickCount64;
                return;
            }

            long elapsed = Environment.TickCount64 - _hoverStartTick;
            int delay = Math.Clamp(Storage.Data.Settings.HoverDelayMs, 80, 2500);
            if (elapsed >= delay && (!Flyout.IsVisible))
            {
                Flyout.ShowForWindow(_hoverCandidateHwnd, snap.Cursor);
            }
        }

        public void ShowFlyoutForForegroundOrSelf()
        {
            IntPtr fg = GetForegroundWindow();
            if (!WindowPlacer.IsValidTarget(fg)) fg = _lastNonSelfForeground;
            if (!WindowPlacer.IsValidTarget(fg))
            {
                var first = WindowCatalog.GetAppWindows(includeMinimized: false).FirstOrDefault();
                if (first != null) fg = first.Hwnd;
            }
            if (!WindowPlacer.IsValidTarget(fg)) return;

            var r = GetExtendedFrame(fg);
            var anchor = new POINT(r.Right - 40, r.Top + 18);
            Flyout.ShowForWindow(fg, anchor);
        }

        private async void OnHotkeyPressed(string action)
        {
            if (action == HotkeyActions.OpenSettings)
            {
                ShowMainWindow();
                return;
            }
            if (action == HotkeyActions.ShowBorders)
            {
                Overlay.ToggleManual();
                return;
            }
            if (action == HotkeyActions.ToggleVirtual)
            {
                Storage.Data.Settings.VirtualMonitorsEnabled = !Storage.Data.Settings.VirtualMonitorsEnabled;
                Storage.Save();
                Overlay.Refresh();
                Overlay.Flash();
                MainWindow?.RebuildAllPages();
                return;
            }
            if (action == HotkeyActions.ShowFlyout)
            {
                ShowFlyoutForForegroundOrSelf();
                return;
            }

            if (action.StartsWith("vp:"))
            {
                var idStr = action.Substring(3);
                var preset = Storage.Data.VirtualPresets.FirstOrDefault(p => p.Id.ToString() == idStr);
                if (preset != null)
                {
                    GetCursorPos(out var pt);
                    var m = MonitorService.FromPoint(pt);
                    AreaService.SetWeights(m, preset.Weights.ToList());
                    Overlay.Refresh();
                    Overlay.Flash();
                    MainWindow?.RebuildAllPages();
                }
                return;
            }

            if (action.StartsWith("ws:"))
            {
                var idStr = action.Substring(3);
                var ws = Storage.Data.Workspaces.FirstOrDefault(w => w.Id.ToString() == idStr);
                if (ws != null)
                {
                    bool newDesk = false;
                    var mode = Storage.Data.Settings.WorkspaceOpenMode;
                    if (mode == WorkspaceOpenMode.NewDesktop) newDesk = true;
                    else if (mode == WorkspaceOpenMode.Ask)
                    {
                        var dlg = new WorkspaceApplyDialog(ws);
                        if (dlg.ShowDialog() != true) return;
                        newDesk = dlg.OpenInNewDesktop;
                    }
                    await WorkspaceService.ApplyAsync(ws, newDesk);
                }
                return;
            }

            IntPtr hwnd = GetForegroundWindow();
            if (!WindowPlacer.IsValidTarget(hwnd)) hwnd = _lastNonSelfForeground;
            if (!WindowPlacer.IsValidTarget(hwnd)) return;

            if (action == HotkeyActions.VirtualMaximize)
            {
                var area = AreaService.AreaForWindow(hwnd);
                WindowPlacer.VirtualMaximize(hwnd, area, allowToggle: true);
            }
            else if (action == HotkeyActions.PhysicalMaximize)
            {
                var m = MonitorService.FromWindow(hwnd);
                WindowPlacer.PhysicalMaximize(hwnd, m);
            }
            else if (action == HotkeyActions.RestoreWindow)
            {
                WindowPlacer.RestoreFromVirtual(hwnd);
            }
            else if (action == HotkeyActions.NextSegment || action == HotkeyActions.PrevSegment)
            {
                var m = MonitorService.FromWindow(hwnd);
                var areas = AreaService.GetAreas(m);
                if (areas.Count > 1)
                {
                    var cur = AreaService.AreaForWindow(hwnd);
                    int idx = Math.Max(0, cur.SegmentIndex);
                    int next = action == HotkeyActions.NextSegment
                        ? (idx + 1) % areas.Count
                        : (idx - 1 + areas.Count) % areas.Count;
                    WindowPlacer.VirtualMaximize(hwnd, areas[next], allowToggle: false);
                }
            }
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Visible = true,
                Text = $"Snappy Layout {AppInfo.VersionText} — {AppInfo.Author}"
            };
            _trayIcon.DoubleClick += (s, e) => Dispatcher.BeginInvoke(new Action(ShowMainWindow));

            RebuildTrayMenu();
            Loc.LanguageChanged += RebuildTrayMenu;
        }

        private void RebuildTrayMenu()
        {
            if (_trayIcon == null) return;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add($"Snappy Layout {AppInfo.VersionText} • {AppInfo.Author} ({AppInfo.AuthorDateTr})").Enabled = false;
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(Loc.T("TrayOpen"), null, (s, e) => Dispatcher.BeginInvoke(new Action(ShowMainWindow)));
            menu.Items.Add(Loc.T("TrayShowFlyout"), null, (s, e) => Dispatcher.BeginInvoke(new Action(ShowFlyoutForForegroundOrSelf)));
            menu.Items.Add(Loc.T("TrayShowBorders"), null, (s, e) => Dispatcher.BeginInvoke(new Action(() => Overlay.Flash())));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(Loc.T("TrayExit"), null, (s, e) => Dispatcher.BeginInvoke(new Action(ExitApplication)));
            _trayIcon.ContextMenuStrip = menu;
        }

        private static Icon CreateTrayIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(Color.FromArgb(255, 37, 99, 235));
                g.FillEllipse(bg, 1, 1, 30, 30);
                using var pen = new Pen(Color.White, 2.2f);
                g.DrawRectangle(pen, 7, 8, 8, 16);
                g.DrawRectangle(pen, 17, 8, 8, 7);
                g.DrawRectangle(pen, 17, 17, 8, 7);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        public void ExitApplication()
        {
            try
            {
                _hoverUiTimer?.Stop();
                MaxClickInterceptor?.Dispose();
                HoverWatcher?.Dispose();
                WindowEvents?.Dispose();
                Hotkeys?.Dispose();
                Overlay?.Dispose();
                Flyout?.Close();
                TopSnapBar?.Close();
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                Storage.SaveNow();
                NativeSnapSuppressor.RestoreWindowsNativeFlyout();
            }
            catch { }
            Shutdown();
        }
    }
}
