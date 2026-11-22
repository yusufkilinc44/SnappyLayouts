using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using SnappyLayout.Models;

namespace SnappyLayout.Services
{
    /// <summary>
    /// Türkçe ve İngilizce yerelleştirme servisi.
    /// Varsayılan seçim Windows görüntüleme dilini (GetUserDefaultUILanguage / CurrentUICulture) temel alır.
    /// </summary>
    public sealed class Loc : INotifyPropertyChanged
    {
        public static Loc Instance { get; } = new Loc();
        public static event Action LanguageChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();

        private static string _currentCode = "en";

        public static string CurrentCode => _currentCode;
        public static bool IsTurkish => _currentCode == "tr";

        public static bool DetectWindowsTurkish() => DetectWindowsLanguageCode() == "tr";

        public static string DetectWindowsLanguageCode()
        {
            try
            {
                ushort langId = GetUserDefaultUILanguage();
                int primary = langId & 0x3FF;
                if (primary == 0x1F) return "tr";
                if (primary == 0x0A) return "es";
                if (primary == 0x16) return "pt";
                if (primary == 0x04) return "zh";
                if (primary == 0x0C) return "fr";
                if (primary == 0x07) return "de";
                if (primary == 0x19) return "ru";

                string ui = (CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName ?? "").ToLowerInvariant();
                if (ui == "tr" || ui == "es" || ui == "pt" || ui == "zh" || ui == "fr" || ui == "de" || ui == "ru")
                    return ui;
            }
            catch { }
            return "en";
        }

        public static void Apply(LanguageChoice choice)
        {
            _currentCode = choice switch
            {
                LanguageChoice.English => "en",
                LanguageChoice.Turkish => "tr",
                LanguageChoice.Spanish => "es",
                LanguageChoice.Portuguese => "pt",
                LanguageChoice.Chinese => "zh",
                LanguageChoice.French => "fr",
                LanguageChoice.German => "de",
                LanguageChoice.Russian => "ru",
                _ => DetectWindowsLanguageCode()
            };
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke();
        }

        public string this[string key] => T(key);

        public static string T(string key)
        {
            if (_currentCode == "tr" && Tr.TryGetValue(key, out var trVal)) return trVal;
            if (_currentCode != "en" && _currentCode != "tr")
            {
                var extra = LocExtraLanguages.Get(_currentCode);
                if (extra != null && extra.TryGetValue(key, out var exVal)) return exVal;
            }
            if (En.TryGetValue(key, out var enVal)) return enVal;
            return key;
        }

        private static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            // Genel & Başlık
            ["AppSubtitle"] = "Özelleştirilebilir Pencere Yerleşim Düzenleri ve Sanal Monitör Yöneticisi",
            ["AuthorBadge"] = "YUSUF KILINÇ • Kasım 2026",
            ["VersionLabel"] = "Sürüm",
            ["Active"] = "Aktif",
            ["Paused"] = "Duraklatıldı",
            ["Pause"] = "Duraklat",
            ["Resume"] = "Devam Et",
            ["Yes"] = "Evet",
            ["No"] = "Hayır",
            ["Cancel"] = "İptal",
            ["Close"] = "Kapat",
            ["Save"] = "Kaydet",
            ["Apply"] = "Uygula",
            ["Delete"] = "Sil",
            ["Edit"] = "Düzenle",
            ["Add"] = "Ekle",
            ["Duplicate"] = "Çoğalt",
            ["Reset"] = "Sıfırla",
            ["Refresh"] = "Yenile",
            ["Clear"] = "Temizle",

            // Navigasyon
            ["NavLayouts"] = "Yerleşim Düzenleri",
            ["NavVirtualMonitors"] = "Sanal Monitörler",
            ["NavWorkspaces"] = "Çalışma Alanları",
            ["NavHotkeys"] = "Kısayol Tuşları",
            ["NavSettings"] = "Ayarlar & DPI",
            ["NavHelp"] = "Yardım & Hakkında",

