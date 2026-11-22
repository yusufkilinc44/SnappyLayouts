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
        public const string Version = "1.8";
        public const string Author = "YUSUF KILINÇ";
        public const string AuthorDateTr = "Kasım 2026";
        public const string AuthorDateEn = "November 2026";

        public static string VersionText => "v" + Version;
        public static string Signature => $"{Author} — {AuthorDateTr}";

        /// <summary>Sürüm geçmişi (en yeni en üstte).</summary>
        public static readonly List<(string Version, string Date, string Tr, string En)> History = new List<(string, string, string, string)>
        {
            ("1.8", "22.11.2026",
             "Snap Layouts Flyout üst çubuğu düzenlendi: yeşil 'Full Physical Screen' butonu sanal monitör satırının en soluna alındı, sanal monitör varsa araya dikey ayraç eklendi (yoksa ayraç gizlenir), birleşik tam ekran butonları 'Virtual 1 + Virtual 2' biçiminde adlandırıldı ve bu satırın altına tam genişlikte yatay ayraç çizgisi eklendi.",
             "Refined the Snap Layouts Flyout header bar: moved the green 'Full Physical Screen' button to the far left of the virtual monitors row with a vertical divider when virtual monitors exist, labeled span chips using full virtual monitor names (e.g., 'Virtual 1 + Virtual 2'), and added a full-width horizontal divider below the selector row."),
            ("1.7", "22.11.2026",
             "Farklı DPI ölçeklerine sahip monitörler (%125 ↔ %175) arasında açıldığında Snap Flyout ve Top Snap Bar pencerelerinin sağ kenardan ekrandan taşması / kırpılması sorunu iki aşamalı DPI yerleşimi ve gerçek GetWindowRect piksel kırpma ile giderildi.",
             "Fixed Snap Flyout and Top Snap Bar overflowing off the right edge of the screen on mixed-DPI multi-monitor setups (e.g., 125% ↔ 175%) by using two-pass WM_DPICHANGED-synchronized positioning with exact GetWindowRect pixel clamping."),
            ("1.6", "22.11.2026",
             "Snap Layout kutularındaki uygulama ikonları artık yalnızca gerçekten tam o bölgenin boyut ve konumunda bulunan (üstü başka pencereyle kapanmamış) uygulamalar için gösterilir (4 kenar toleransı + IoU >= 0.85). Tekrarlayan öneri satırı kaldırılarak her düzenin tek kez ve yalnızca gerçek canlı pencere ikonlarıyla görünmesi sağlandı.",
             "Refined live app icons inside Snap Layout zones to appear strictly when a visible, unoccluded window actually occupies that exact zone size and position (4-edge tolerance + IoU >= 0.85), and removed the duplicate suggestion row so each layout appears once with accurate live occupancy icons."),
            ("1.5", "22.11.2026",
             "Windows 11 orijinal Snap Layouts görünümünde olduğu gibi Snap Layout kutularının içinde açık uygulamaların ikonları eklendi: üst satırda son kullanılan diğer açık uygulamalarla çoklu pencere öneri şablonları (tıklandığında tüm önerilen pencereleri ilgili bölgelere tek tıkla yerleştirir) ve alt satırlar ile Top Snap Bar kutularında o bölgelerde halihazırda bulunan uygulamaların canlı ikonları gösterilir (WindowIconProvider — önbellekli ve sıfır bloklama).",
             "Added Windows 11-style live application icons inside Snap Layout zones: a top row of smart multi-window layout suggestions displaying icons of recently used open apps (clicking arranges the active window and suggested companion apps together), plus live occupied-zone app icons in regular Snap Flyout and Top Snap Bar tiles via a fast, cached, zero-hang WindowIconProvider."),
            ("1.4", "22.11.2026",
             "Yeni bir Snappy Layouts örneği başlatıldığında (veya sürüm güncellendiğinde) kapanan eski örneğin Windows'un yerleşik Snap Layouts ayarlarını yanlışlıkla geri açması (single-instance race condition) giderildi ve uyku/ekran değişikliği sonrasında native Snap bastırmanın otomatik doğrulanması (EnsureSuppressed) eklendi.",
             "Fixed a single-instance handoff race condition where an exiting older instance could accidentally re-enable Windows's native Snap Layouts right after the new instance suppressed it, and added automatic EnsureSuppressed verification after display changes or waking from sleep."),
            ("1.3", "22.11.2026",
             "Büyük PDF açan Adobe Acrobat gibi CPU/UI yükü altındaki veya yavaş yanıt veren uygulamalarda maksimize tuşu üstü Snap Flyout menüsünün açılmaması / gecikmesi sorunu giderildi. MaximizeHoverWatcher artık başlık çubuğu dışında gereksiz cross-process WM_NCHITTEST göndermez, hedef uygulama meşgul veya özel başlık çubuklu (HTCLIENT) olsa bile 0 ms geometrik tespit ile anında açılır ve UI iş parçacığı asla hedef uygulamayı beklemez.",
             "Fixed Maximize Hover Snap Flyout failing to open or lagging on busy/heavy apps (e.g., Adobe Acrobat loading large PDFs). MaximizeHoverWatcher now pre-filters to the caption button band with zero IPC elsewhere, falls back instantly to geometric maximize-button bounds when the target app is busy/hung or returns HTCLIENT for custom titlebars, and keeps SnapFlyoutWindow/WindowEventService free of blocking UI-thread hit-tests."),
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
