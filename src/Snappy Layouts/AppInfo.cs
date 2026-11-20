using System.Collections.Generic;

namespace SnappyLayout
{
    /// <summary>
    /// Uygulama kimliği ve sürüm bilgisi.
    /// Sürüm numarası Snappy Layouts.csproj içindeki &lt;InformationalVersion&gt; ile birlikte güncellenmelidir.
    /// </summary>
    public static class AppInfo
    {
        public const string Name = "Snappy Layouts";
        public const string Version = "1.2";
        public const string Author = "YUSUF KILINÇ";
        public const string AuthorDateTr = "Kasım 2026";
        public const string AuthorDateEn = "November 2026";

        public static string VersionText => "v" + Version;
        public static string Signature => $"{Author} — {AuthorDateTr}";

        /// <summary>Sürüm geçmişi (en yeni en üstte).</summary>
        public static readonly List<(string Version, string Date, string Tr, string En)> History = new List<(string, string, string, string)>
        {
            ("1.2", "20.11.2026",
             "Harici bir monitör bağlantısı kesildiğinde (veya uykudan dönüldüğünde) çıkarılan monitöre ait eski sanal monitör sınır çizgilerinin ve önbellekteki monitör bilgisinin kalan ekranda görünmesi hatası giderildi. Sınır katmanları gizlenirken bellekten temizlenir ve bağlı monitör listesi anlık/otomatik olarak tazelenir.",
             "Fixed a bug where unplugging an external monitor (or resuming from sleep) could leave stale virtual monitor border overlays from the disconnected monitor visible on the remaining display. Border overlays now close cleanly when hidden, monitor topology refreshes with a follow-up settle pass on display/power changes, and the monitor cache auto-refreshes every 1.5s."),
            ("1.1", "19.11.2026",
             "Kurulum sihirbazı (Setup.exe) artık standart C:\\Program Files\\Snappy Layouts dizinine kurulur ve antivirüs (Bitdefender vb.) yanlış pozitiflerini önlemek için tek dosya sıkıştırmalı paketleyici yerine şeffaf çoklu-dosya self-contained yayın biçimine geçildi.",
             "Updated Windows Installer (Setup.exe) to install into standard C:\\Program Files\\Snappy Layouts and switched from single-file self-extracting bundles to transparent multi-file self-contained binaries to prevent antivirus false positives."),
            ("1.0", "19.11.2026",
             "Snappy Layouts v1.0 ilk kararlı sürüm (Production Release): Windows 11 tarzı maksimize üstü Snap Flyout ve üst sürükleme Snap Bar, kenar/tepe sanal monitör yaslama, yan yana sanal monitörler ve çoklu sanal monitör birleşik tam ekran (1+2, 2+3, Shift+Tık), doğrudan sanal monitör maksimize, özelleştirilebilir ızgara düzen editörü, Çalışma Alanları (Workspaces & Apps), 8 dil ve vektörel bayrak desteği, Per-Monitor V2 DPI uyumu, daraltılabilir renkli sol menü, Windows kurulum sihirbazı (Setup.exe) ve taşınabilir (Portable) dağıtım.",
             "Snappy Layouts v1.0 initial production release: Windows 11-styled Maximize Hover Snap Flyout and Top-of-Screen Drag Snap Bar, virtual-monitor-aware edge and top-edge snapping, side-by-side Virtual Monitors with multi-virtual-monitor spanning (1+2, 2+3, Shift+Click), zero-flash direct Virtual Maximize, customizable per-monitor grid layout editor, Workspaces & Apps capture/restore, 8-language localization with vector flags, Per-Monitor V2 DPI support, collapsible sidebar, and Windows Installer (Setup.exe) + Portable distribution.")
        };
    }
}
