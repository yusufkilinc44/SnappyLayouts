using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace SnappyLayout.Services
{
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Snappy Layouts";
        private const string LegacyValueName = "Snappy Layout";

        public static void CleanupLegacy()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (k == null) return;
                bool hadLegacy = k.GetValue(LegacyValueName) is string s && !string.IsNullOrEmpty(s);
                if (hadLegacy)
                {
                    k.DeleteValue(LegacyValueName, false);
                    string exe = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exe))
                        k.SetValue(ValueName, $"\"{exe}\" --minimized");
                }
            }
            catch { }
        }

        public static bool IsEnabled()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return (k?.GetValue(ValueName) is string s && !string.IsNullOrEmpty(s))
                    || (k?.GetValue(LegacyValueName) is string s2 && !string.IsNullOrEmpty(s2));
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (k == null) return;
                k.DeleteValue(LegacyValueName, false);
                if (enabled)
                {
                    string exe = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exe))
                        k.SetValue(ValueName, $"\"{exe}\" --minimized");
                }
                else
                {
                    k.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { Logger.Log("Startup registry error: " + ex.Message); }
        }
    }
}
