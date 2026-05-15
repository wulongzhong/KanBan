# Builds KanBan MSI with standard install-directory wizard (WiX WixUI_InstallDir).
# Requires: .NET 9 SDK, WiX Toolset 6 (dotnet tool install -g wix)
param(
    [string]$Version = "1.0.0",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $Root "dist\publish"
$InstallerDir = Join-Path $Root "dist\installer"
$WixProj = Join-Path $Root "installer\KanBan.Installer.wixproj"

# MSI requires x.x.x.x
$MsiVersion = if ($Version -match '^\d+\.\d+\.\d+\.\d+$') { $Version } else { "$Version.0" }

Write-Host "Publishing KanBan ($Runtime, v$Version)..." -ForegroundColor Cyan
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

dotnet publish (Join-Path $Root "KanBan.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained `
    -o $PublishDir

$publishMb = [math]::Round((Get-ChildItem $PublishDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 2)
Write-Host "  Published app: $publishMb MB (trimmed, no PDBs)" -ForegroundColor DarkGray

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Host "Installing WiX Toolset 6..." -ForegroundColor Yellow
    dotnet tool install -g wix --version 6.0.2
    wix extension add -g WixToolset.UI.wixext/6.0.2
}

if (Test-Path $InstallerDir) {
    Remove-Item $InstallerDir -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null

Write-Host "Building MSI..." -ForegroundColor Cyan
dotnet build $WixProj -c Release `
    -p:ProductVersion=$MsiVersion `
    -p:BindPath=$PublishDir `
    -p:HarvestDirectory=$PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed."
}

$msi = Get-ChildItem -Path (Join-Path $Root "installer\bin\x64\Release") -Filter "*.msi" -Recurse |
    Select-Object -First 1
if (-not $msi) {
    throw "MSI was not produced. Check installer build output."
}

Copy-Item $msi.FullName (Join-Path $InstallerDir "KanBan.msi") -Force

$sizeMb = [math]::Round((Get-Item (Join-Path $InstallerDir "KanBan.msi")).Length / 1MB, 2)
Write-Host ""
Write-Host "Done:" -ForegroundColor Green
Write-Host "  $(Join-Path $InstallerDir 'KanBan.msi')  ($sizeMb MB)" -ForegroundColor Cyan
Write-Host "  Finish page: desktop shortcut + launch app (both checked by default)." -ForegroundColor DarkGray
