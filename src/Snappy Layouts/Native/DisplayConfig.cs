using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SnappyLayout.Native
{
    /// <summary>
    /// Monitörlerin kullanıcı dostu isimlerini (ör. "DELL U2720Q") ve kalıcı cihaz yollarını
    /// QueryDisplayConfig API'si ile okur.
    /// </summary>
    internal static class DisplayConfig
    {
        public sealed class Info
        {
            public string GdiDeviceName;   // \\.\DISPLAY1
            public string FriendlyName;    // DELL U2720Q
            public string DevicePath;      // \\?\DISPLAY#DEL....
        }

        private const uint QDC_ONLY_ACTIVE_PATHS = 2;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL { public uint Numerator, Denominator; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx;
            public uint outputTechnology; public uint rotation; public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate; public uint scanLineOrdering;
            public int targetAvailable; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType; public uint id; public LUID adapterId;
            // union (48 bytes) – içeriği kullanmıyoruz
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public int type; public int size; public LUID adapterId; public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags; public uint outputTechnology;
            public ushort edidManufactureId; public ushort edidProductCodeId; public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
        [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] DISPLAYCONFIG_PATH_INFO[] paths, ref uint numModes, [Out] DISPLAYCONFIG_MODE_INFO[] modes, IntPtr topologyId);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME req);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME req);

        public static Dictionary<string, Info> Query()
        {
            var result = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint np, out uint nm) != 0) return result;
                var paths = new DISPLAYCONFIG_PATH_INFO[np];
                var modes = new DISPLAYCONFIG_MODE_INFO[nm];
                if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return result;

                for (int i = 0; i < np; i++)
                {
                    var p = paths[i];
                    var src = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    src.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    src.header.size = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                    src.header.adapterId = p.sourceInfo.adapterId;
                    src.header.id = p.sourceInfo.id;
                    if (DisplayConfigGetDeviceInfo(ref src) != 0) continue;

                    var tgt = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    tgt.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    tgt.header.size = Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                    tgt.header.adapterId = p.targetInfo.adapterId;
                    tgt.header.id = p.targetInfo.id;
                    DisplayConfigGetDeviceInfo(ref tgt);

                    if (!result.ContainsKey(src.viewGdiDeviceName))
                    {
                        result[src.viewGdiDeviceName] = new Info
                        {
                            GdiDeviceName = src.viewGdiDeviceName,
                            FriendlyName = string.IsNullOrWhiteSpace(tgt.monitorFriendlyDeviceName) ? null : tgt.monitorFriendlyDeviceName,
                            DevicePath = string.IsNullOrWhiteSpace(tgt.monitorDevicePath) ? null : tgt.monitorDevicePath
                        };
                    }
                }
            }
            catch { /* isim bilgisi opsiyonel */ }
            return result;
        }
    }
}
