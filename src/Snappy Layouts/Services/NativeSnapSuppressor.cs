using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SnappyLayout.Core;
using SnappyLayout.Native;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Services
{
    /// <summary>
    /// Windows 11'in yerleşik maksimize tuşu üstü Snap Layouts menüsünü (EnableSnapAssistFlyout / EnableSnapBar)
    /// Snappy Layouts çalışırken devre dışı bırakır; Snappy Layouts kapatıldığında veya duraklatıldığında eski haline döndürür.
    /// Ayrıca ImmersiveShell SettingsCache COM servisini tetikleyerek Explorer yeniden başlatılmadan anında devreye girmesini sağlar.
    /// </summary>
    public static class NativeSnapSuppressor
    {
        private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const uint SPI_GETWINARRANGING = 0x0082;
        private const uint SPI_SETWINARRANGING = 0x0083;
        private const uint SPIF_UPDATEINIFILE = 0x0001;
        private const uint SPIF_SENDCHANGE = 0x0002;

        private static int? _origSnapAssistFlyout;
        private static int? _origSnapBar;
        private static bool? _origWinArranging;
        private static bool _applied;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out bool pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        private static readonly Guid CLSID_ImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        private static readonly Guid IID_IServiceProvider = new Guid("6D5140C1-7436-11CE-8034-00AA006009FA");
        private static readonly Guid SID_SettingsCache1 = new Guid("86FB57A6-3C81-4F40-A971-6280BE4E526E");
        private static readonly Guid SID_SettingsCache2 = new Guid("E78295EF-4439-494F-9D40-16E91A4CBF03");
        private static readonly Guid IID_ISettingsCache = new Guid("B1BED1AE-63DA-4687-8063-C89EC276D28A");

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IComServiceProvider
        {
            [PreserveSig]
            int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int RefreshSettingDelegate(IntPtr thisPtr, int settingId);

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(
            ref Guid rclsid,
            IntPtr pUnkOuter,
            uint dwClsContext,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IComServiceProvider ppv);

        /// <summary>
        /// Explorer yeniden başlatıldıysa veya Windows uyku sonrası ayarları sıfırladıysa
        /// native Snap bastırmayı hızlıca yeniden uygular (zaten 0 ise hiçbir şey yapmaz).
        /// </summary>
        public static void EnsureSuppressed()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, writable: false);
                bool flyoutOn = !(key?.GetValue("EnableSnapAssistFlyout") is int v1 && v1 == 0);
                bool barOn = !(key?.GetValue("EnableSnapBar") is int v2 && v2 == 0);
                bool arrOn = SystemParametersInfo(SPI_GETWINARRANGING, 0, out bool arr, 0) && arr;
                if (flyoutOn || barOn || arrOn)
                {
                    SuppressWindowsNativeFlyout();
                }
            }
            catch { }
        }

        public static void SuppressWindowsNativeFlyout()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, writable: true);
                if (key == null) return;

                if (!_applied)
                {
                    if (key.GetValue("EnableSnapAssistFlyout") is int v1 && v1 != 0) _origSnapAssistFlyout = v1;
                    if (key.GetValue("EnableSnapBar") is int v2 && v2 != 0) _origSnapBar = v2;
                    if (SystemParametersInfo(SPI_GETWINARRANGING, 0, out bool origArr, 0) && origArr)
                        _origWinArranging = true;
                    _applied = true;
                }

                key.SetValue("EnableSnapAssistFlyout", 0, RegistryValueKind.DWord);
                key.SetValue("EnableSnapBar", 0, RegistryValueKind.DWord);

                // Windows'un kendi kenara sürükleme (Aero Snap) önizleme ve yaslama davranışını devre dışı bırak
                SystemParametersInfo(SPI_SETWINARRANGING, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                RefreshExplorerSettingsCache();
                BroadcastSettingChange();
                DismissActiveNativeFlyout();
            }
            catch (Exception ex)
            {
                Logger.Log("NativeSnapSuppressor suppress warning: " + ex.Message);
            }
        }

        public static void RestoreWindowsNativeFlyout()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, writable: true);
                if (key != null)
                {
                    key.SetValue("EnableSnapAssistFlyout", _origSnapAssistFlyout ?? 1, RegistryValueKind.DWord);
                    key.SetValue("EnableSnapBar", _origSnapBar ?? 1, RegistryValueKind.DWord);
                }

                using var deskKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
                deskKey?.SetValue("WindowArrangementActive", "1", RegistryValueKind.String);

                // Windows'un kendi kenara/tepeye sürükleme (Aero Snap) ve Snap Layouts ayarlarını anında geri yükle
                SystemParametersInfo(SPI_SETWINARRANGING, (_origWinArranging ?? true) ? 1u : 0u, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                RefreshExplorerSettingsCache();
                BroadcastSettingChange();
            }
            catch (Exception ex)
            {
                Logger.Log("NativeSnapSuppressor restore warning: " + ex.Message);
            }
        }

        public static bool IsNativeSnapWindowClass(string cls)
        {
            return cls == "XamlExplorerHostIslandWindow" || cls == "Shell_LightDismissOverlay";
        }

        /// <summary>
        /// Yalnızca Windows 11'in gerçek Snap Layouts Flyout veya üst Snap Bar pencerelerini tanımlar.
        /// Task View (Win+Tab), Sanal Masaüstü Seçici (Desktops), Başlat Menüsü, Bildirim Merkezi gibi
        /// diğer XamlExplorerHostIslandWindow pencerelerine KESİNLİKLE dokunmaz.
        /// </summary>
        public static bool IsNativeSnapPopup(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            string cls = NativeMethods.GetClassName(hwnd);
            if (!IsNativeSnapWindowClass(cls)) return false;

            if (!GetWindowRect(hwnd, out var r) || r.Width <= 0 || r.Height <= 0) return false;
            var mon = MonitorService.FromPoint(r.Center);
            if (mon == null) return false;

            // Tam ekran Task View / Sanal Masaüstü görünümüne ASLA dokunma
            if (r.Width >= mon.Bounds.Width * 0.80 && r.Height >= mon.Bounds.Height * 0.80)
                return false;

            // Görev çubuğu üstünden açılan Desktops seçici / Başlat / Hızlı Ayarlar / Takvim pencerelerine ASLA dokunma
            if (r.Bottom >= mon.WorkArea.Bottom - 24 && r.Top > mon.WorkArea.Top + 120)
                return false;

            return true;
        }

        public static void SuppressWindowInstant(IntPtr h)
        {
            if (h == IntPtr.Zero) return;
            try
            {
                SetWindowPos(h, IntPtr.Zero, -32000, -32000, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW | SWP_ASYNCWINDOWPOS);
                ShowWindow(h, SW_HIDE);
            }
            catch { }
        }

        /// <summary>
        /// Eğer Windows'un kendi Snap Layouts Flyout / Snap Bar penceresi açılmışsa anında kapatır.
        /// </summary>
        public static void DismissActiveNativeFlyout()
        {
            try
            {
                EnumWindows((h, l) =>
                {
                    if (!IsWindowVisible(h)) return true;
                    if (IsNativeSnapPopup(h))
                    {
                        SuppressWindowInstant(h);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private static void RefreshExplorerSettingsCache()
        {
            try
            {
                Guid clsid = CLSID_ImmersiveShell;
                Guid iidSp = IID_IServiceProvider;
                if (CoCreateInstance(ref clsid, IntPtr.Zero, 4 /* CLSCTX_LOCAL_SERVER */ | 1 /* CLSCTX_INPROC_SERVER */, ref iidSp, out var sp) == 0 && sp != null)
                {
                    TryRefreshCacheViaService(sp, SID_SettingsCache1);
                    TryRefreshCacheViaService(sp, SID_SettingsCache2);
                    Marshal.ReleaseComObject(sp);
                }
            }
            catch { }
        }

        private static void TryRefreshCacheViaService(IComServiceProvider sp, Guid sid)
        {
            IntPtr pCache = IntPtr.Zero;
            try
            {
                Guid iid = IID_ISettingsCache;
                if (sp.QueryService(ref sid, ref iid, out pCache) == 0 && pCache != IntPtr.Zero)
                {
                    IntPtr vtable = Marshal.ReadIntPtr(pCache);
                    IntPtr fnPtr = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size); // slot 3: RefreshSetting(int settingId)
                    var refreshFn = Marshal.GetDelegateForFunctionPointer<RefreshSettingDelegate>(fnPtr);
                    // 11 (0xB) = EnableSnapAssistFlyout, 12 (0xC) = EnableSnapBar
                    refreshFn(pCache, 11);
                    refreshFn(pCache, 12);
                }
            }
            catch { }
            finally
            {
                if (pCache != IntPtr.Zero) Marshal.Release(pCache);
            }
        }

        private static void BroadcastSettingChange()
        {
            try
            {
                uint msgCache = RegisterWindowMessage("SettingsCacheChangeMessage");
                if (msgCache != 0)
                {
                    SendNotifyMessage(HWND_BROADCAST, msgCache, new IntPtr(11), IntPtr.Zero);
                    SendNotifyMessage(HWND_BROADCAST, msgCache, new IntPtr(12), IntPtr.Zero);
                }
                SendNotifyMessage(HWND_BROADCAST, (uint)WM_SETTINGCHANGE, IntPtr.Zero, "SettingsCacheChangeMessage");
                SendNotifyMessage(HWND_BROADCAST, (uint)WM_SETTINGCHANGE, IntPtr.Zero, "Explorer");
                SendNotifyMessage(HWND_BROADCAST, (uint)WM_SETTINGCHANGE, IntPtr.Zero, "TraySettings");
                SendNotifyMessage(HWND_BROADCAST, (uint)WM_SETTINGCHANGE, IntPtr.Zero, "Policy");
            }
            catch { }
        }
    }
}
