# Snappy Layout — Teknik Mimari (Technical Architecture)

**Yazar / Author:** **YUSUF KILINÇ — Kasım 2026**

Bu belge, **Snappy Layout** uygulamasının alt seviye Win32, DWM, Shcore, COM ve WPF mimarisini ayrıntılı olarak açıklar.

---

## 1. Windows 11 Orijinal Snap Flyout Engelleme Mekanizması (`NativeSnapSuppressor`)

Windows 11'de bir pencerenin maksimize (`HTMAXBUTTON`) düğmesi üzerine gelindiğinde işletim sistemi `explorer.exe` üzerinden `XamlExplorerHostIslandWindow` sınıfına sahip yerleşik Snap Flyout penceresini açar. Yalnızca `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` altındaki `EnableSnapAssistFlyout` ve `EnableSnapBar` değerlerini `0` yapmak, `explorer.exe` bu değerleri başlangıçta bellekteki `CSettingsCache` içinde önbelleğe aldığı için tek başına yeterli değildir.

Snappy Layout bu sorunu **4 katmanlı** bir mimariyle çözer:

1. **ImmersiveShell `ISettingsCache` COM Yenilemesi:**
   - `CLSID_ImmersiveShell` (`{C2F03A33-21F5-47FA-B4BB-156362A2F239}`) üzerinden `IServiceProvider::QueryService` çağrılarak `SID_SettingsCache` (`{86FB57A6-3C81-4F40-A971-6280BE4E526E}` ve `{E78295EF-4439-494F-9D40-16E91A4CBF03}`) ve `IID_ISettingsCache` (`{B1BED1AE-63DA-4687-8063-C89EC276D28A}`) arayüzü alınır.
   - Sanal tablo (vtable) 3. sıradaki `RefreshSetting(int settingId)` metodu `11` (`EnableSnapAssistFlyout`) ve `12` (`EnableSnapBar`) parametreleriyle çağrılarak `explorer.exe` belleğindeki önbellek anında güncellenir.
2. **`SettingsCacheChangeMessage` Yayımı:**
   - `RegisterWindowMessage("SettingsCacheChangeMessage")` ve `WM_SETTINGCHANGE` mesajları `SendNotifyMessage(HWND_BROADCAST, ...)` ile tüm kabuk pencerelerine iletilir.
3. **`MainWindowWndProc` `HTMAXBUTTON` Filtrelemesi:**
   - Snappy Layout'un kendi ana yönetim penceresinde (`MainWindow`), `WM_NCHITTEST` mesajı `HTMAXBUTTON` döndürdüğünde ve fare tuşu basılı değilken (`!IsKeyDown(VK_LBUTTON)`), `MaximizeHoverWatcher.NotifySelfMaxHover` tetiklenir ve işletim sistemine `HTBORDER` döndürülerek Windows 11'in ana pencere üzerinde yerleşik Snap Flyout tetiklemesi %100 engellenir.
4. **Sıfır Gecikmeli `EVENT_OBJECT_SHOW` Kancası:**
   - `WindowEventService` içinde `SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW, ...)` kancası çalışır. Maksimize düğmesi üzerinde gezinirken `XamlExplorerHostIslandWindow` veya `Shell_LightDismissOverlay` oluşturulursa DWM tek bir kare bile çizmeden `0ms` içinde ekran dışına taşınıp gizlenir.

---

## 2. Per-Monitor V2 Dinamik DPI ve İki Aşamalı Pencere Yerleştirme (`WindowPlacer`)

Windows 10/11'de pencerelerin `GetWindowRect` ile döndürülen dikdörtgeni, DWM'nin gölge için kullandığı **görünmez kenarlıkları** (7-8px) içerir. Görünen gerçek çerçeve ise `DwmGetWindowAttribute(..., DWMWA_EXTENDED_FRAME_BOUNDS, ...)` ile alınır.

Ayrıca, örneğin `%125` ölçekli bir monitörden `%175` ölçekli bir monitöre pencere taşınırken pencere `WM_DPICHANGED` mesajı alarak kendi çerçeve kalınlığını yeniden ölçeklendirir. `WindowPlacer.Place()` bu durumu **iki geçişli (two-pass)** algoritmayla çözer:

1. **1. Geçiş:** Mevcut DWM çerçeve farkları (`ml, mt, mr, mb`) hesaplanarak pencere hedef monitördeki dikdörtgene `SetWindowPos` ile taşınır.
2. **2. Geçiş:** Hedef monitörün DPI değeri kaynak monitörden farklıysa (veya pencere boyut/konumunda 1px'den fazla sapma varsa), yeni DPI altında `DWMWA_EXTENDED_FRAME_BOUNDS` tekrar ölçülür ve ikinci bir `SetWindowPos` ile sıfır piksel hatayla hedef bölgeye oturtulur.

---

## 3. Çoklu Monitör ve Sanal Monitör İzolasyonu (`MonitorService` & `AreaService`)

- Her fiziksel monitör `QueryDisplayConfig` (CCD API) ile gerçek donanım adıyla (örn. `PHL 345E2`) tanımlanır.
- Bir fiziksel monitör yan yana `N` adet sanal monitöre bölündüğünde, her sanal monitör bağımsız bir `SnapArea` nesnesi üretir (`MonitorKey::vm:SegmentCount:SegmentIndex`).
- Her `SnapArea`, `Storage.Data.AreaLayouts` sözlüğünde **kendine özel pencere yerleşim listesine** sahiptir ve kendi gerçek en/boy oranını (`Aspect = Width / Height`) hesaplar.

---

## 4. Çalışma Alanları ve Tarayıcı URL Yakalama (`WindowCatalog` & `WorkspaceService`)

- Açık pencerelerin süreç yolları `QueryFullProcessImageName` ile, komut satırı argümanları ise `NtQueryInformationProcess` ve süreç belleği (`PEB -> ProcessParameters -> CommandLine`) üzerinden okunur.
- Chromium ve Gecko tabanlı tarayıcılarda (`chrome`, `msedge`, `brave`, `vivaldi`, `opera`, `firefox`) aktif sekmenin URL adresi hem pencere erişilebilirlik ağacından hem de komut satırından tespit edilerek kaydedilir.
