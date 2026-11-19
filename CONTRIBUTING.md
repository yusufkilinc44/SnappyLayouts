# Katkıda Bulunma Rehberi (Contributing Guide)

**Snappy Layout** projesine ilgi gösterdiğiniz için teşekkür ederiz!

## Geliştirme Ortamı
1. Windows 10 veya Windows 11 üzerinde **.NET 10.0 SDK** kurulu olmalıdır.
2. Depoyu klonlayın:
   ```powershell
   git clone https://github.com/yusufkilinc44/SnappyLayouts.git
   cd SnappyLayouts
   ```
3. Projeyi derleyin:
   ```powershell
   dotnet build "Snappy Layout.sln" -c Release
   ```

## Sürüm ve Değişiklik Politikası
- Her kod değişikliğinde sürüm numarası `0.1` artırılır (`v0.8` → `v0.9` vb.).
- Sürüm numarası hem `src/Snappy Layout/AppInfo.cs` hem `src/Snappy Layout/Snappy Layout.csproj` hem de `CHANGELOG.md` ve `README.md` dosyalarında senkronize tutulmalıdır.
- Her yeni özellik veya arayüz metni `src/Snappy Layout/Services/Loc.cs` içinde hem **Türkçe (`Tr`)** hem **İngilizce (`En`)** sözlüğe eklenmelidir.
- Her kod ya da proje dosyası değişikliğinde GitHub deposu güncel tutulmalıdır.