            // Monitör ve Hedef Alan
            ["TargetMonitor"] = "Hedef Monitör / Alan:",
            ["Step1PhysicalMonitor"] = "1. Fiziksel Monitör Seçin:",
            ["Step2VirtualMonitor"] = "2. Pencere Düzeni Ayarlanacak Sanal Monitör Seçin:",
            ["NoVirtualOnThisMonitor"] = "Bu fiziksel monitörde sanal monitör bölmesi yok — pencere düzenleri doğrudan fiziksel monitöre uygulanır.",
            ["GoToVirtualMonitorsBtn"] = "◫ Bu Monitörü Sanal Monitörlere Böl →",
            ["CopyLayoutsToSiblingsBtn"] = "⧉ Bu Pencere Düzenlerini Diğer Sanal Monitörlere Kopyala",
            ["CopiedToSiblingsMsg"] = "Seçili sanal monitörün pencere yerleşim düzenleri bu monitördeki diğer tüm sanal monitörlere kopyalandı.",
            ["ActiveTargetBanner"] = "Şu anda yalnızca seçili alan içindeki PENCERE YASLANMA DÜZENLERİNİ düzenliyorsunuz:",
            ["PhysicalMonitor"] = "Fiziksel Monitör",
            ["RenameMonitor"] = "Monitöre İsim Ver",
            ["RenameMonitorPrompt"] = "Bu fiziksel monitör için özel bir isim girin (boş bırakırsanız donanım adı kullanılır):",
            ["RenameMonitorReset"] = "Varsayılan Donanım Adına Dön",
            ["HardwareName"] = "Donanım Adı",
            ["VirtualMonitor"] = "Sanal Monitör",
            ["VirtualShort"] = "Sanal",
            ["FullPhysicalMonitor"] = "Tam Fiziksel Monitör",
            ["AspectRatio"] = "En / Boy Oranı",
            ["Resolution"] = "Çözünürlük",
            ["DpiScale"] = "DPI Ölçeği",
            ["Primary"] = "Birincil",

            // Silinemez Gerçek Tam Ekran
            ["PermFullTitle"] = "Tam Fiziksel Monitör (Silinemez)",
            ["PermFullDesc"] = "Sanal monitörler aktif olsa bile pencereyi gerçek fiziksel monitörün tamamına kaplatır. Bu yerleşim her zaman mevcuttur ve silinemez.",
            ["PermFullBadge"] = "SABİT / SİLİNEMEZ",
            ["VirtualFullTitle"] = "Sanal Monitör Tam Ekran",
            ["VirtualFullDesc"] = "Pencereyi yalnızca bulunduğu sanal monitörün sınırları içinde tam ekran yapar.",
            ["CannotDeleteVirtualFull"] = "Sanal Monitör Tam Ekran yerleşimi sabit bir sistem öğesidir; silinemez ve sırası değiştirilemez.",
            ["VirtualFullSettingsTitle"] = "Sanal Monitör Tam Ekran Davranışı",
            ["VirtualFullShortcutLabel"] = "Klavye Kısayolu:",
            ["Coverage"] = "Kapsama:",
            ["MoveUp"] = "Yukarı Taşı",
            ["MoveDown"] = "Aşağı Taşı",
            ["FilterByScreenTypes"] = "Ekran Tipine Göre Filtrele:",
            ["NoMatchingLayouts"] = "Seçili ekran tipi filtresine uyan yerleşim düzeni yok.",

            // Yerleşim Düzenleri Sekmesi
            ["LayoutsTitle"] = "Pencere Yerleşim Düzenleri",
            ["LayoutsSubtitle"] = "Pencerelerin maksimize tuşu üzerine gelindiğinde açılan yerleşim şablonlarını yönetin. Tüm şemalar seçili monitörün (veya sanal monitörün) gerçek en/boy oranında çizilir.",
            ["NewLayout"] = "+ Yeni Yerleşim Ekle",
            ["RestoreDefaults"] = "Windows Varsayılanlarını Yükle",
            ["DeleteAllCustom"] = "Tümünü Sil",
            ["LayoutName"] = "Yerleşim Adı:",
            ["Columns"] = "Sütunlar",
            ["Rows"] = "Satırlar",
            ["AddCol"] = "+ Sütun",
            ["RemCol"] = "− Sütun",
            ["AddRow"] = "+ Satır",
            ["RemRow"] = "− Satır",
            ["EqualizeCols"] = "Sütunları Eşitle",
            ["EqualizeRows"] = "Satırları Eşitle",
            ["MergeSelected"] = "Seçili Hücreleri Birleştir",
            ["SplitAll"] = "Birleştirmeleri Çöz (Izgaraya Dön)",
            ["ColWeights"] = "Sütun Genişlik Oranları (%):",
            ["RowWeights"] = "Satır Yükseklik Oranları (%):",
            ["EditorHint"] = "İpucu: Izgara üzerindeki iç çizgileri fareyle sürükleyerek sütun/satır oranlarını değiştirebilir; birden fazla hücreyi sürükleyip seçerek tek bölge halinde birleştirebilirsiniz. Birleşik bir bölgeye sağ tıklayarak tekrar bölebilirsiniz.",
            ["ShowOnLandscape"] = "Standart (16:9 / 16:10)",
            ["ShowOnUltrawide"] = "Ultra-Geniş (21:9 / 32:9)",
            ["ShowOnPortrait"] = "Dikey (Portre)",
            ["VisibilityFilter"] = "Gösterileceği Ekran Tipleri:",
            ["TestFlyoutBtn"] = "Yerleşim Menüsünü Şimdi Göster",
            ["ZonesCount"] = "bölge",
            ["CannotDeletePermanent"] = "Tam Fiziksel Monitör yerleşimi sabit bir sistem öğesidir ve silinemez.",

