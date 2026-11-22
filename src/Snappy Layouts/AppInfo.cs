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
        public const string Version = "1.12";
        public const string Author = "YUSUF KILINÇ";
        public const string AuthorDateTr = "Kasım 2026";
        public const string AuthorDateEn = "November 2026";

        public static string VersionText => "v" + Version;
        public static string Signature => $"{Author} — {AuthorDateTr}";

        /// <summary>Sürüm geçmişi (en yeni en üstte).</summary>
        public static readonly List<(string Version, string Date, string Tr, string En)> History = new List<(string, string, string, string)>
        {
            ("1.12", "22.11.2026",
             "Snap Layouts Flyout üst seçici düzeni 3 temiz satıra ayrıldı: 1. satırda fiziksel monitörler, 2. satırda yeşil 'Tam Fiziksel Monitör' + sanal monitör butonları, 3. satırda ise (3+ sanal monitör varsa) birleştirilmiş sanal ekran butonları (örn. Virtual 1 + Virtual 2) yer alır; tüm satırlar her zaman ekrana taşmadan sığacak şekilde otomatik boyutlandırılır ve konumlandırılır.",
             "Organized the Snap Layouts Flyout header into 3 clean dedicated rows: Row 1 for Physical Monitors, Row 2 for Full Physical Monitor + Virtual Monitor buttons, and Row 3 (when 3+ virtual monitors exist) for Combined Span buttons (e.g., Virtual 1 + Virtual 2), with automatic width clamping and repositioning so the flyout always fits cleanly within the screen without overflowing."),
            ("1.11", "22.11.2026",
             "Snap Layouts Flyout ve Top Snap Bar açıldığında, o anki hedef pencere halihazırda bir yerleşim bölgesine oturmuş durumdaysa ilgili düzen içindeki kutusu parlak vurgu çerçevesi, aktif arka plan tonu, kendi uygulama ikonu ve sağ üst durum noktasıyla (●) işaretlenir.",
             "When Snap Layouts Flyout or Top Snap Bar opens, if the current target window already occupies a snap zone, that exact zone box is highlighted with a bright accent border, active fill tint, the window's own app icon, and a top-right status dot (●) so you immediately see where the window currently sits."),
            ("1.10", "22.11.2026",
             "Kısayol atanmamış tüm işlemlere Türkçe Q (AltGr) ve Windows kısayollarıyla çakışmayan boş varsayılan tuş kombinasyonları atandı: 'Sanal monitörleri etkinleştir/devre dışı bırak' → Ctrl+Alt+V, Kayıtlı Sanal Monitör Şablonları → Ctrl+Alt+Shift+1..4 (yeni eklenenler otomatik boş kısayol alır), Çalışma Alanları → Ctrl+Alt+F1..F12. Mevcut ayarlar açılışta otomatik güncellenir.",
             "Assigned conflict-free default keyboard shortcuts (also safe for Turkish Q AltGr characters) to all previously unassigned actions: Toggle Virtual Monitors → Ctrl+Alt+V, saved Virtual Monitor Presets → Ctrl+Alt+Shift+1..4 (with automatic free-shortcut assignment for newly created presets), and Workspaces → Ctrl+Alt+F1..F12. Existing user configurations are automatically upgraded on startup."),
            ("1.9", "22.11.2026",
             "Yerleşim Düzenleri ve tüm ekranlarda önemli arayüz geliştirmeleri: (1) Sabit ilk düzen olan 'Sanal Monitör Tam Ekran' artık tıklanabilir/seçilebilir (sağda ayarları ve %100 önizlemesi görünür), yalnızca seçili olduğunda vurgulu çerçeve alır (çift seçili kutu görünümü giderildi), 'SABİT / SİLİNEMEZ' rozeti + kilit ikonu taşır ve sırası değiştirilemez. (2) Diğer tüm yerleşim düzenleri ▲/▼ butonlarıyla yeniden sıralanabilir. (3) Liste üstüne çoklu seçimli Ekran Tipi Filtre Çubuğu (Standart / Ultra-Geniş / Dikey) eklendi. (4) 'Full Physical Screen' tüm dillerde 'Full Physical Monitor' / 'Tam Fiziksel Monitör' olarak güncellendi. (5) Fiziksel Monitörler (koyu mavi/indigo donanım kartı) ile Sanal Monitörler (camgöbeği/turkuaz yuvarlak hap) renk, yazı tipi ve biçim olarak tüm ekranlarda belirgin şekilde ayrıştırıldı.",
             "Major UI & UX improvements across Snap Layouts and all screens: (1) The permanent first 'Virtual Monitor Fullscreen' card is now clickable/selectable (displaying its behavior settings, shortcut, and 100% preview on the right), only highlights its border when actually selected (eliminating the double-selected border effect), displays a 'PERMANENT / INDELETABLE' badge + lock icon, and remains pinned at #1. (2) Added ▲/▼ reorder buttons for all custom/standard layouts. (3) Added a multi-select Screen Type Filter Bar (Standard / Ultra-Wide / Portrait) above the layout list. (4) Renamed 'Full Physical Screen' to 'Full Physical Monitor' across all 8 languages while keeping Fullscreen labels unchanged. (5) Visually differentiated Physical Monitor buttons (bold indigo/blue hardware cards) from Virtual Monitor buttons (vibrant cyan/teal rounded pills) across Snap Layouts, Virtual Monitors, Settings & DPI, and the Snap Flyout."),
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
