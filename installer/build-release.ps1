param(
    [string]$Version = "1.11"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Proj = Join-Path $Root "src\Snappy Layouts\Snappy Layouts.csproj"
$PublishDir = Join-Path $Root "publish\win-x64"
$DistDir = Join-Path $Root "dist"
$IssFile = Join-Path $PSScriptRoot "SnappyLayouts.iss"

Write-Host "=== Snappy Layouts v$Version Release Builder ===" -ForegroundColor Cyan

# Clean previous publish/dist outputs
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# 1) Publish standard self-contained win-x64 files (unpacked PE binaries avoid AV single-file packer false positives)
Write-Host "`n[1/3] Publishing self-contained win-x64 binaries..." -ForegroundColor Yellow
dotnet publish $Proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed!" }

# 2) Build Portable ZIP
Write-Host "`n[2/3] Creating Portable ZIP..." -ForegroundColor Yellow
$PortableZip = Join-Path $DistDir "SnappyLayouts-v$Version-Portable-win-x64.zip"
if (Test-Path $PortableZip) { Remove-Item $PortableZip -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $PortableZip -CompressionLevel Optimal
Write-Host "Created: $PortableZip" -ForegroundColor Green

# 3) Build Windows Installer (Setup.exe) via Inno Setup 6
Write-Host "`n[3/3] Building Windows Installer (Setup.exe)..." -ForegroundColor Yellow
$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}

if ($iscc) {
    & $iscc "/DAppVersion=$Version" $IssFile
    if ($LASTEXITCODE -ne 0) { throw "ISCC.exe failed!" }
    $SetupExe = Join-Path $DistDir "SnappyLayouts-v$Version-Setup.exe"
    Write-Host "Created: $SetupExe" -ForegroundColor Green
} else {
    Write-Warning "Inno Setup 6 (ISCC.exe) was not found. Install it via: winget install JRSoftware.InnoSetup"
    Write-Warning "Portable ZIP was still created at: $PortableZip"
}

Write-Host "`nDone! Output directory: $DistDir" -ForegroundColor Cyan
