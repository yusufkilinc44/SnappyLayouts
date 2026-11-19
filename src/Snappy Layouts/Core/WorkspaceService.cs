using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SnappyLayout.Models;
using SnappyLayout.Native;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    /// <summary>Çalışma alanlarını (sanal monitör düzeni + uygulamalar + URL'ler) kaydeder ve geri yükler.</summary>
    public static class WorkspaceService
    {
        public static RECT GetScopeRect(MonitorInfo m, IList<double> weights, int segment)
        {
            if (segment < 0) return m.WorkArea;
            var rects = AreaService.SplitRect(m.WorkArea, weights);
            return segment < rects.Count ? rects[segment] : m.WorkArea;
        }

        /// <summary>Kapsam alanı içinde (merkezi alanda olan) pencereler.</summary>
        public static List<WindowItem> GetWindowsInScope(MonitorInfo m, int segment)
        {
            var weights = AreaService.GetStoredWeights(m);
            var scope = GetScopeRect(m, weights, segment);
            var all = WindowCatalog.GetAppWindows(includeMinimized: false);
            return all.Where(w =>
            {
                var c = w.Frame.Center;
                if (w.Maximized)
                    return segment < 0 && MonitorService.FromWindow(w.Hwnd).Handle == m.Handle;
                return scope.Contains(c);
            }).ToList();
        }

        public static async Task<Workspace> CaptureAsync(string name, MonitorInfo m, int segment, bool includeApps, IEnumerable<WindowItem> windows)
        {
            var weights = AreaService.GetStoredWeights(m);
            var ws = new Workspace
            {
                Name = name,
                MonitorKey = m.Key,
                MonitorName = m.ShortName,
                MonitorIndex = m.Index,
                Weights = weights,
                ScopeSegment = segment,
                IncludeApps = includeApps,
                Created = DateTime.Now
            };
            if (!includeApps || windows == null) return ws;

            var scope = GetScopeRect(m, weights, segment);
            var items = windows.ToList();
            var folders = items.Any(w => WindowCatalog.IsExplorer(w.ExePath)) ? WindowCatalog.GetExplorerFolders() : new Dictionary<IntPtr, string>();

            var apps = await Task.Run(() =>
            {
                var list = new List<SavedApp>();
                // Z sırasının tersi: en alttaki pencere önce açılsın, en üstteki en son
                foreach (var w in Enumerable.Reverse(items))
                {
                    try { list.Add(Describe(w, scope, folders)); }
                    catch (Exception ex) { Logger.Log("Describe failed: " + ex.Message); }
                }
                return list;
            });
            ws.Apps = apps;
            return ws;
        }

        private static SavedApp Describe(WindowItem w, RECT scope, Dictionary<IntPtr, string> folders)
        {
            var app = new SavedApp
            {
                ExePath = w.ExePath,
                Title = w.Title,
                ClassName = w.ClassName,
                IsMaximized = w.Maximized,
                X = (double)(w.Frame.Left - scope.Left) / scope.Width,
                Y = (double)(w.Frame.Top - scope.Top) / scope.Height,
                W = (double)w.Frame.Width / scope.Width,
                H = (double)w.Frame.Height / scope.Height
            };
            if (WindowCatalog.NeedsAumidLaunch(w.ExePath))
                app.AppUserModelId = WindowCatalog.GetAppUserModelId(w.Hwnd);
            if (WindowCatalog.IsBrowser(w.ExePath))
            {
                app.IsBrowser = true;
                app.Url = WindowCatalog.GetBrowserUrl(w.Hwnd, w.ExePath);
            }
            else if (WindowCatalog.IsExplorer(w.ExePath))
            {
                if (folders.TryGetValue(w.Hwnd, out var f)) app.FolderPath = f;
            }
            else
            {
                var args = WindowCatalog.ExtractArguments(WindowCatalog.GetCommandLine(w.Pid));
                // Electron/Chromium alt süreç bayraklarını kaydetme
                if (args != null && !args.Contains("--type=")) app.Arguments = args;
            }
            return app;
        }

        // ======================= GERİ YÜKLEME =======================

        public static async Task<(int Placed, int Launched, int Failed)> ApplyAsync(Workspace ws, bool newDesktop, Action<string> progress = null)
        {
            int placed = 0, launched = 0, failed = 0;
            var monitors = MonitorService.GetMonitors(true);
            var m = MonitorService.FindByKey(ws.MonitorKey)
                    ?? monitors.FirstOrDefault(x => x.Index == ws.MonitorIndex)
                    ?? monitors.FirstOrDefault(x => x.IsPrimary) ?? monitors[0];

            // 1) Sanal monitör düzenini uygula
            if (ws.Weights != null && ws.Weights.Count > 0)
            {
                AreaService.SetWeights(m, ws.Weights);
                if (ws.Weights.Count > 1 && !Storage.Data.Settings.VirtualMonitorsEnabled)
                {
                    Storage.Data.Settings.VirtualMonitorsEnabled = true;
                    Storage.Save();
                }
                App.Instance?.Overlay?.Refresh();
                App.Instance?.Overlay?.Flash();
            }
            if (!ws.IncludeApps || ws.Apps.Count == 0) return (0, 0, 0);

            // 2) Gerekirse yeni sanal masaüstü (Win+Ctrl+D)
            if (newDesktop)
            {
                progress?.Invoke(Loc.T("WsCreatingDesktop"));
                CreateNewVirtualDesktop();
                await Task.Delay(900);
            }

            var scope = GetScopeRect(m, ws.Weights, ws.ScopeSegment);
            var used = new HashSet<IntPtr>();
            var existing = newDesktop ? new List<WindowItem>() : WindowCatalog.GetAppWindows();
            Dictionary<IntPtr, string> folders = null;

            foreach (var app in ws.Apps)
            {
                try
                {
                    progress?.Invoke(app.DisplayName);
                    IntPtr hwnd = IntPtr.Zero;
                    if (!newDesktop)
                    {
                        if (folders == null && app.FolderPath != null) folders = WindowCatalog.GetExplorerFolders();
                        hwnd = FindMatch(existing, app, used, folders);
                    }
                    if (hwnd == IntPtr.Zero)
                    {
                        hwnd = await LaunchAndWaitAsync(app, used);
                        if (hwnd != IntPtr.Zero) launched++;
                    }
                    if (hwnd == IntPtr.Zero) { failed++; continue; }
                    used.Add(hwnd);
                    await PlaceAppAsync(hwnd, app, scope, m, ws.ScopeSegment);
                    placed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Logger.Log("Apply app failed: " + app.DisplayName + " " + ex.Message);
                }
            }
            return (placed, launched, failed);
        }

        private static async Task PlaceAppAsync(IntPtr hwnd, SavedApp app, RECT scope, MonitorInfo m, int scopeSegment)
        {
            // Çalışma alanı, mevcut Snap Layout şablonları sonradan değişmiş veya silinmiş olsa bile
            // pencereleri her zaman kaydedildiği kesin konumlara (ws.Weights + scope + X/Y/W/H) yerleştirir.
            if (app.IsMaximized)
            {
                if (scopeSegment >= 0)
                    WindowPlacer.Place(hwnd, scope, activate: true);
                else
                    WindowPlacer.PhysicalMaximize(hwnd, m);
                return;
            }
            int left = scope.Left + (int)Math.Round(app.X * scope.Width);
            int top = scope.Top + (int)Math.Round(app.Y * scope.Height);
            int right = scope.Left + (int)Math.Round((app.X + app.W) * scope.Width);
            int bottom = scope.Top + (int)Math.Round((app.Y + app.H) * scope.Height);

            // Monitör çözünürlüğü hafif değiştiyse bile pencere ekran dışına taşmasın
            var wa = m.WorkArea;
            if (right - left < 120) right = Math.Min(wa.Right, left + 400);
            if (bottom - top < 80) bottom = Math.Min(wa.Bottom, top + 300);
            var target = new RECT(
                Math.Clamp(left, wa.Left, Math.Max(wa.Left, wa.Right - 120)),
                Math.Clamp(top, wa.Top, Math.Max(wa.Top, wa.Bottom - 80)),
                Math.Clamp(right, wa.Left + 120, wa.Right),
                Math.Clamp(bottom, wa.Top + 80, wa.Bottom));

            WindowPlacer.Place(hwnd, target, activate: true);
            // Bazı uygulamalar açıldıktan hemen sonra kendi kayıtlı konumlarına döner → tekrar uygula
            await Task.Delay(600);
            if (IsWindow(hwnd) && !GetExtendedFrame(hwnd).SameAs(target, 4))
                WindowPlacer.Place(hwnd, target, activate: false);
        }

        private static IntPtr FindMatch(List<WindowItem> existing, SavedApp app, HashSet<IntPtr> used, Dictionary<IntPtr, string> folders)
        {
            var candidates = existing.Where(w => !used.Contains(w.Hwnd) && IsWindow(w.Hwnd)).ToList();
            if (!string.IsNullOrEmpty(app.AppUserModelId))
            {
                var c = candidates.FirstOrDefault(w => string.Equals(WindowCatalog.GetAppUserModelId(w.Hwnd), app.AppUserModelId, StringComparison.OrdinalIgnoreCase));
                return c?.Hwnd ?? IntPtr.Zero;
            }
            var sameExe = candidates.Where(w => string.Equals(w.ExePath, app.ExePath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (sameExe.Count == 0) return IntPtr.Zero;

            if (app.IsBrowser)
            {
                if (string.IsNullOrEmpty(app.Url)) return IntPtr.Zero;
                foreach (var w in sameExe)
                    if (WindowCatalog.SameUrl(WindowCatalog.GetBrowserUrl(w.Hwnd, w.ExePath), app.Url)) return w.Hwnd;
                return IntPtr.Zero; // farklı sayfadaki tarayıcı penceresine dokunma, yeni pencere aç
            }
            if (!string.IsNullOrEmpty(app.FolderPath))
            {
                foreach (var w in sameExe)
                    if (folders != null && folders.TryGetValue(w.Hwnd, out var f) && string.Equals(f.TrimEnd('\\'), app.FolderPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        return w.Hwnd;
                return IntPtr.Zero;
            }
            if (WindowCatalog.IsExplorer(app.ExePath)) return IntPtr.Zero;
            var exact = sameExe.FirstOrDefault(w => w.Title == app.Title);
            return (exact ?? sameExe[0]).Hwnd;
        }

        private static async Task<IntPtr> LaunchAndWaitAsync(SavedApp app, HashSet<IntPtr> used)
        {
            var before = WindowCatalog.SnapshotHandles();
            if (!Launch(app)) return IntPtr.Zero;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 20000)
            {
                await Task.Delay(300);
                var now = WindowCatalog.GetAppWindows(includeMinimized: false);
                foreach (var w in now)
                {
                    if (before.Contains(w.Hwnd) || used.Contains(w.Hwnd)) continue;
                    bool match;
                    if (!string.IsNullOrEmpty(app.AppUserModelId))
                        match = string.Equals(WindowCatalog.GetAppUserModelId(w.Hwnd), app.AppUserModelId, StringComparison.OrdinalIgnoreCase)
                                || w.ClassName == "ApplicationFrameWindow";
                    else if (WindowCatalog.IsExplorer(app.ExePath))
                        match = w.ClassName == "CabinetWClass";
                    else
                        match = string.Equals(w.ExePath, app.ExePath, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(Path.GetFileName(w.ExePath ?? ""), Path.GetFileName(app.ExePath ?? ""), StringComparison.OrdinalIgnoreCase);
                    if (match)
                    {
                        await Task.Delay(400); // pencere ilk boyutlandırmasını bitirsin
                        return w.Hwnd;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private static bool Launch(SavedApp app)
        {
            try
            {
                ProcessStartInfo psi;
                if (!string.IsNullOrEmpty(app.AppUserModelId))
                    psi = new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + app.AppUserModelId);
                else if (string.IsNullOrEmpty(app.ExePath) || !File.Exists(app.ExePath))
                    return false;
                else if (app.IsBrowser)
                {
                    string url = string.IsNullOrEmpty(app.Url) ? "" : " \"" + app.Url + "\"";
                    psi = new ProcessStartInfo(app.ExePath, (WindowCatalog.IsFirefox(app.ExePath) ? "-new-window" : "--new-window") + url);
                }
                else if (WindowCatalog.IsExplorer(app.ExePath))
                    psi = new ProcessStartInfo("explorer.exe", string.IsNullOrEmpty(app.FolderPath) ? "" : "\"" + app.FolderPath + "\"");
                else
                    psi = new ProcessStartInfo(app.ExePath, app.Arguments ?? "");
                psi.UseShellExecute = true;
                if (!string.IsNullOrEmpty(app.ExePath) && File.Exists(app.ExePath) && string.IsNullOrEmpty(app.AppUserModelId))
                    psi.WorkingDirectory = Path.GetDirectoryName(app.ExePath);
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("Launch failed: " + app.DisplayName + " " + ex.Message);
                return false;
            }
        }

        /// <summary>Windows'un standart kısayolu (Win+Ctrl+D) ile yeni sanal masaüstü oluşturur ve ona geçer.</summary>
        public static void CreateNewVirtualDesktop()
        {
            const ushort VK_LCONTROL = 0xA2;
            var inputs = new[]
            {
                Key((ushort)VK_LWIN, false), Key(VK_LCONTROL, false), Key((ushort)VK_D, false),
                Key((ushort)VK_D, true), Key(VK_LCONTROL, true), Key((ushort)VK_LWIN, true)
            };
            SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
        }

        private static INPUT Key(ushort vk, bool up)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (vk == VK_LWIN ? KEYEVENTF_EXTENDEDKEY : 0) } }
            };
        }
    }
}
