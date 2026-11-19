using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using SnappyLayout.Native;
using SnappyLayout.Services;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Core
{
    public class WindowItem
    {
        public IntPtr Hwnd;
        public string Title;
        public string ClassName;
        public uint Pid;
        public string ExePath;
        public RECT Frame;
        public bool Maximized;
        public bool Minimized;
        public string ExeName => string.IsNullOrEmpty(ExePath) ? "?" : Path.GetFileName(ExePath);
        public override string ToString() => $"{Path.GetFileNameWithoutExtension(ExePath ?? "?")} — {Title}";
    }

    /// <summary>Açık pencereleri, süreç bilgilerini, tarayıcı URL'lerini ve klasör yollarını toplar.</summary>
    public static class WindowCatalog
    {
        private static readonly string[] ChromiumBrowsers = { "chrome.exe", "msedge.exe", "brave.exe", "opera.exe", "vivaldi.exe", "chromium.exe", "yandex.exe", "browser.exe", "arc.exe" };
        private static readonly string[] FirefoxBrowsers = { "firefox.exe", "librewolf.exe", "waterfox.exe", "zen.exe", "floorp.exe" };

        public static bool IsChromium(string exe) => ChromiumBrowsers.Contains(Path.GetFileName(exe ?? "").ToLowerInvariant());
        public static bool IsFirefox(string exe) => FirefoxBrowsers.Contains(Path.GetFileName(exe ?? "").ToLowerInvariant());
        public static bool IsBrowser(string exe) => IsChromium(exe) || IsFirefox(exe);
        public static bool IsExplorer(string exe) => string.Equals(Path.GetFileName(exe ?? ""), "explorer.exe", StringComparison.OrdinalIgnoreCase);

        /// <summary>Geçerli sanal masaüstündeki kullanıcı pencereleri (Z sırasına göre).</summary>
        public static List<WindowItem> GetAppWindows(bool includeMinimized = true)
        {
            var list = new List<WindowItem>();
            EnumWindows((h, l) =>
            {
                try
                {
                    if (!IsWindowVisible(h) || IsCloaked(h)) return true;
                    if (!WindowPlacer.IsValidTarget(h)) return true;
                    if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
                    long ex = GetExStyle(h), st = GetStyle(h);
                    if ((ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0) return true;
                    if ((st & (WS_CAPTION | WS_THICKFRAME)) == 0 && (ex & WS_EX_APPWINDOW) == 0) return true;
                    string title = GetWindowTitle(h);
                    if (string.IsNullOrWhiteSpace(title)) return true;
                    bool min = IsIconic(h);
                    if (min && !includeMinimized) return true;
                    var fr = GetExtendedFrame(h);
                    if (!min && (fr.Width < 60 || fr.Height < 40)) return true;
                    uint pid = GetProcessId(h);
                    list.Add(new WindowItem
                    {
                        Hwnd = h,
                        Title = title,
                        ClassName = NativeMethods.GetClassName(h),
                        Pid = pid,
                        ExePath = GetExePath(pid),
                        Frame = min ? WindowPlacer.GetNormalRectScreen(h) : fr,
                        Maximized = IsZoomed(h),
                        Minimized = min
                    });
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static HashSet<IntPtr> SnapshotHandles()
        {
            var set = new HashSet<IntPtr>();
            EnumWindows((h, l) => { set.Add(h); return true; }, IntPtr.Zero);
            return set;
        }

        // ---------------- Süreç bilgisi ----------------

        public static string GetExePath(uint pid)
        {
            IntPtr hp = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hp == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(hp, 0, sb, ref size) ? sb.ToString() : null;
            }
            finally { CloseHandle(hp); }
        }

        /// <summary>Sürecin komut satırı (ProcessCommandLineInformation, Windows 8.1+).</summary>
        public static string GetCommandLine(uint pid)
        {
            IntPtr hp = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hp == IntPtr.Zero) return null;
            IntPtr buf = IntPtr.Zero;
            try
            {
                int len = 4096;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    buf = Marshal.AllocHGlobal(len);
                    int status = NtQueryInformationProcess(hp, 60, buf, len, out int ret);
                    if (status == 0)
                    {
                        var us = Marshal.PtrToStructure<UNICODE_STRING>(buf);
                        if (us.Buffer == IntPtr.Zero || us.Length == 0) return null;
                        return Marshal.PtrToStringUni(us.Buffer, us.Length / 2);
                    }
                    Marshal.FreeHGlobal(buf); buf = IntPtr.Zero;
                    if (ret <= len) return null;
                    len = ret + 64;
                }
                return null;
            }
            catch { return null; }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
                CloseHandle(hp);
            }
        }

        /// <summary>Komut satırından exe kısmını atıp argümanları döndürür.</summary>
        public static string ExtractArguments(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return null;
            cmd = cmd.Trim();
            int i;
            if (cmd.StartsWith("\""))
            {
                i = cmd.IndexOf('"', 1);
                if (i < 0) return null;
                i++;
            }
            else
            {
                i = cmd.IndexOf(' ');
                if (i < 0) return null;
            }
            var args = cmd.Substring(i).Trim();
            return string.IsNullOrEmpty(args) ? null : args;
        }

        // ---------------- AppUserModelID (Mağaza uygulamaları) ----------------

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY { public Guid fmtid; public uint pid; }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPVARIANT { public ushort vt; public ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint c);
            [PreserveSig] int GetAt(uint i, out PROPERTYKEY k);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            [PreserveSig] int Commit();
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ps);
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PROPVARIANT pv);

        public static string GetAppUserModelId(IntPtr hwnd)
        {
            try
            {
                var iid = typeof(IPropertyStore).GUID;
                if (SHGetPropertyStoreForWindow(hwnd, ref iid, out var ps) != 0 || ps == null) return null;
                try
                {
                    var key = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
                    if (ps.GetValue(ref key, out var pv) != 0) return null;
                    string s = pv.vt == 31 ? Marshal.PtrToStringUni(pv.p) : null;
                    PropVariantClear(ref pv);
                    return s;
                }
                finally { Marshal.ReleaseComObject(ps); }
            }
            catch { return null; }
        }

        public static bool NeedsAumidLaunch(string exe) =>
            !string.IsNullOrEmpty(exe) && (exe.IndexOf("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase));

        // ---------------- Tarayıcı URL'si (UI Automation) ----------------

        public static string GetBrowserUrl(IntPtr hwnd, string exe)
        {
            try
            {
                var task = Task.Run(() => ReadUrl(hwnd, exe));
                if (task.Wait(3000)) return task.Result;
            }
            catch (Exception ex) { Logger.Log("URL read failed: " + ex.Message); }
            return null;
        }

        private static string ReadUrl(IntPtr hwnd, string exe)
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root == null) return null;
            AutomationElement edit = null;
            if (IsFirefox(exe))
                edit = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "urlbar-input"));
            if (edit == null)
            {
                var editCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                var toolbars = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar));
                foreach (AutomationElement tb in toolbars)
                {
                    edit = tb.FindFirst(TreeScope.Descendants, editCond);
                    if (edit != null) break;
                }
                if (edit == null) edit = root.FindFirst(TreeScope.Descendants, editCond);
            }
            if (edit == null) return null;
            if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out object vp))
                return NormalizeUrl(((ValuePattern)vp).Current.Value);
            return null;
        }

        public static string NormalizeUrl(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            v = v.Trim();
            if (v.Contains(' ')) return null; // adres değil, arama metni
            if (v.Contains("://") || v.StartsWith("about:") || v.StartsWith("chrome:") || v.StartsWith("edge:") || v.StartsWith("file:")) return v;
            if (!v.Contains('.') && !v.StartsWith("localhost")) return null;
            return "https://" + v;
        }

        public static bool SameUrl(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string n(string s) => s.Trim().TrimEnd('/').Replace("://www.", "://").ToLowerInvariant();
            return n(a) == n(b);
        }

        // ---------------- Dosya Gezgini klasörü ----------------

        public static Dictionary<IntPtr, string> GetExplorerFolders()
        {
            var res = new Dictionary<IntPtr, string>();
            try
            {
                var t = Type.GetTypeFromProgID("Shell.Application");
                if (t == null) return res;
                dynamic shell = Activator.CreateInstance(t);
                dynamic windows = shell.Windows();
                int count = windows.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic w = windows.Item(i);
                        if (w == null) continue;
                        IntPtr h = new IntPtr((long)w.HWND);
                        string url = w.LocationURL;
                        string path = null;
                        if (!string.IsNullOrEmpty(url) && url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                            path = new Uri(url).LocalPath;
                        else
                        {
                            try { path = w.Document.Folder.Self.Path; } catch { }
                        }
                        if (!string.IsNullOrEmpty(path)) res[h] = path;
                    }
                    catch { }
                }
                Marshal.ReleaseComObject(windows);
                Marshal.ReleaseComObject(shell);
            }
            catch (Exception ex) { Logger.Log("Explorer folders: " + ex.Message); }
            return res;
        }
    }
}
