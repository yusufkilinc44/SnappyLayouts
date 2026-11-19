using System.Collections.Generic;

namespace SnappyLayout
{
    /// <summary>
    /// Uygulama kimliği ve sürüm bilgisi.
    /// SÜRÜM POLİTİKASI: Her değişiklikte sürüm 0.1 artırılır (v0.1 → v0.2 → ...).
    /// Canlıya alındığında v1.0 yapılır. Sürüm numarası Snappy Layout.csproj içindeki
    /// &lt;InformationalVersion&gt; ile birlikte burada da güncellenmelidir.
    /// </summary>
    public static class AppInfo
    {
        public const string Name = "Snappy Layout";
        public const string Version = "0.16";
        public const string Author = "YUSUF KILINÇ";
        public const string AuthorDateTr = "Kasım 2026";
        public const string AuthorDateEn = "November 2026";

        public static string VersionText => "v" + Version;
        public static string Signature => $"{Author} — {AuthorDateTr}";

        /// <summary>Sürüm geçmişi (en yeni en üstte).</summary>
        public static readonly List<(string Version, string Date, string Tr, string En)> History = new List<(string, string, string, string)>
        {
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
             "Pencereyi fareyle ekranın en üstüne sürükleyince Windows'un kendi Snap Bar'ı bastırılarak Snappy Layout'un üst sürükleme yerleşim çubuğunun (TopSnapBarWindow) açılması sağlandı. Hem maksimize üstü Flyout hem de üst sürükleme Snap Bar ekranları Windows 11 orijinal Snap Layouts görünümüne (koyu/açık nötr yüzey, çerçevesiz yuvarlatılmış bölge kutuları, Windows vurgu mavisi) birebir benzetildi.",
             "Replaced Windows 11's native top-of-screen drag Snap Bar with Snappy Layout's TopSnapBarWindow when dragging any window to the top edge. Redesigned both the Maximize Snap Flyout and Top Drag Snap Bar to closely match Windows 11's native Snap Layouts visual design (clean neutral surface, frameless rounded zone tiles, Windows accent blue hover)."),
            ("0.10", "19.11.2026",
             "Maksimize üstü yerleşim menüsüne (Snap Flyout) tüm fiziksel monitörleri doğrudan seçebilme ve canlı önizleme desteği eklendi; böylece herhangi bir pencere tek tıkla farklı bir fiziksel monitöre ve o monitörün sanal monitörlerine/düzenlerine gönderilebilir.",
             "Added physical monitor selector pills with live screen preview to the Maximize Snap Flyout, allowing any window to be snapped directly onto any physical monitor and its virtual monitors/layouts."),
            ("0.9", "18.11.2026",
             "Windows 11 orijinal Snap Layouts menüsünün (ana pencere dahil) hiçbir zaman açılmaması sağlandı (ImmersiveShell SettingsCache COM yenilemesi, WndProc HTMAXBUTTON filtrelemesi ve anlık EVENT_OBJECT_SHOW yakalama). Maksimize üstü Flyout menüsü her zaman Windows temasında çalışacak ve maksimize tuşunu kapatmayacak şekilde başlık çubuğu altına taşındı. Sanal Monitör Tam Ekran kartı yerleşimlerin en başına alındı. Oran kutularına alt-üst sayı arttırma/azaltma (▲/▼) butonları ile 25%, 33%, 50%, 66%, 75%, 100% hızlı seçim çipleri eklendi. Kayıtlı sanal monitör şablonlarına görsel oran önizleme şemaları eklendi.",
             "Completely prevented Windows 11 native Snap Layouts flyout from appearing on any window including MainWindow (ImmersiveShell SettingsCache COM refresh, WndProc HTMAXBUTTON intercept, and instant EVENT_OBJECT_SHOW hook). Snap Flyout now always uses the Windows system theme and is positioned below the maximize button without covering it. Moved Virtual Monitor Full Screen card to the start of the layout cards. Added ▲/▼ spinner buttons and 25%, 33%, 50%, 66%, 75%, 100% quick ratio chips to percentage inputs. Added visual ratio thumbnail diagrams to Saved Virtual Monitor Presets."),
            ("0.8", "18.11.2026",
             "Snappy Layout ana yönetim penceresinin kendi maksimize (büyüt) tuşu üzerinde de Snappy Layout yerleşim menüsünün (Flyout) açılması ve sanal monitör tam ekran / yerleşim bölgelerine yaslanabilmesi sağlandı.",
             "Enabled Snappy Layout's custom Snap Flyout and virtual monitor maximize/zone snapping on Snappy Layout's own main window maximize button."),
            ("0.7", "18.11.2026",
             "Sol menüdeki ve Ayarlar sekmesindeki Dil ve Tema açılır menülerinin (ComboBox) tıklanmama/açılmama sorunu giderildi (Fluent tema sözlüğü değiştiğinde örtük ComboBox/ToggleButton/Popup stillerinin ezilmesi düzeltildi).",
             "Fixed Language and Theme dropdown menus (ComboBox) in the sidebar and Settings tab not opening on click (resolved Fluent theme dictionary overriding custom ComboBox/ToggleButton/Popup templates)."),
            ("0.6", "17.11.2026",
             "Proje ana çalışma dizini 'C:\\Users\\yusuf\\Documents\\Özel\\Yapay Zeka\\Snappy Layouts' klasörüne taşındı.",
             "Moved project workspace directory to 'C:\\Users\\yusuf\\Documents\\Özel\\Yapay Zeka\\Snappy Layouts'."),
            ("0.5", "17.11.2026",
             "Uygulama adı, klasör yapısı, proje/dosya adları ve tüm kod içerikleri 'Snappy Layout' olarak güncellendi.",
             "Updated application name, folder structure, project/file names, and all source code references to 'Snappy Layout'."),
            ("0.4", "16.11.2026",
             "Uygulama adı, klasör yapısı, proje/dosya adları ve tüm kod içerikleri yeniden adlandırıldı.",
             "Renamed application name, folder structure, project/file names, and source code references."),
            ("0.3", "16.11.2026",
             "Sanal monitör düzenleri ile pencere yaslanma düzenleri tamamen ayrıldı (her fiziksel/sanal monitöre özel bağımsız pencere düzeni listesi ve 2 aşamalı seçim). Windows'un kendi maksimize üstü Snap Flyout menüsünün Snappy Layout ile aynı anda çıkması engellendi.",
             "Completely separated virtual monitor presets from window snap layouts (independent per-physical/virtual-monitor layout lists & 2-step selector). Prevented Windows's native maximize Snap Flyout from appearing alongside Snappy Layout."),
            ("0.2", "16.11.2026",
             "Açık (Light) temada buton, menü, açılır kutu ve metin görünürlüğü/kontrastı düzeltildi.",
             "Fixed button, sidebar menu, combobox, and text contrast/visibility in Light theme."),
            ("0.1", "16.11.2026",
             "İlk sürüm: özelleştirilebilir yerleşim düzenleri, sanal monitörler, çalışma alanları, kısayollar, TR/EN dil, açık/koyu tema, DPI desteği.",
             "Initial release: customizable snap layouts, virtual monitors, workspaces, hotkeys, TR/EN language, light/dark theme, DPI support."),
        };
    }
}
