# Client build: compiles all four mods (the two server-side ones plus the
# two client-only ones) and bundles them into windows-installer/, then
# rebuilds ValheimModpack.zip. No server access needed -- this is the
# complete, self-contained "give this to a player" artifact.
#
# Usage (from the repo root, or anywhere):
#   pwsh scripts/build-client.ps1
#   powershell -File scripts\build-client.ps1

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Dotnet = "dotnet"
if (-not (Get-Command $Dotnet -ErrorAction SilentlyContinue)) {
    $fallback = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path $fallback) { $Dotnet = $fallback }
    else {
        Write-Host "dotnet not found on PATH and not at the default install location. Install the .NET SDK first." -ForegroundColor Red
        exit 1
    }
}

# name = source project folder; plugin = destination folder name under
# windows-installer/plugins/ (must match the BepInEx.Plugin GUID's
# namespace-name convention other tooling in this repo already uses).
$Projects = @(
    @{ name = "ValheimQoL-source";           csproj = "ValheimQoL.csproj";               plugin = "richard-ValheimQoL" }
    @{ name = "AzuCraftyBoxes-source";        csproj = "AzuCraftyBoxes.csproj";           plugin = "Azumatt-AzuCraftyBoxes" }
    @{ name = "PlantEasily-source";           csproj = "Advize_PlantEasily.csproj";       plugin = "Advize-PlantEasily" }
    @{ name = "ConfigurationManager-source";  csproj = "ConfigurationManager.csproj";     plugin = "shudnal-ConfigurationManager" }
)

function Write-Step($msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }

foreach ($p in $Projects) {
    $projDir = Join-Path $RepoRoot $p.name
    $csprojPath = Join-Path $projDir $p.csproj

    Write-Step "Building $($p.name)"
    if (-not (Test-Path $csprojPath)) {
        Write-Host "    $csprojPath not found -- skipping." -ForegroundColor Yellow
        continue
    }

    & $Dotnet build $csprojPath -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build failed for $($p.name) -- stopping. Fix the error above and re-run." -ForegroundColor Red
        exit 1
    }

    $binDir = Join-Path $projDir "bin\Release"
    $destDir = Join-Path $RepoRoot "windows-installer\plugins\$($p.plugin)"
    if (Test-Path $destDir) { Remove-Item $destDir -Recurse -Force }
    New-Item -ItemType Directory -Path $destDir | Out-Null

    Get-ChildItem $binDir -Filter "*.dll" | ForEach-Object {
        Copy-Item $_.FullName $destDir -Force
    }
    Write-Ok "$($p.plugin) <- $((Get-ChildItem $destDir -Filter '*.dll' | ForEach-Object { $_.Name }) -join ', ')"
}

Write-Step "Rebuilding ValheimModpack.zip"
$zipPath = Join-Path $RepoRoot "ValheimModpack.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $RepoRoot "windows-installer\*") -DestinationPath $zipPath
Write-Ok "ValheimModpack.zip ($((Get-Item $zipPath).Length) bytes)"

Write-Host ""
Write-Host "=== Client build complete ===" -ForegroundColor Green
Write-Host "Hand out ValheimModpack.zip -- see windows-installer/README.txt for what a player does with it."
