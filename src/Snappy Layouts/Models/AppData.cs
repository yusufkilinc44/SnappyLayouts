using System;
using System.Collections.Generic;
using System.Linq;

namespace SnappyLayout.Models
{
    public enum ThemeChoice { System, Light, Dark }
    public enum LanguageChoice { System, English, Turkish, Spanish, Portuguese, Chinese, French, German, Russian }
    public enum WorkspaceOpenMode { Ask, CurrentDesktop, NewDesktop }

    /// <summary>Kısayol tuşu (RegisterHotKey modifier + sanal tuş kodu).</summary>
    public class Hotkey
    {
        public uint Modifiers { get; set; }
        public uint Key { get; set; }

        public Hotkey() { }
        public Hotkey(uint mods, uint key) { Modifiers = mods; Key = key; }

        public bool IsEmpty => Key == 0;
        public Hotkey Clone() => new Hotkey(Modifiers, Key);
        public bool SameAs(Hotkey o) => o != null && o.Modifiers == Modifiers && o.Key == Key;

        public override string ToString()
        {
            if (IsEmpty) return "";
            var parts = new List<string>();
            if ((Modifiers & Native.NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((Modifiers & Native.NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
            if ((Modifiers & Native.NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((Modifiers & Native.NativeMethods.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(KeyName(Key));
            return string.Join(" + ", parts);
        }

        public static string KeyName(uint vk)
        {
            try
            {
                var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)vk);
                switch (key)
                {
                    case System.Windows.Input.Key.Left: return "←";
                    case System.Windows.Input.Key.Right: return "→";
                    case System.Windows.Input.Key.Up: return "↑";
                    case System.Windows.Input.Key.Down: return "↓";
                }
                var s = key.ToString();
                if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s.Substring(1);
                if (s.StartsWith("NumPad")) return "Num" + s.Substring(6);
                if (s == "Oem3") return "\"";
                return s;
            }
            catch { return "VK" + vk; }
        }
    }

    /// <summary>Kaydedilmiş sanal monitör düzeni (ör. 2 Sanal Monitör: 50% | 50%).</summary>
    public class VirtualPreset
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public List<double> Weights { get; set; } = new List<double>();
        public Hotkey Hotkey { get; set; }
        public string Description => $"{Weights.Count} Sanal Monitör: " + string.Join(" | ", Percentages(Weights).Select(p => p.ToString("0.#") + "%"));

        public static IEnumerable<double> Percentages(IList<double> w)
        {
            double t = w.Sum();
            if (t <= 0) yield break;
            foreach (var x in w) yield return x / t * 100.0;
        }
    }

    /// <summary>Çalışma alanında kayıtlı bir uygulama/pencere.</summary>
    public class SavedApp
    {
        public string ExePath { get; set; }
        public string Arguments { get; set; }
        public string AppUserModelId { get; set; }
        public string Title { get; set; }
        public string ClassName { get; set; }
        public string Url { get; set; }          // tarayıcılar için
        public string FolderPath { get; set; }   // Dosya Gezgini pencereleri için
        public bool IsBrowser { get; set; }
        public bool IsMaximized { get; set; }    // gerçek fiziksel tam ekran
        // Kapsam alanına göre göreli konum (0..1)
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }

        public string DisplayName
        {
            get
            {
                string exe = string.IsNullOrEmpty(ExePath) ? (AppUserModelId ?? "?") : System.IO.Path.GetFileNameWithoutExtension(ExePath);
                if (!string.IsNullOrEmpty(Url)) return exe + " — " + Url;
                if (!string.IsNullOrEmpty(FolderPath)) return exe + " — " + FolderPath;
                return exe + (string.IsNullOrEmpty(Title) ? "" : " — " + Title);
            }
        }
    }

    /// <summary>Çalışma alanı: sanal monitör düzeni + (opsiyonel) içindeki uygulamalar ve konumları.</summary>
    public class Workspace
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string MonitorKey { get; set; }
        public string MonitorName { get; set; }
        public int MonitorIndex { get; set; }
        public List<double> Weights { get; set; } = new List<double>();
        /// <summary>-1 = tüm fiziksel monitör, 0..n = sanal monitör indeksi</summary>
        public int ScopeSegment { get; set; } = -1;
        public bool IncludeApps { get; set; } = true;
        public List<SavedApp> Apps { get; set; } = new List<SavedApp>();
        public Hotkey Hotkey { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
    }

    public class AppSettings
    {
        public LanguageChoice Language { get; set; } = LanguageChoice.English;
        public ThemeChoice Theme { get; set; } = ThemeChoice.System;
        public bool StartWithWindows { get; set; } = false;
        public bool FlyoutEnabled { get; set; } = true;
        public int HoverDelayMs { get; set; } = 220;
        public int ZoneGap { get; set; } = 0;
        public bool VirtualMonitorsEnabled { get; set; } = true;
        public bool VirtualMaximize { get; set; } = true;
        public bool ShiftBypassesVirtualMaximize { get; set; } = true;
        public bool ShowBordersWhileDragging { get; set; } = true;
        public bool AlwaysShowBorders { get; set; } = false;
        public int BorderThickness { get; set; } = 6;
        public string BorderColor { get; set; } = "Accent";
        public int BorderFlashSeconds { get; set; } = 3;
        public WorkspaceOpenMode WorkspaceOpenMode { get; set; } = WorkspaceOpenMode.Ask;
        public bool FirstRunDone { get; set; } = false;
    }

    public static class HotkeyActions
    {
        public const string ShowFlyout = "ShowFlyout";
        public const string ShowBorders = "ShowBorders";
        public const string VirtualMaximize = "VirtualMaximize";
        public const string PhysicalMaximize = "PhysicalMaximize";
        public const string NextSegment = "NextSegment";
        public const string PrevSegment = "PrevSegment";
        public const string RestoreWindow = "RestoreWindow";
        public const string ToggleVirtual = "ToggleVirtual";
        public const string OpenSettings = "OpenSettings";

        public static readonly string[] All =
            { ShowFlyout, ShowBorders, VirtualMaximize, PhysicalMaximize, NextSegment, PrevSegment, RestoreWindow, ToggleVirtual, OpenSettings };

        public static Dictionary<string, Hotkey> Defaults()
        {
            const uint CA = Native.NativeMethods.MOD_CONTROL | Native.NativeMethods.MOD_ALT;
            return new Dictionary<string, Hotkey>
            {
                [ShowFlyout] = new Hotkey(CA, 0x5A),               // Ctrl+Alt+Z
                [ShowBorders] = new Hotkey(CA, 0x42),              // Ctrl+Alt+B
                [VirtualMaximize] = new Hotkey(CA, 0x26),          // Ctrl+Alt+Up
                [PhysicalMaximize] = new Hotkey(CA | Native.NativeMethods.MOD_SHIFT, 0x26), // Ctrl+Alt+Shift+Up
                [NextSegment] = new Hotkey(CA, 0x27),              // Ctrl+Alt+Right
                [PrevSegment] = new Hotkey(CA, 0x25),              // Ctrl+Alt+Left
                [RestoreWindow] = new Hotkey(CA, 0x28),            // Ctrl+Alt+Down
                [ToggleVirtual] = new Hotkey(CA, 0x56),            // Ctrl+Alt+V
                [OpenSettings] = new Hotkey(CA, 0x4B),             // Ctrl+Alt+K
            };
        }
    }

    /// <summary>Kalıcı olarak saklanan tüm veriler (settings.json).</summary>
    public class AppData
    {
        public int SchemaVersion { get; set; } = 3;
        public AppSettings Settings { get; set; } = new AppSettings();

        /// <summary>Geriye dönük uyumluluk / varsayılan şablon listesi</summary>
        public List<SnapLayout> Layouts { get; set; }

        /// <summary>
        /// Her fiziksel monitör veya her sanal monitör için bağımsız Pencere Yerleşim Düzenleri!
        /// Anahtar: "{MonitorKey}::phys" (sanal monitör yoksa) veya "{MonitorKey}::vm:{SegmentIndex}" (sanal monitör varsa).
        /// </summary>
        public Dictionary<string, List<SnapLayout>> AreaLayouts { get; set; } = new Dictionary<string, List<SnapLayout>>();

        /// <summary>Monitör anahtarı → sanal monitör genişlik oranları</summary>
        public Dictionary<string, List<double>> VirtualMonitors { get; set; } = new Dictionary<string, List<double>>();

        /// <summary>Fiziksel monitör anahtarı → kullanıcı tanımlı özel monitör adı</summary>
        public Dictionary<string, string> MonitorCustomNames { get; set; } = new Dictionary<string, string>();

        public List<VirtualPreset> VirtualPresets { get; set; } = new List<VirtualPreset>
        {
            new VirtualPreset
            {
                Name = "2 Sanal Monitör (50% | 50%)",
                Weights = new List<double> { 1, 1 },
                Hotkey = new Hotkey(Native.NativeMethods.MOD_CONTROL | Native.NativeMethods.MOD_ALT | Native.NativeMethods.MOD_SHIFT, 0x31) // Ctrl+Alt+Shift+1
            },
            new VirtualPreset
            {
                Name = "3 Sanal Monitör (33% | 33% | 33%)",
                Weights = new List<double> { 1, 1, 1 },
                Hotkey = new Hotkey(Native.NativeMethods.MOD_CONTROL | Native.NativeMethods.MOD_ALT | Native.NativeMethods.MOD_SHIFT, 0x32) // Ctrl+Alt+Shift+2
            },
            new VirtualPreset
            {
                Name = "3 Sanal Monitör — Orta Geniş (25% | 50% | 25%)",
                Weights = new List<double> { 1, 2, 1 },
                Hotkey = new Hotkey(Native.NativeMethods.MOD_CONTROL | Native.NativeMethods.MOD_ALT | Native.NativeMethods.MOD_SHIFT, 0x33) // Ctrl+Alt+Shift+3
            },
            new VirtualPreset
            {
                Name = "2 Sanal Monitör — Asimetrik (40% | 60%)",
                Weights = new List<double> { 2, 3 },
                Hotkey = new Hotkey(Native.NativeMethods.MOD_CONTROL | Native.NativeMethods.MOD_ALT | Native.NativeMethods.MOD_SHIFT, 0x34) // Ctrl+Alt+Shift+4
            },
        };

        public List<Workspace> Workspaces { get; set; } = new List<Workspace>();
        public Dictionary<string, Hotkey> Hotkeys { get; set; }

        public bool IsHotkeyInUse(Hotkey candidate)
        {
            if (candidate == null || candidate.IsEmpty) return false;
            if (Hotkeys != null && Hotkeys.Values.Any(h => h != null && !h.IsEmpty && h.SameAs(candidate))) return true;
            if (VirtualPresets != null && VirtualPresets.Any(p => p.Hotkey != null && !p.Hotkey.IsEmpty && p.Hotkey.SameAs(candidate))) return true;
            if (Workspaces != null && Workspaces.Any(w => w.Hotkey != null && !w.Hotkey.IsEmpty && w.Hotkey.SameAs(candidate))) return true;
            return false;
        }

        /// <summary>
        /// Çakışmayan ve Türkçe Q AltGr karakterleriyle çakışmayan ilk boş kısayol kombinasyonunu bulur.
        /// Sanal monitör şablonları: Ctrl+Alt+Shift+1..9, 0 → ardından Ctrl+Alt+F1..F12.
        /// Çalışma alanları: Ctrl+Alt+F1..F12 → ardından Ctrl+Alt+Shift+F1..F12.
        /// </summary>
        public Hotkey FindNextAvailableHotkey(bool forWorkspace)
        {
            const uint CA = Native.NativeMethods.MOD_CONTROL | Native.NativeMethods.MOD_ALT;
            const uint CAS = CA | Native.NativeMethods.MOD_SHIFT;

            var candidates = new List<Hotkey>();
            if (!forWorkspace)
            {
                for (uint k = 0x31; k <= 0x39; k++) candidates.Add(new Hotkey(CAS, k)); // Ctrl+Alt+Shift+1..9
                candidates.Add(new Hotkey(CAS, 0x30));                                 // Ctrl+Alt+Shift+0
                for (uint k = 0x70; k <= 0x7B; k++) candidates.Add(new Hotkey(CA, k)); // Ctrl+Alt+F1..F12
                for (uint k = 0x70; k <= 0x7B; k++) candidates.Add(new Hotkey(CAS, k));// Ctrl+Alt+Shift+F1..F12
            }
            else
            {
                for (uint k = 0x70; k <= 0x7B; k++) candidates.Add(new Hotkey(CA, k)); // Ctrl+Alt+F1..F12
                for (uint k = 0x70; k <= 0x7B; k++) candidates.Add(new Hotkey(CAS, k));// Ctrl+Alt+Shift+F1..F12
                for (uint k = 0x31; k <= 0x39; k++) candidates.Add(new Hotkey(CAS, k));// Ctrl+Alt+Shift+1..9
            }

            foreach (var c in candidates)
            {
                if (!IsHotkeyInUse(c)) return c;
            }
            return new Hotkey();
        }

        /// <summary>Kısayolu boş olan tüm eylemlere, sanal monitör şablonlarına ve çalışma alanlarına boş kısayol atar.</summary>
        public void AssignMissingHotkeys()
        {
            if (Hotkeys == null) Hotkeys = HotkeyActions.Defaults();
            var defs = HotkeyActions.Defaults();
            foreach (var kv in defs)
            {
                if (!Hotkeys.TryGetValue(kv.Key, out var cur) || cur == null || cur.IsEmpty)
                {
                    if (!IsHotkeyInUse(kv.Value))
                        Hotkeys[kv.Key] = kv.Value.Clone();
                    else
                        Hotkeys[kv.Key] = FindNextAvailableHotkey(forWorkspace: false);
                }
            }

            if (VirtualPresets != null)
            {
                foreach (var vp in VirtualPresets)
                {
                    if (vp.Hotkey == null || vp.Hotkey.IsEmpty)
                        vp.Hotkey = FindNextAvailableHotkey(forWorkspace: false);
                }
            }

            if (Workspaces != null)
            {
                foreach (var ws in Workspaces)
                {
                    if (ws.Hotkey == null || ws.Hotkey.IsEmpty)
                        ws.Hotkey = FindNextAvailableHotkey(forWorkspace: true);
                }
            }
        }

        public void EnsureDefaults()
        {
            if (Settings == null) Settings = new AppSettings();
            if (Layouts == null) Layouts = DefaultLayouts.Create();
            foreach (var l in Layouts)
            {
                l.Normalize();
                // Önceki sürümde sanal monitör şablonu sanılarak "2 monitör" olarak adlandırılan pencere düzenini düzelt
                if (!string.IsNullOrEmpty(l.DefaultKey) && (l.Name == "2 monitör" || DefaultLayouts.IsStandardCodeName(l.Name)))
                    l.Name = DefaultLayouts.GetLocalizedDefaultName(l.DefaultKey);
            }

            if (AreaLayouts == null) AreaLayouts = new Dictionary<string, List<SnapLayout>>();
            foreach (var list in AreaLayouts.Values)
            {
                if (list == null) continue;
                foreach (var l in list)
                {
                    l.Normalize();
                    if (!string.IsNullOrEmpty(l.DefaultKey) && (l.Name == "2 monitör" || DefaultLayouts.IsStandardCodeName(l.Name)))
                        l.Name = DefaultLayouts.GetLocalizedDefaultName(l.DefaultKey);
                }
            }

            if (VirtualMonitors == null) VirtualMonitors = new Dictionary<string, List<double>>();
            if (MonitorCustomNames == null) MonitorCustomNames = new Dictionary<string, string>();
            if (VirtualPresets == null) VirtualPresets = new List<VirtualPreset>();
            foreach (var vp in VirtualPresets)
            {
                if (vp.Name == "50 | 50") vp.Name = "2 Sanal Monitör (50% | 50%)";
                else if (vp.Name == "33 | 33 | 33") vp.Name = "3 Sanal Monitör (33% | 33% | 33%)";
                else if (vp.Name == "25 | 50 | 25") vp.Name = "3 Sanal Monitör — Orta Geniş (25% | 50% | 25%)";
                else if (vp.Name == "40 | 60") vp.Name = "2 Sanal Monitör — Asimetrik (40% | 60%)";
            }

            if (Workspaces == null) Workspaces = new List<Workspace>();
            if (Hotkeys == null) Hotkeys = HotkeyActions.Defaults();
            foreach (var kv in HotkeyActions.Defaults())
                if (!Hotkeys.ContainsKey(kv.Key)) Hotkeys[kv.Key] = kv.Value;

            // v1.10 yükseltmesi: henüz kısayol atanmamış eylem, sanal monitör şablonu ve çalışma alanlarına boş kısayollar ata
            if (SchemaVersion < 3)
            {
                AssignMissingHotkeys();
                SchemaVersion = 3;
            }
        }
    }
}