            // Sanal Monitörler Sekmesi
            ["VmTitle"] = "Sanal Monitörler (Yan Yana Bölme)",
            ["VmSubtitle"] = "Tek bir fiziksel monitörü yan yana istediğiniz sayıda ve istediğiniz genişlik oranlarında sanal monitörlere bölün. Sanal monitör aktifken yerleşim düzenleri ve tam ekran işlemi o sanal monitörün sınırlarına göre çalışır.",
            ["SelectPhysicalMonitor"] = "İşlem Yapılacak Fiziksel Monitör:",
            ["VmCountLabel"] = "Sanal Monitör Sayısı:",
            ["VmAdd"] = "+ Sanal Monitör Ekle",
            ["VmRemove"] = "− Sonuncuyu Kaldır",
            ["VmEqualize"] = "Genişlikleri Eşitle",
            ["VmDisable"] = "Tek Ekran (Bölmeyi Kapat)",
            ["VmWidthsTitle"] = "Sanal Monitör Genişlikleri (Eşit veya Eşit Olmayan Oranlar):",
            ["VmBarHint"] = "Aşağıdaki monitör şeması seçili fiziksel monitörün gerçek en/boy oranındadır. Ayırıcı çizgileri sola/sağa sürükleyerek veya aşağıdaki kutulardan oran/yüzde girerek eşit olmayan genişlikler verebilirsiniz.",
            ["VmShowBordersNow"] = "Sınır Çizgilerini Göster (Vurgula)",
            ["VmAlwaysShowBorders"] = "Sanal monitör sınır çizgilerini her zaman ekranda göster",
            ["VmShowOnDrag"] = "Pencere sürüklerken ve ayar yaparken sınırları kalın çizgiyle göster",
            ["VmBorderThickness"] = "Sınır Çizgisi Kalınlığı (px):",
            ["VmBorderColor"] = "Sınır Çizgisi Rengi:",
            ["ColorAccent"] = "Vurgu Rengi (Mavi/Sistem)",
            ["ColorCyan"] = "Camgöbeği (Cyan)",
            ["ColorGold"] = "Altın Sarısı",
            ["ColorCoral"] = "Mercan Kırmızı",
            ["ColorEmerald"] = "Zümrüt Yeşili",
            ["VmPresetsTitle"] = "Kayıtlı Sanal Monitör Düzenleri (Hazır Şablonlar)",
            ["VmPresetSaveNew"] = "+ Mevcut Oranları Şablon Olarak Kaydet",
            ["VmPresetNamePrompt"] = "Şablon adı:",
            ["VmPresetApplied"] = "Sanal monitör düzeni uygulandı.",

