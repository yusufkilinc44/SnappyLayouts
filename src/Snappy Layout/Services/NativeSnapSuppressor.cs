using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SnappyLayout.Native;
using static SnappyLayout.Native.NativeMethods;

namespace SnappyLayout.Services
{
    /// <summary>
    /// Windows 11'in yerleşik maksimize tuşu üstü Snap Layouts menüsünü (EnableSnapAssistFlyout / EnableSnapBar)
    /// Snappy Layout çalışırken devre dışı bırakır; Snappy Layout kapatıldığında veya duraklatıldığında eski haline döndürür.
    /// Ayrıca ImmersiveShell SettingsCache COM servisini tetikleyerek Explorer yeniden başlatılmadan anında devreye girmesini sağlar.
    /// </summary>
    public static class NativeSnapSuppressor
    {
        private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private static int? _origSnapAssistFlyout;
        private static int? _origSnapBar;
        private static bool _applied;

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
                    _applied = true;
                }

                key.SetValue("EnableSnapAssistFlyout", 0, RegistryValueKind.DWord);
                key.SetValue("EnableSnapBar", 0, RegistryValueKind.DWord);

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
            if (!_applied) return;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, writable: true);
                if (key == null) return;

                key.SetValue("EnableSnapAssistFlyout", _origSnapAssistFlyout ?? 1, RegistryValueKind.DWord);
                key.SetValue("EnableSnapBar", _origSnapBar ?? 1, RegistryValueKind.DWord);

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
        /// Eğer Windows'un kendi XamlExplorerHostIslandWindow Snap Flyout penceresi açılmışsa anında kapatır.
        /// </summary>
        public static void DismissActiveNativeFlyout()
        {
            try
            {
                EnumWindows((h, l) =>
                {
                    if (!IsWindowVisible(h)) return true;
                    string cls = NativeMethods.GetClassName(h);
                    if (IsNativeSnapWindowClass(cls))
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
