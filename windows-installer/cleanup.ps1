# Client-side cleanup for this modpack -- removes the four custom mods
# (and their settings) that this pack's own install.ps1 put in place, for
# players moving to a different setup (e.g. Valheim Plus + stock
# AzuCraftyBoxes on a different server). Only ever touches the exact
# plugin folders and config files this pack itself installs -- never
# BepInEx itself, never world saves or characters (those live in
# %userprofile%\AppData\LocalLow\IronGate\Valheim\, untouched here), and
# never any other mod you may have installed separately.
#
# Usage: double-click cleanup.bat, or run this directly.

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    ! $msg" -ForegroundColor Yellow }

function Find-ValheimDir {
    $candidates = New-Object System.Collections.Generic.List[string]

    try {
        $steamPath = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name "SteamPath" -ErrorAction Stop).SteamPath
        $candidates.Add((Join-Path $steamPath "steamapps\common\Valheim"))

        $vdf = Join-Path $steamPath "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            $matches = Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"' -AllMatches
            foreach ($m in $matches.Matches) {
                $libPath = $m.Groups[1].Value -replace '\\\\', '\'
                $candidates.Add((Join-Path $libPath "steamapps\common\Valheim"))
            }
        }
    } catch {
        Write-Warn "Could not read Steam install location from the registry."
    }

    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "valheim.exe")) { return $c }
    }
    return $null
}

Write-Step "Looking for your Valheim install"
$ValheimDir = Find-ValheimDir
if (-not $ValheimDir) {
    Write-Warn "Couldn't auto-detect it."
    $ValheimDir = Read-Host "Enter the full path to your Valheim folder (the one containing valheim.exe)"
    if (-not (Test-Path (Join-Path $ValheimDir "valheim.exe"))) {
        Write-Host ""
        Write-Host "valheim.exe not found in that folder. Stopping -- check the path and re-run." -ForegroundColor Red
        Read-Host "Press Enter to close"
        exit 1
    }
}
Write-Ok "Found Valheim at: $ValheimDir"

if (Get-Process -Name "valheim" -ErrorAction SilentlyContinue) {
    Write-Host ""
    Write-Host "Valheim is currently running. Close it first, then re-run this cleanup." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

$PluginsDir = Join-Path $ValheimDir "BepInEx\plugins"
$ConfigDir  = Join-Path $ValheimDir "BepInEx\config"

# Exact plugin folder names this pack's install.ps1 creates -- see
# windows-installer\plugins\ in the repo for the source of truth.
$Plugins = @(
    "richard-ValheimQoL",
    "Azumatt-AzuCraftyBoxes",
    "Advize-PlantEasily",
    "shudnal-ConfigurationManager"
)

# Exact config files/folders each of those plugins writes under BepInEx\config.
$ConfigFiles = @(
    "richard.valheimqol.cfg",
    "Azumatt.AzuCraftyBoxes.cfg",
    "Azumatt.AzuCraftyBoxes.yml",
    "advize.PlantEasily.cfg",
    "_shudnal.ConfigurationManager.cfg"
)
$ConfigDirs = @(
    "richard.valheimqol.serverprofiles"
)

Write-Step "Removing this pack's plugins"
$removedAny = $false
foreach ($p in $Plugins) {
    $path = Join-Path $PluginsDir $p
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
        Write-Ok "Removed $p"
        $removedAny = $true
    } else {
        Write-Warn "$p not found -- already removed?"
    }
}

Write-Step "Removing this pack's settings"
foreach ($f in $ConfigFiles) {
    $path = Join-Path $ConfigDir $f
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Ok "Removed $f"
    } else {
        Write-Warn "$f not found -- already removed?"
    }
}
foreach ($d in $ConfigDirs) {
    $path = Join-Path $ConfigDir $d
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
        Write-Ok "Removed $d"
    } else {
        Write-Warn "$d not found -- already removed?"
    }
}

Write-Host ""
if ($removedAny) {
    Write-Host "=== Cleanup complete ===" -ForegroundColor Green
} else {
    Write-Host "=== Nothing from this pack was found -- already clean ===" -ForegroundColor Green
}
Write-Host "BepInEx itself, any other mods, your world saves, and your character are all untouched."
Write-Host ""
Read-Host "Press Enter to close"
