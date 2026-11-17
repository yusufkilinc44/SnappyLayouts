# Sürüm Geçmişi / Changelog

Bu projedeki tüm önemli değişiklikler bu dosyada belgelenmektedir.  
**Sürüm Politikası:** Her değişiklikte sürüm numarası `0.1` artırılır (`v0.1` → `v0.2` → ...), canlıya geçişte `v1.0` yapılır.

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
