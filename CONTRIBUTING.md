# Contributing to Snappy Layouts

Thank you for your interest in contributing to **Snappy Layouts**!

## Development Setup
1. Install **.NET 10.0 SDK** on Windows 10 or Windows 11.
2. Clone the repository:
   ```powershell
   git clone https://github.com/yusufkilinc44/SnappyLayouts.git
   cd SnappyLayouts
   ```
3. Build the solution:
   ```powershell
   dotnet build "Snappy Layouts.sln" -c Release
   ```

## Coding & Versioning Guidelines
- **Versioning Policy:** Every functional change increments the version by `0.1` (`v0.11` → `v0.12`, etc.). Keep the version synchronized across:
  - `src/Snappy Layouts/AppInfo.cs`
  - `src/Snappy Layouts/Snappy Layouts.csproj`
  - `README.md`
  - `CHANGELOG.md`
- **Localization:** Every new user-facing string must be added to both the **English (`En`)** and **Turkish (`Tr`)** dictionaries in `src/Snappy Layouts/Services/Loc.cs`.
- **Multi-DPI Testing:** Any window positioning or overlay change must account for mixed-DPI monitor setups (`PerMonitorV2` + `WM_DPICHANGED`).
- **Clean Builds:** Ensure both `Release` and `Debug` configurations build with zero errors and zero warnings before opening a Pull Request.
