# Sürüm Geçmişi / Changelog

Bu projedeki tüm önemli değişiklikler bu dosyada belgelenmektedir.  
**Sürüm Politikası:** Her değişiklikte sürüm numarası `0.1` artırılır (`v0.1` → `v0.2` → ...), canlıya geçişte `v1.0` yapılır.

---

## [v0.12] — 19.11.2026
### Düzeltildi (TR)
- Farklı DPI ölçeklerine sahip fiziksel monitörler (örn. `%125` ve `%175`) arasında canlı önizleme çerçevesi (`SnapZonePreviewOverlay`) geçişi yapılırken `WM_DPICHANGED` yeniden ölçeklemesi nedeniyle yeşil/mavi önizleme kutusunun diğer monitöre taşması veya küçülmesi hatası giderildi.

### Fixed (EN)
- Fixed live preview overlay (`SnapZonePreviewOverlay`) overflowing onto adjacent monitors or shrinking when switching between physical monitors with different DPI scales (e.g. `125%` ↔ `175%`) due to `WM_DPICHANGED` rescaling.

---

## [v0.11] — 19.11.2026
### Eklendi / Değiştirildi (TR)
- **Üst Kenara Sürükleme Çubuğu (Top Drag Snap Bar):** Herhangi bir pencereyi fareyle ekranın (veya sanal monitörün) en üst kenarına sürüklediğinizde Windows 11'in yerleşik Snap Bar'ı bastırılarak Snappy Layout'un kendi `TopSnapBarWindow` çubuğu açılır; fareyi hedef bölge üzerinde bıraktığınızda pencere anında o bölgeye yerleştirilir.
- **Windows 11 Orijinal Görünüm:** Hem maksimize tuşu üstündeki `SnapFlyoutWindow` hem de üst kenar sürükleme çubuğu `TopSnapBarWindow`, Windows 11'in orijinal Snap Layouts tasarım diline (temiz `#2C2C2C` / `#F3F3F3` yüzey, dış çerçevesiz yuvarlatılmış bölge kutuları, Windows vurgu mavisi `#0078D4` hover ve yumuşak gölge) uygun şekilde yeniden tasarlandı.

### Added / Changed (EN)
- **Top-of-Screen Drag Snap Bar (`TopSnapBarWindow`):** Dragging any window to the top edge of any physical or virtual monitor now suppresses Windows 11's native Snap Bar and displays Snappy Layout's custom horizontal Snap Bar with live zone previews and drop-to-snap support.
- **Native Windows 11 Visual Design:** Redesigned both `SnapFlyoutWindow` and `TopSnapBarWindow` to match Windows 11's native Snap Layouts aesthetic (neutral `#2C2C2C` / `#F3F3F3` surface, frameless rounded zone tiles, Windows Accent Blue `#0078D4` hover highlight).

---

## [v0.10] — 19.11.2026
### Eklendi / Değiştirildi (TR)
- Maksimize üstü yerleşim menüsüne (Snap Flyout) **tüm fiziksel monitörleri seçebilme** çubuğu eklendi; üzerine gelindiğinde ilgili fiziksel monitör ekranda önizlenir, tıklandığında o fiziksel monitörün sanal monitörleri ve pencere yerleşim düzenleri anında açılır.

### Added / Changed (EN)
- Added **Physical Monitor selector pills** with live preview to the Maximize Snap Flyout so users can switch between all connected physical monitors and snap windows directly to any monitor's virtual monitors or layouts.

---

## [v0.9] — 18.11.2026
### Eklendi / Değiştirildi (TR)
- Windows 11 orijinal Snap Layouts menüsünün (ana pencere dahil) hiçbir zaman açılmaması sağlandı (`ImmersiveShell` `ISettingsCache` COM yenilemesi, `WndProc` `HTMAXBUTTON` filtrelemesi ve anlık `EVENT_OBJECT_SHOW` yakalama).
- Maksimize üstü Flyout menüsü uygulama temasından bağımsız olarak her zaman **Windows sistem temasında** çalışacak şekilde güncellendi.
- Flyout menüsü, maksimize tuşunu kapatmayacak şekilde başlık çubuğunun / maksimize tuşunun hemen altına taşındı.
- **Sanal Monitör Tam Ekran (`100%`)** kartı, fiziksel tam ekranın yanından alınarak alttaki yerleşim düzeni kartlarının en başına taşındı.
- Oran giriş kutularına alt-üst sayı arttırma/azaltma (`▲` / `▼`) butonları ile **`25%`, `33%`, `50%`, `66%`, `75%`, `100%`** hızlı seçim çipleri eklendi.
- Kayıtlı sanal monitör şablonlarına (Saved Virtual Monitor Presets) oranlara uygun görsel küçük önizleme şemaları eklendi.

### Added / Changed (EN)
- Completely prevented Windows 11 native Snap Layouts flyout from appearing on any window including `MainWindow` (`ImmersiveShell` `ISettingsCache` COM refresh, `WndProc` `HTMAXBUTTON` intercept, and instant `EVENT_OBJECT_SHOW` hook).
- Snap Flyout now always uses the **Windows system theme** regardless of the application theme setting.
- Positioned Snap Flyout directly below the maximize button so it never covers the maximize button.
- Moved the **Virtual Monitor Full Screen (`100%`)** card to the beginning of the layout cards list.
- Added `▲` / `▼` spinner buttons and **`25%`, `33%`, `50%`, `66%`, `75%`, `100%`** quick ratio chips to percentage inputs.
- Added true-aspect-ratio visual thumbnail diagrams to Saved Virtual Monitor Presets.

---

## [v0.8] — 18.11.2026
- Snappy Layout ana yönetim penceresinin kendi maksimize (büyüt) tuşu üzerinde de Snappy Layout yerleşim menüsünün (Flyout) açılması ve sanal monitör tam ekran / yerleşim bölgelerine yaslanabilmesi sağlandı.

---

## [v0.7] — 18.11.2026
- Sol menüdeki ve Ayarlar sekmesindeki Dil ve Tema açılır menülerinin (`ComboBox`) tıklanmama/açılmama sorunu giderildi (Fluent tema sözlüğü değiştiğinde örtük `ComboBox`/`ToggleButton`/`Popup` stillerinin ezilmesi düzeltildi).

---

## [v0.6] — 17.11.2026
- Proje ana çalışma dizini `Snappy Layouts` klasörüne taşındı.

---

## [v0.5] — 17.11.2026
- Uygulama adı, klasör yapısı, proje/dosya adları ve tüm kod içerikleri `Snappy Layout` olarak güncellendi.

---

## [v0.4] — 16.11.2026
- Uygulama adı, klasör yapısı, proje/dosya adları ve tüm kod içerikleri yeniden adlandırıldı.

---

## [v0.3] — 16.11.2026
- Sanal monitör düzenleri ile pencere yaslanma düzenleri tamamen ayrıldı (her fiziksel/sanal monitöre özel bağımsız pencere düzeni listesi ve 2 aşamalı seçim).
- Windows'un kendi maksimize üstü Snap Flyout menüsünün Snappy Layout ile aynı anda çıkması engellendi.

---

## [v0.2] — 16.11.2026
- Açık (Light) temada buton, menü, açılır kutu ve metin görünürlüğü/kontrastı düzeltildi.

---

## [v0.1] — 16.11.2026
- İlk sürüm: özelleştirilebilir yerleşim düzenleri, sanal monitörler, çalışma alanları, kısayollar, TR/EN dil, açık/koyu tema, Per-Monitor V2 DPI desteği.
