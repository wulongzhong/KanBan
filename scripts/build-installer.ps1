# Builds a self-contained Windows release and packages it as an installable Setup.exe.
# Requires: .NET 9 SDK. Installs vpk global tool if missing.
param(
    [string]$Version = "1.0.0",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$Msi
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $Root "dist\publish"
$ReleaseDir = Join-Path $Root "dist\release"

Write-Host "Publishing KanBan ($Runtime, v$Version)..." -ForegroundColor Cyan
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

dotnet publish (Join-Path $Root "KanBan.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained `
    -p:PublishReadyToRun=true `
    -o $PublishDir

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "Installing vpk tool..." -ForegroundColor Yellow
    dotnet tool install -g vpk --version 0.0.1298
}

if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null

$packArgs = @(
    "pack",
    "--packId", "KanBan",
    "--packVersion", $Version,
    "--packDir", $PublishDir,
    "--mainExe", "KanBan.exe",
    "--outputDir", $ReleaseDir
)
if ($Msi) {
    $packArgs += "--msi"
}

Write-Host "Creating installer..." -ForegroundColor Cyan
& vpk @packArgs

Write-Host ""
Write-Host "Done. Installer output:" -ForegroundColor Green
Get-ChildItem $ReleaseDir -File | ForEach-Object { Write-Host "  $($_.FullName)" }