            // Çalışma Alanları Sekmesi
            ["WsTitle"] = "Çalışma Alanları ve Uygulama Yerleşimleri",
            ["WsSubtitle"] = "Sanal monitör oranlarını ve seçilen sanal/fiziksel monitör içindeki açık uygulamaları (pencere konumları, tarayıcı URL adresleri ve klasör yollarıyla birlikte) kaydedin; istediğiniz an mevcut masaüstünde veya yeni bir sanal masaüstünde tek tıkla açın.",
            ["WsCaptureNew"] = "+ Mevcut Ekranı ve Uygulamaları Kaydet",
            ["WsApplyCurrent"] = "Mevcut Masaüstünde Aç",
            ["WsApplyNewDesktop"] = "Yeni Masaüstünde Aç",
            ["WsScopeFull"] = "Tüm Fiziksel Monitör",
            ["WsScopeVirtual"] = "Sadece Seçili Sanal Monitör",
            ["WsAppsCount"] = "uygulama kayıtlı",
            ["WsLayoutOnly"] = "Sadece sanal monitör düzeni (uygulama yok)",
            ["WsCreatingDesktop"] = "Yeni sanal masaüstü oluşturuluyor...",
            ["WsCaptureTitle"] = "Çalışma Alanı Kaydet",
            ["WsNameLabel"] = "Çalışma Alanı Adı:",
            ["WsMonitorLabel"] = "Monitör:",
            ["WsScopeLabel"] = "Kapsam:",
            ["WsIncludeAppsCheck"] = "Bu alandaki açık uygulamaları, konumlarını ve tarayıcı URL adreslerini de kaydet",
            ["WsDetectedWindows"] = "Kapsam İçindeki Açık Pencereler (kaydedilecek olanları işaretleyin, URL'leri düzenleyebilirsiniz):",
            ["WsAddManualApp"] = "+ Manuel Uygulama / URL Ekle",
            ["WsAskTitle"] = "Çalışma Alanını Uygula",
            ["WsAskPrompt"] = "Bu çalışma alanı nasıl açılsın?",
            ["WsNoWorkspaces"] = "Henüz kaydedilmiş bir çalışma alanı yok. Yukarıdaki '+ Mevcut Ekranı ve Uygulamaları Kaydet' düğmesiyle hemen oluşturabilirsiniz.",

            // Kısayollar Sekmesi
            ["HkTitle"] = "Kısayol Tuş Kombinasyonları",
            ["HkSubtitle"] = "Tüm işlemlere, kayıtlı sanal monitör şablonlarına ve çalışma alanlarına global klavye kısayolları atayın.",
            ["HkAction_ShowFlyout"] = "Aktif pencere için yerleşim menüsünü (Flyout) aç",
            ["HkAction_ShowBorders"] = "Sanal monitör sınır çizgilerini göster / gizle",
            ["HkAction_VirtualMaximize"] = "Aktif pencereyi bulunduğu sanal monitörde tam ekran yap",
            ["HkAction_PhysicalMaximize"] = "Aktif pencereyi gerçek fiziksel monitörde tam ekran yap",
            ["HkAction_NextSegment"] = "Aktif pencereyi sonraki sanal monitöre taşı",
            ["HkAction_PrevSegment"] = "Aktif pencereyi önceki sanal monitöre taşı",
            ["HkAction_RestoreWindow"] = "Aktif pencereyi önceki boyutuna döndür",
            ["HkAction_ToggleVirtual"] = "Sanal monitörleri etkinleştir / devre dışı bırak",
            ["HkAction_OpenSettings"] = "Snappy Layouts yönetim penceresini aç",
            ["HkChange"] = "Kısayol Ata",
            ["HkClear"] = "Kaldır",
            ["HkPressKeys"] = "Lütfen istediğiniz tuş kombinasyonuna basın (Örn: Ctrl + Alt + Z)...",
            ["HkConflict"] = "Bu kısayol başka bir işlemde kullanılıyor veya Windows tarafından ayrılmış.",
            ["HkPresetPrefix"] = "Sanal Monitör Şablonu: ",
            ["HkWorkspacePrefix"] = "Çalışma Alanı: ",
            ["HkRestoreDefaults"] = "Varsayılan Kısayolları Yükle",

