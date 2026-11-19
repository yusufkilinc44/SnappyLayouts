# Contributing to Snappy Layout

Thank you for your interest in contributing to **Snappy Layout**!

## Development Setup
1. Install **.NET 10.0 SDK** on Windows 10 or Windows 11.
2. Clone the repository:
   ```powershell
   git clone https://github.com/yusufkilinc44/SnappyLayouts.git
   cd SnappyLayouts
   ```
3. Build the solution:
   ```powershell
   dotnet build "Snappy Layout.sln" -c Release
   ```

## Coding & Versioning Guidelines
- **Versioning Policy:** Every functional change increments the version by `0.1` (`v0.11` → `v0.12`, etc.). Keep the version synchronized across:
  - `src/Snappy Layout/AppInfo.cs`
  - `src/Snappy Layout/Snappy Layout.csproj`
  - `README.md`
  - `CHANGELOG.md`
- **Localization:** Every new user-facing string must be added to both the **English (`En`)** and **Turkish (`Tr`)** dictionaries in `src/Snappy Layout/Services/Loc.cs`.
- **Multi-DPI Testing:** Any window positioning or overlay change must account for mixed-DPI monitor setups (`PerMonitorV2` + `WM_DPICHANGED`).
- **Clean Builds:** Ensure both `Release` and `Debug` configurations build with zero errors and zero warnings before opening a Pull Request.
