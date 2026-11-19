using System.Collections.Generic;

namespace SnappyLayout
{
    /// <summary>
    /// Uygulama kimliği ve sürüm bilgisi.
    /// SÜRÜM POLİTİKASI: Her değişiklikte sürüm 0.1 artırılır (v0.1 → v0.2 → ...).
    /// Canlıya alındığında v1.0 yapılır. Sürüm numarası Snappy Layouts.csproj içindeki
    /// &lt;InformationalVersion&gt; ile birlikte burada da güncellenmelidir.
    /// </summary>
    public static class AppInfo
    {
        public const string Name = "Snappy Layouts";
        public const string Version = "0.25";
        public const string Author = "YUSUF KILINÇ";
        public const string AuthorDateTr = "Kasım 2026";
        public const string AuthorDateEn = "November 2026";

        public static string VersionText => "v" + Version;
        public static string Signature => $"{Author} — {AuthorDateTr}";

        /// <summary>Sürüm geçmişi (en yeni en üstte).</summary>
        public static readonly List<(string Version, string Date, string Tr, string En)> History = new List<(string, string, string, string)>
        {
            ("0.25", "19.11.2026",
             "Uygulama adı tüm kaynak kod, proje dosyaları, arayüz ve belgelerde 'Snappy Layouts' olarak güncellendi (eski %AppData%\\Snappy Layout ayarları otomatik taşınır). Uygulama duraklatıldığında veya tamamen kapatıldığında Windows'un orijinal Snap Layouts ve Aero Snap özellikleri eksiksiz geri yüklenir. Task View / Sanal Masaüstü seçici (Desktops) penceresinin fare hareketiyle kaybolma hatası giderildi ve Çalışma Alanlarının (Workspaces & Apps) düzenler değişmiş olsa bile pencereleri tam kaydedilen konumlara yerleştirmesi sağlandı.",
             "Renamed the application to 'Snappy Layouts' across all code, project/solution files, UI, and documentation (with automatic migration of settings from legacy %AppData%\\Snappy Layout). Fixed pausing and exiting so Windows's native Snap Layouts and Aero Snap are immediately and reliably restored. Fixed Windows 11 Task View / Virtual Desktops switcher disappearing on mouse movement, and ensured Workspaces always restore windows to their exact saved coordinates even if Snap Layouts changed."),
            ("0.24", "19.11.2026",
             "Pencere en üst kenara sürüklendiğinde Top Snap Bar'daki ilk kutu (Sanal Monitör Tam Ekran) doğrudan mavi seçili olarak vurgulanır; Fiziksel Tam Ekran kutusu ise üzerine gelinmediği sürece nötr görünerek kafa karışıklığı önlenir.",
             "When dragging a window to the very top edge, the first Top Snap Bar tile (Virtual Monitor Fullscreen) now highlights in accent blue as selected, while the Full Physical Screen tile stays neutral until actually hovered to avoid visual confusion."),
            ("0.23", "19.11.2026",
             "Windows'un yerleşik kenara/tepeye sürükleme (Aero Snap) önizlemesi ve yaslaması SPI_SETWINARRANGING ile devre dışı bırakıldı (kapanışta/duraklatmada otomatik geri yüklenir). En sol/sağ kenara sürükleyince uç sanal monitörün %50 yarısı, en üst kenara sürükleyince imlecin bulunduğu sanal monitörün %100 tam ekranı Snappy Layouts önizlemesiyle gösterilip bırakıldığında yerleştirilir. Ayrıca daraltılmış (collapse) sol menüdeki ikonların sağ kenarının kırpılması giderildi.",
             "Suppressed Windows's native edge/top-drag Aero Snap via SPI_SETWINARRANGING (auto-restored on exit/pause) and replaced it with Snappy Layouts's virtual-monitor-aware edge & top snap: left/right screen edges snap to 50% of the edge virtual monitor, and the very top edge previews/snaps to 100% fullscreen of the active virtual monitor. Also fixed right-edge clipping of sidebar icons in collapsed mode."),
            ("0.22", "19.11.2026",
             "İngilizce dil seçimi bayrağı Birleşik Krallık (UK / Union Jack) bayrağı olarak güncellendi.",
             "Updated the English language selector flag icon to the United Kingdom (Union Jack) flag."),
            ("0.21", "19.11.2026",
             "Sol menü genişletme/daraltma (expand/collapse) tam sabit koordinatlı hale getirildi: geçişte yalnızca metinler ve açılır listeler gizlenip açılır, tüm ikonların (X, Y) konumları piksel düzeyinde sabit kalır. Daraltılmış modda aktif dil bayrağı, tema ikonu, sınır çizgileri ve duraklatma butonları tek tıkla açılır menüyle tam erişilebilir yapıldı.",
             "Unified the left sidebar expand/collapse layout so icon (X, Y) coordinates stay pixel-locked while only labels and dropdowns show/hide. Added clickable active language flag and theme icon menus in collapsed mode so every expanded sidebar control remains directly accessible in collapsed mode."),
            ("0.20", "19.11.2026",
             "Tüm dil bayrakları (ABD/İngilizce, Türkiye ay-yıldız, İspanya arması, Portekiz arması, Çin 5 yıldızı, Fransa, Almanya, Rusya) tam piksel koordinatlı vektörel Canvas ile yeniden çizildi.",
             "Redrew all 8 country flags with exact vector Canvas coordinates: US flag for English, authentic crescent & 5-pointed star for Turkey, Coat of Arms for Spain, armillary sphere & shield for Portugal, 5-star arc for China, and tricolors for France, Germany, and Russia."),
            ("0.19", "19.11.2026",
             "Sol menü genişletme/daraltma (expand/collapse) geçişlerinde tüm ikon boyutları (32×32) ve menü satır yükseklikleri (46px) sabitlenerek geçişte dikey kayma olmaması sağlandı.",
             "Locked sidebar navigation icon sizes (32×32) and row heights (46px) across expanded and collapsed modes so icons and menu rows never shift vertically when toggling."),
            ("0.18", "19.11.2026",
             "Sol menü başlığındaki sürüm rozetinin (v0.18) daraltma düğmesi (☰) yanında kırpılması giderildi; rozet durum satırına (Active yanına) alınarak her zaman tam ve okunaklı görünmesi sağlandı.",
             "Fixed the version badge in the sidebar header getting clipped next to the collapse button (☰) by placing it cleanly alongside the Active status line."),
            ("0.17", "19.11.2026",
             "8 dil desteği (İngilizce varsayılan, Türkçe, İspanyolca, Portekizce, Çince, Fransızca, Almanca, Rusça) ve vektörel ülke bayrakları eklendi. Monitör en/boy oranları standart 16:9, 16:10, 21:9, 32:9, 4:3 biçimine dönüştürüldü. Önizleme kutuları çok daha şeffaf/yumuşak yapıldı ve Notepad gibi pencerelerin altında kalma / kırpılma sorunu giderildi. Sol menü pencere küçüldüğünde renkli ikonlarla daraltılabilir (collapsible) hale getirildi, uygulama genel olarak renklendirildi ve yazar/yıl bilgisi yalnızca Yardım & Hakkında sekmesine alındı.",
             "Added 8-language localization (English default, Turkish, Spanish, Portuguese, Chinese, French, German, Russian) with crisp vector country flags. Formatted monitor aspect ratios as standard ratios (16:9, 16:10, 21:9, 32:9, 4:3). Made Snap preview overlays softer/more transparent and fixed overlay clipping/Z-order over Notepad. Added responsive collapsible left sidebar with vibrant colored icons, enriched app-wide accent colors, and kept author/year info exclusively in the Help & About section."),
            ("0.16", "19.11.2026",
             "Yan yana birden fazla sanal monitörü kapsayan Birleşik Tam Ekran (Span: ◫ 1+2, ◫ 2+3 ve Shift+Tık) özelliği hem Snap Flyout hem de üst sürükleme Snap Bar'a eklendi. Ayrıca WH_MOUSE_LL tabanlı MaximizeClickInterceptor ile sanal monitör aktifken Maksimize tuşuna basıldığında önce fiziksel monitörde büyüyüp sonra küçülme efekti kaldırılarak pencerenin doğrudan sanal monitörde tam ekran olması sağlandı.",
             "Added Multi-Virtual-Monitor Spanning (one-click ◫ 1+2 / ◫ 2+3 chips, Shift+Click range spanning in Snap Flyout, and adjacent span drop tiles in Top Snap Bar). Added WH_MOUSE_LL MaximizeClickInterceptor so clicking a window's Maximize button goes directly to Virtual Monitor Fullscreen without flashing to full physical screen first."),
            ("0.15", "19.11.2026",
             "Sanal ve fiziksel monitör önizleme kutularının (mavi/yeşil SnapZonePreviewOverlay) dolgu ve kenarlık şeffaflığı artırılarak daha yumuşak ve şeffaf bir görünüm sağlandı.",
             "Increased transparency of the blue and green virtual/physical monitor preview overlays (SnapZonePreviewOverlay) for a lighter, more subtle glass appearance."),
            ("0.14", "19.11.2026",
             "Maksimize üstü Snap Flyout penceresinde hover sırasında alt durum metni değiştiğinde pencere genişliğinin/konumunun oynaması ve butonların yanıp sönmesi (flicker) hatası sabit genişlik ve Z-sırası (önizleme katmanını Flyout'un hemen altında tutma) ile tamamen giderildi. Flyout'taki fiziksel tam ekran butonundan '(Silinemez) / (Indeletable)' ifadesi kaldırıldı (yalnızca ana uygulama içinde gösterilir).",
             "Eliminated hover flickering and button/text jumping in SnapFlyoutWindow by locking the outer flyout width during rebuilds and placing SnapZonePreviewOverlay strictly behind the flyout/bar in Z-order. Removed '(Indeletable)' from the Snap Flyout button (kept only inside the main management app)."),
            ("0.13", "19.11.2026",
             "Fiziksel monitörlere özel isim verebilme özelliği eklendi (Yerleşim Düzenleri, Sanal Monitörler ve Ayarlar & DPI sekmelerinden düzenlenebilir; verilen isimler Flyout dahil tüm uygulamada kalıcı olarak gösterilir).",
             "Added custom naming for physical monitors (editable from Snap Layouts, Virtual Monitors, and Settings & DPI tabs; custom names persist in settings.json and appear everywhere including Snap Flyout)."),
            ("0.12", "19.11.2026",
             "Farklı DPI ölçeklerine sahip fiziksel monitörler (örn. %125 ve %175) arasında önizleme çerçevesi (SnapZonePreviewOverlay) geçişi yapılırken WM_DPICHANGED yeniden ölçeklemesi nedeniyle yeşil/mavi önizleme kutusunun diğer monitöre taşması veya küçülmesi hatası iki aşamalı DPI yerleşimi ile giderildi.",
             "Fixed SnapZonePreviewOverlay overflowing or shrinking when switching live previews between physical monitors with different DPI scales (e.g. 125% and 175%) by using two-pass DPI-synchronized positioning."),
            ("0.11", "19.11.2026",
             "Pencereyi fareyle ekranın en üstüne sürükleyince Windows'un kendi Snap Bar'ı bastırılarak Snappy Layouts'un üst sürükleme yerleşim çubuğunun (TopSnapBarWindow) açılması sağlandı. Hem maksimize üstü Flyout hem de üst sürükleme Snap Bar ekranları Windows 11 orijinal Snap Layouts görünümüne (koyu/açık nötr yüzey, çerçevesiz yuvarlatılmış bölge kutuları, Windows vurgu mavisi) birebir benzetildi.",
             "Replaced Windows 11's native top-of-screen drag Snap Bar with Snappy Layouts's TopSnapBarWindow when dragging any window to the top edge. Redesigned both the Maximize Snap Flyout and Top Drag Snap Bar to closely match Windows 11's native Snap Layouts visual design (clean neutral surface, frameless rounded zone tiles, Windows accent blue hover)."),
            ("0.10", "19.11.2026",
             "Maksimize üstü yerleşim menüsüne (Snap Flyout) tüm fiziksel monitörleri doğrudan seçebilme ve canlı önizleme desteği eklendi; böylece herhangi bir pencere tek tıkla farklı bir fiziksel monitöre ve o monitörün sanal monitörlerine/düzenlerine gönderilebilir.",
             "Added physical monitor selector pills with live screen preview to the Maximize Snap Flyout, allowing any window to be snapped directly onto any physical monitor and its virtual monitors/layouts."),
            ("0.9", "18.11.2026",
             "Windows 11 orijinal Snap Layouts menüsünün (ana pencere dahil) hiçbir zaman açılmaması sağlandı (ImmersiveShell SettingsCache COM yenilemesi, WndProc HTMAXBUTTON filtrelemesi ve anlık EVENT_OBJECT_SHOW yakalama). Maksimize üstü Flyout menüsü her zaman Windows temasında çalışacak ve maksimize tuşunu kapatmayacak şekilde başlık çubuğu altına taşındı. Sanal Monitör Tam Ekran kartı yerleşimlerin en başına alındı. Oran kutularına alt-üst sayı arttırma/azaltma (▲/▼) butonları ile 25%, 33%, 50%, 66%, 75%, 100% hızlı seçim çipleri eklendi. Kayıtlı sanal monitör şablonlarına görsel oran önizleme şemaları eklendi.",
             "Completely prevented Windows 11 native Snap Layouts flyout from appearing on any window including MainWindow (ImmersiveShell SettingsCache COM refresh, WndProc HTMAXBUTTON intercept, and instant EVENT_OBJECT_SHOW hook). Snap Flyout now always uses the Windows system theme and is positioned below the maximize button without covering it. Moved Virtual Monitor Full Screen card to the start of the layout cards. Added ▲/▼ spinner buttons and 25%, 33%, 50%, 66%, 75%, 100% quick ratio chips to percentage inputs. Added visual ratio thumbnail diagrams to Saved Virtual Monitor Presets."),
            ("0.8", "18.11.2026",
             "Snappy Layouts ana yönetim penceresinin kendi maksimize (büyüt) tuşu üzerinde de Snappy Layouts yerleşim menüsünün (Flyout) açılması ve sanal monitör tam ekran / yerleşim bölgelerine yaslanabilmesi sağlandı.",
             "Enabled Snappy Layouts's custom Snap Flyout and virtual monitor maximize/zone snapping on Snappy Layouts's own main window maximize button."),
            ("0.7", "18.11.2026",
             "Sol menüdeki ve Ayarlar sekmesindeki Dil ve Tema açılır menülerinin (ComboBox) tıklanmama/açılmama sorunu giderildi (Fluent tema sözlüğü değiştiğinde örtük ComboBox/ToggleButton/Popup stillerinin ezilmesi düzeltildi).",
             "Fixed Language and Theme dropdown menus (ComboBox) in the sidebar and Settings tab not opening on click (resolved Fluent theme dictionary overriding custom ComboBox/ToggleButton/Popup templates)."),
            ("0.6", "17.11.2026",
             "Proje ana çalışma dizini 'C:\\Users\\yusuf\\Documents\\Özel\\Yapay Zeka\\Snappy Layouts' klasörüne taşındı.",
             "Moved project workspace directory to 'C:\\Users\\yusuf\\Documents\\Özel\\Yapay Zeka\\Snappy Layouts'."),
            ("0.5", "17.11.2026",
             "Uygulama adı, klasör yapısı, proje/dosya adları ve tüm kod içerikleri 'Snappy Layouts' olarak güncellendi.",
             "Updated application name, folder structure, project/file names, and all source code references to 'Snappy Layouts'."),
            ("0.4", "16.11.2026",
             "Uygulama adı, klasör yapısı, proje/dosya adları ve tüm kod içerikleri yeniden adlandırıldı.",
             "Renamed application name, folder structure, project/file names, and source code references."),
            ("0.3", "16.11.2026",
             "Sanal monitör düzenleri ile pencere yaslanma düzenleri tamamen ayrıldı (her fiziksel/sanal monitöre özel bağımsız pencere düzeni listesi ve 2 aşamalı seçim). Windows'un kendi maksimize üstü Snap Flyout menüsünün Snappy Layouts ile aynı anda çıkması engellendi.",
             "Completely separated virtual monitor presets from window snap layouts (independent per-physical/virtual-monitor layout lists & 2-step selector). Prevented Windows's native maximize Snap Flyout from appearing alongside Snappy Layouts."),
            ("0.2", "16.11.2026",
             "Açık (Light) temada buton, menü, açılır kutu ve metin görünürlüğü/kontrastı düzeltildi.",
             "Fixed button, sidebar menu, combobox, and text contrast/visibility in Light theme."),
            ("0.1", "16.11.2026",
             "İlk sürüm: özelleştirilebilir yerleşim düzenleri, sanal monitörler, çalışma alanları, kısayollar, TR/EN dil, açık/koyu tema, DPI desteği.",
             "Initial release: customizable snap layouts, virtual monitors, workspaces, hotkeys, TR/EN language, light/dark theme, DPI support."),
        };
    }
}