            // Ayarlar & DPI Sekmesi
            ["SetTitle"] = "Ayarlar ve Çoklu Monitör / DPI Yönetimi",
            ["SetSubtitle"] = "Dil, tema, maksimize tuşu davranışı ve farklı çözünürlük/ölçeklendirme (100%, 125%, 150% vb.) ayarlarına sahip monitörler için dinamik DPI yapılandırması.",
            ["SetLanguage"] = "Uygulama Dili (Language):",
            ["LangSystem"] = "Windows Dili (Varsayılan)",
            ["LangTr"] = "Türkçe",
            ["LangEn"] = "English (İngilizce)",
            ["SetTheme"] = "Görünüm Teması:",
            ["ThemeSystem"] = "Windows Teması (Varsayılan)",
            ["ThemeLight"] = "Açık Tema (Light)",
            ["ThemeDark"] = "Koyu Tema (Dark)",
            ["SetFlyoutEnabled"] = "Pencerelerin maksimize tuşu üzerine gelindiğinde Snappy Layouts yerleşim menüsünü göster",
            ["SetHoverDelay"] = "Maksimize tuşu üzerinde bekleme süresi (ms):",
            ["SetZoneGap"] = "Yerleşim bölgeleri arasındaki boşluk (px):",
            ["SetVirtualMax"] = "Sanal monitör aktifken pencerede Tam Ekran (Maximize) tuşuna basıldığında o sanal monitör içinde tam ekran yap",
            ["SetShiftBypass"] = "Shift tuşuna basılı tutarak Tam Ekran'a basıldığında sanal monitörü atla ve gerçek fiziksel monitörü kapla",
            ["SetStartWithWin"] = "Windows başladığında Snappy Layouts'ı otomatik başlat",
            ["SetWsOpenMode"] = "Çalışma alanı açılırken varsayılan davranış:",
            ["WsModeAsk"] = "Her seferinde sor (Mevcut Masaüstü / Yeni Masaüstü)",
            ["WsModeCurrent"] = "Her zaman mevcut masaüstünde aç",
            ["WsModeNew"] = "Her zaman yeni sanal masaüstünde aç",
            ["DpiTableTitle"] = "Algılanan Fiziksel Monitörler ve Dinamik DPI Durumu",
            ["DpiDesc"] = "Per-Monitor V2 DPI desteği aktiftir. Farklı çözünürlük ve ölçeklendirmeye sahip monitörler arasında geçiş yapılırken pencere çerçeveleri (DWM görünmez sınırlar dahil) iki aşamalı dinamik DPI hesaplamasıyla piksel kayması olmadan yerleştirilir.",
            ["ExportSettings"] = "Ayarları Dışa Aktar (.json)",
            ["ImportSettings"] = "Ayarları İçe Aktar (.json)",

            // Yardım & Hakkında Sekmesi
            ["HelpTitle"] = "Kullanım Kılavuzu ve Hakkında",
            ["HelpSubtitle"] = "Snappy Layouts'ın tüm özelliklerini adım adım nasıl kullanacağınızı aşağıda bulabilirsiniz.",

            // Flyout (Maksimize Üstü Açılır Pencere)
            ["FlyoutHeader"] = "Pencere Yerleşimi Seçin",
            ["FlyoutVirtualFull"] = "Sanal Monitör Tam Ekran",
            ["FlyoutPhysicalFull"] = "Tam Fiziksel Monitör",
            ["FlyoutSpanFull"] = "Birleşik Tam Ekran",
            ["FlyoutSpanHint"] = "İpucu: Shift + Tık ile birden fazla sanal monitörü birleştirebilirsiniz",
            ["FlyoutPhysicalMonitor"] = "Fiziksel Monitör:",
            ["FlyoutTargetArea"] = "Hedef Alan:",
            ["FlyoutOpenApp"] = "Ayarlar & Düzenleyici",
            ["FlyoutShowBorders"] = "Sınırları Göster",

            // Tepsi Menüsü
            ["TrayOpen"] = "Snappy Layouts Yönetim Panelini Aç",
            ["TrayShowFlyout"] = "Aktif Pencere İçin Yerleşim Menüsü",
            ["TrayShowBorders"] = "Sanal Monitör Sınırlarını Göster",
            ["TrayExit"] = "Çıkış",
        };

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // General & Header
            ["AppSubtitle"] = "Customizable Snap Layouts & Virtual Monitor Manager for Windows",
            ["AuthorBadge"] = "YUSUF KILINÇ • Kasım 2026",
            ["VersionLabel"] = "Version",
            ["Active"] = "Active",
            ["Paused"] = "Paused",
            ["Pause"] = "Pause",
            ["Resume"] = "Resume",
            ["Yes"] = "Yes",
            ["No"] = "No",
            ["Cancel"] = "Cancel",
            ["Close"] = "Close",
            ["Save"] = "Save",
            ["Apply"] = "Apply",
            ["Delete"] = "Delete",
            ["Edit"] = "Edit",
            ["Add"] = "Add",
            ["Duplicate"] = "Duplicate",
            ["Reset"] = "Reset",
            ["Refresh"] = "Refresh",
            ["Clear"] = "Clear",

            // Navigation
            ["NavLayouts"] = "Snap Layouts",
            ["NavVirtualMonitors"] = "Virtual Monitors",
            ["NavWorkspaces"] = "Workspaces & Apps",
            ["NavHotkeys"] = "Keyboard Shortcuts",
            ["NavSettings"] = "Settings & DPI",
            ["NavHelp"] = "Help & About",

            // Monitor & Target Area
            ["TargetMonitor"] = "Target Monitor / Area:",
            ["Step1PhysicalMonitor"] = "1. Select Physical Monitor:",
            ["Step2VirtualMonitor"] = "2. Select Virtual Monitor to Configure Window Layouts For:",
            ["NoVirtualOnThisMonitor"] = "This physical monitor is not split into virtual monitors — window snap layouts apply directly to the physical monitor.",
            ["GoToVirtualMonitorsBtn"] = "◫ Split This Monitor into Virtual Monitors →",
            ["CopyLayoutsToSiblingsBtn"] = "⧉ Copy These Window Layouts to Sibling Virtual Monitors",
            ["CopiedToSiblingsMsg"] = "Window snap layouts of the selected virtual monitor have been copied to all other virtual monitors on this screen.",
            ["ActiveTargetBanner"] = "You are currently editing WINDOW SNAP LAYOUTS exclusively for:",
            ["PhysicalMonitor"] = "Physical Monitor",
            ["RenameMonitor"] = "Rename Monitor",
            ["RenameMonitorPrompt"] = "Enter a custom name for this physical monitor (leave empty to use the hardware name):",
            ["RenameMonitorReset"] = "Reset to Hardware Name",
            ["HardwareName"] = "Hardware Name",
            ["VirtualMonitor"] = "Virtual Monitor",
            ["VirtualShort"] = "Virtual",
            ["FullPhysicalMonitor"] = "Full Physical Monitor",
            ["AspectRatio"] = "Aspect Ratio",
            ["Resolution"] = "Resolution",
            ["DpiScale"] = "DPI Scale",
            ["Primary"] = "Primary",

            // Indeletable Physical Fullscreen
            ["PermFullTitle"] = "Full Physical Monitor (Indeletable)",
            ["PermFullDesc"] = "Covers the entire real physical monitor even when virtual monitors are active. This layout is always available and cannot be deleted.",
            ["PermFullBadge"] = "PERMANENT / INDELETABLE",
            ["VirtualFullTitle"] = "Virtual Monitor Fullscreen",
            ["VirtualFullDesc"] = "Maximizes the window strictly within the boundaries of its current virtual monitor.",
            ["CannotDeleteVirtualFull"] = "Virtual Monitor Fullscreen is a permanent system item; it cannot be deleted or reordered.",
            ["VirtualFullSettingsTitle"] = "Virtual Monitor Fullscreen Behavior",
            ["VirtualFullShortcutLabel"] = "Keyboard Shortcut:",
            ["Coverage"] = "Coverage:",
            ["MoveUp"] = "Move Up",
            ["MoveDown"] = "Move Down",
            ["FilterByScreenTypes"] = "Filter by Screen Types:",
            ["NoMatchingLayouts"] = "No layouts match the selected screen type filter.",

            // Layouts Tab
            ["LayoutsTitle"] = "Window Snap Layouts",
            ["LayoutsSubtitle"] = "Manage the snap templates shown when hovering over any window's maximize button. Every diagram is rendered using the true aspect ratio of the selected monitor (or virtual monitor).",
            ["NewLayout"] = "+ Add New Layout",
            ["RestoreDefaults"] = "Restore Windows Defaults",
            ["DeleteAllCustom"] = "Delete All",
            ["LayoutName"] = "Layout Name:",
            ["Columns"] = "Columns",
            ["Rows"] = "Rows",
            ["AddCol"] = "+ Column",
            ["RemCol"] = "− Column",
            ["AddRow"] = "+ Row",
            ["RemRow"] = "− Row",
            ["EqualizeCols"] = "Equalize Columns",
            ["EqualizeRows"] = "Equalize Rows",
            ["MergeSelected"] = "Merge Selected Cells",
            ["SplitAll"] = "Unmerge All (Reset to Grid)",
            ["ColWeights"] = "Column Width Ratios (%):",
            ["RowWeights"] = "Row Height Ratios (%):",
            ["EditorHint"] = "Tip: Drag inner grid lines to adjust column/row ratios, or drag-select multiple cells to merge them into a single zone. Right-click a merged zone to split it back.",
            ["ShowOnLandscape"] = "Standard (16:9 / 16:10)",
            ["ShowOnUltrawide"] = "Ultra-Wide (21:9 / 32:9)",
            ["ShowOnPortrait"] = "Portrait (Vertical)",
            ["VisibilityFilter"] = "Show on Screen Types:",
            ["TestFlyoutBtn"] = "Show Snap Flyout Now",
            ["ZonesCount"] = "zones",
            ["CannotDeletePermanent"] = "The Full Physical Monitor layout is a permanent system item and cannot be deleted.",

            // Virtual Monitors Tab
            ["VmTitle"] = "Virtual Monitors (Side-by-Side Split)",
            ["VmSubtitle"] = "Split a single physical monitor into any number of side-by-side virtual monitors with equal or unequal widths. Once active, snap layouts and fullscreen operate relative to each virtual monitor.",
            ["SelectPhysicalMonitor"] = "Physical Monitor to Configure:",
            ["VmCountLabel"] = "Virtual Monitor Count:",
            ["VmAdd"] = "+ Add Virtual Monitor",
            ["VmRemove"] = "− Remove Last",
            ["VmEqualize"] = "Equalize Widths",
            ["VmDisable"] = "Single Screen (Disable Split)",
            ["VmWidthsTitle"] = "Virtual Monitor Widths (Equal or Unequal Ratios):",
            ["VmBarHint"] = "The monitor diagram below matches the true aspect ratio of the selected physical monitor. Drag the divider bars left/right or enter ratios/percentages below for custom unequal widths.",
            ["VmShowBordersNow"] = "Highlight Boundary Lines Now",
            ["VmAlwaysShowBorders"] = "Always show virtual monitor boundary lines on screen",
            ["VmShowOnDrag"] = "Show thick boundary lines while dragging windows or adjusting splits",
            ["VmBorderThickness"] = "Boundary Line Thickness (px):",
            ["VmBorderColor"] = "Boundary Line Color:",
            ["ColorAccent"] = "System Accent Blue",
            ["ColorCyan"] = "Electric Cyan",
            ["ColorGold"] = "Amber Gold",
            ["ColorCoral"] = "Coral Red",
            ["ColorEmerald"] = "Emerald Green",
            ["VmPresetsTitle"] = "Saved Virtual Monitor Presets",
            ["VmPresetSaveNew"] = "+ Save Current Ratios as Preset",
            ["VmPresetNamePrompt"] = "Preset name:",
            ["VmPresetApplied"] = "Virtual monitor preset applied.",

            // Workspaces Tab
            ["WsTitle"] = "Workspaces & Saved App Placements",
            ["WsSubtitle"] = "Save virtual monitor ratios along with open applications (including window positions, browser URLs, and Explorer folders) inside a virtual or full physical monitor; restore them anytime on the current desktop or a new virtual desktop.",
            ["WsCaptureNew"] = "+ Save Current Screen & Applications",
            ["WsApplyCurrent"] = "Open on Current Desktop",
            ["WsApplyNewDesktop"] = "Open on New Desktop",
            ["WsScopeFull"] = "Entire Physical Monitor",
            ["WsScopeVirtual"] = "Selected Virtual Monitor Only",
            ["WsAppsCount"] = "saved apps",
            ["WsLayoutOnly"] = "Virtual monitor layout only (no apps)",
            ["WsCreatingDesktop"] = "Creating new virtual desktop...",
            ["WsCaptureTitle"] = "Save Workspace",
            ["WsNameLabel"] = "Workspace Name:",
            ["WsMonitorLabel"] = "Monitor:",
            ["WsScopeLabel"] = "Scope:",
            ["WsIncludeAppsCheck"] = "Also save open applications, window positions, and browser URLs in this area",
            ["WsDetectedWindows"] = "Open Windows in Scope (check the ones to save, edit URLs if needed):",
            ["WsAddManualApp"] = "+ Add Manual App / URL",
            ["WsAskTitle"] = "Apply Workspace",
            ["WsAskPrompt"] = "How would you like to open this workspace?",
            ["WsNoWorkspaces"] = "No saved workspaces yet. Click '+ Save Current Screen & Applications' above to create one.",

            // Hotkeys Tab
            ["HkTitle"] = "Global Keyboard Shortcuts",
            ["HkSubtitle"] = "Assign keyboard shortcuts to all actions, virtual monitor presets, and workspaces.",
            ["HkAction_ShowFlyout"] = "Show Snap Layout Flyout for active window",
            ["HkAction_ShowBorders"] = "Show / hide virtual monitor boundary lines",
            ["HkAction_VirtualMaximize"] = "Fullscreen active window inside its virtual monitor",
            ["HkAction_PhysicalMaximize"] = "Fullscreen active window across the real physical monitor",
            ["HkAction_NextSegment"] = "Move active window to next virtual monitor",
            ["HkAction_PrevSegment"] = "Move active window to previous virtual monitor",
            ["HkAction_RestoreWindow"] = "Restore active window to previous size",
            ["HkAction_ToggleVirtual"] = "Enable / disable virtual monitors",
            ["HkAction_OpenSettings"] = "Open Snappy Layouts manager window",
            ["HkChange"] = "Set Shortcut",
            ["HkClear"] = "Clear",
            ["HkPressKeys"] = "Press the desired key combination (e.g. Ctrl + Alt + Z)...",
            ["HkConflict"] = "This shortcut is already used by another action or reserved by Windows.",
            ["HkPresetPrefix"] = "Virtual Monitor Preset: ",
            ["HkWorkspacePrefix"] = "Workspace: ",
            ["HkRestoreDefaults"] = "Restore Default Shortcuts",

            // Settings & DPI Tab
            ["SetTitle"] = "Settings & Multi-Monitor / DPI Management",
            ["SetSubtitle"] = "Language, theme, maximize button hover behavior, and dynamic Per-Monitor V2 DPI compensation across monitors with different resolutions and scaling (100%, 125%, 150%, etc.).",
            ["SetLanguage"] = "Application Language (Dil):",
            ["LangSystem"] = "Windows Language (Default)",
            ["LangTr"] = "Türkçe (Turkish)",
            ["LangEn"] = "English",
            ["SetTheme"] = "Appearance Theme:",
            ["ThemeSystem"] = "Windows Theme (Default)",
            ["ThemeLight"] = "Light Theme",
            ["ThemeDark"] = "Dark Theme",
            ["SetFlyoutEnabled"] = "Show Snappy Layouts layout flyout when hovering over a window's maximize button",
            ["SetHoverDelay"] = "Maximize button hover delay (ms):",
            ["SetZoneGap"] = "Gap between snapped window zones (px):",
            ["SetVirtualMax"] = "When virtual monitors are active, pressing Maximize/Fullscreen fills the current virtual monitor",
            ["SetShiftBypass"] = "Holding Shift while clicking Maximize bypasses virtual monitor and fills the real physical monitor",
            ["SetStartWithWin"] = "Start Snappy Layouts automatically when Windows starts",
            ["SetWsOpenMode"] = "Default behavior when opening a Workspace:",
            ["WsModeAsk"] = "Always ask (Current Desktop / New Desktop)",
            ["WsModeCurrent"] = "Always open on current desktop",
            ["WsModeNew"] = "Always open on a new virtual desktop",
            ["DpiTableTitle"] = "Detected Physical Monitors & Dynamic DPI Status",
            ["DpiDesc"] = "Per-Monitor V2 DPI awareness is active. When moving or snapping windows between monitors with different resolutions and scaling factors, window frames (including invisible DWM borders) are compensated in two passes so windows never drift.",
            ["ExportSettings"] = "Export Settings (.json)",
            ["ImportSettings"] = "Import Settings (.json)",

            // Help & About Tab
            ["HelpTitle"] = "User Guide & About",
            ["HelpSubtitle"] = "Step-by-step instructions for every feature in Snappy Layouts.",

            // Flyout
            ["FlyoutHeader"] = "Choose Window Snap Layout",
            ["FlyoutVirtualFull"] = "Virtual Monitor Fullscreen",
            ["FlyoutPhysicalFull"] = "Full Physical Monitor",
            ["FlyoutSpanFull"] = "Combined Fullscreen",
            ["FlyoutSpanHint"] = "Tip: Shift + Click another virtual monitor to span across them",
            ["FlyoutPhysicalMonitor"] = "Physical Monitor:",
            ["FlyoutTargetArea"] = "Target Area:",
            ["FlyoutOpenApp"] = "Settings & Editor",
            ["FlyoutShowBorders"] = "Show Borders",

            // Tray
            ["TrayOpen"] = "Open Snappy Layouts Control Panel",
            ["TrayShowFlyout"] = "Show Layout Flyout for Active Window",
            ["TrayShowBorders"] = "Highlight Virtual Monitor Borders",
            ["TrayExit"] = "Exit",
        };
    }
}
