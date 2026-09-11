# Client-side installer for this modpack. Finds your Valheim install,
# installs BepInEx, and copies in the plugins bundled in this folder's
# plugins\ subdirectory. Safe to re-run if something goes wrong partway
# through.
#
# Every plugin here is built from source in this repo (see ../SETUP.md) --
# nothing is downloaded from Thunderstore except BepInEx itself, which is
# infrastructure (the mod loader), not one of this pack's mods.
# Server admins: after any source change, rebuild the plugin(s), re-copy
# the DLL(s) into plugins\<namespace-name>\, and rebuild ValheimModpack.zip
# -- see ../SETUP.md part 4.

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ApiBase = "https://thunderstore.io/api/experimental/package"
$Headers = @{ "User-Agent" = "curl/8.5.0" }

$BepInExPack = @{ ns = "denikson"; name = "BepInExPack_Valheim"; ver = "5.4.2333" }

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
    Write-Host "Valheim is currently running. Close it first, then re-run this installer." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

$PluginsDir = Join-Path $ValheimDir "BepInEx\plugins"
$TempDir = Join-Path $env:TEMP "valheim-modpack-install"
if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempDir | Out-Null

function Get-PackageMeta($ns, $name, $ver) {
    $url = "$ApiBase/$ns/$name/$ver/"
    return Invoke-RestMethod -Uri $url -Headers $Headers -TimeoutSec 30
}

function Install-Package($ns, $name, $ver, $destRoot) {
    Write-Host "    installing $ns-$name-$ver"
    $meta = Get-PackageMeta $ns $name $ver
    $zipPath = Join-Path $TempDir "$ns-$name-$ver.zip"
    Invoke-WebRequest -Uri $meta.download_url -Headers $Headers -OutFile $zipPath -TimeoutSec 60
    $dest = Join-Path $destRoot "$ns-$name"
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    Expand-Archive -Path $zipPath -DestinationPath $dest -Force
}

Write-Step "Installing BepInEx (mod loader -- not one of this pack's own mods)"
try {
    Install-Package $BepInExPack.ns $BepInExPack.name $BepInExPack.ver $TempDir
    $bepinexExtract = Join-Path $TempDir "$($BepInExPack.ns)-$($BepInExPack.name)\BepInExPack_Valheim"
    Copy-Item (Join-Path $bepinexExtract "winhttp.dll") $ValheimDir -Force
    Copy-Item (Join-Path $bepinexExtract "doorstop_config.ini") $ValheimDir -Force
    $bepinexCore = Join-Path $ValheimDir "BepInEx"

    # BepInEx\config holds every plugin's tuned .cfg (and BepInEx\config\Azumatt.AzuCraftyBoxes.yml) --
    # back it up before the wholesale delete+replace below, then restore it, so re-running this
    # installer (e.g. to pick up a new mod version) doesn't silently reset all your settings back
    # to plugin defaults. Character/world saves are never at risk here regardless -- those live in
    # %userprofile%\AppData\LocalLow\IronGate\Valheim\, which this script never touches.
    $configBackup = Join-Path $TempDir "config-backup"
    $existingConfig = Join-Path $bepinexCore "config"
    if (Test-Path $existingConfig) {
        Copy-Item $existingConfig $configBackup -Recurse -Force
    }

    if (Test-Path $bepinexCore) { Remove-Item $bepinexCore -Recurse -Force }
    Copy-Item (Join-Path $bepinexExtract "BepInEx") $ValheimDir -Recurse -Force

    if (Test-Path $configBackup) {
        Copy-Item $configBackup $existingConfig -Recurse -Force
        Write-Ok "Restored your existing plugin settings (BepInEx\config)"
    }

    Write-Ok "BepInEx installed"
} catch {
    Write-Host ""
    Write-Host "Failed installing BepInEx: $_" -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

Write-Step "Installing this pack's mods (bundled in this folder -- built from source, not downloaded)"
New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
$BundledPluginsDir = Join-Path $ScriptDir "plugins"
if (-not (Test-Path $BundledPluginsDir)) {
    Write-Host ""
    Write-Host "plugins\ folder not found next to this script -- something's missing from the zip." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}
$installedPlugins = @()
Get-ChildItem $BundledPluginsDir -Directory | ForEach-Object {
    $dest = Join-Path $PluginsDir $_.Name
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    Copy-Item $_.FullName $dest -Recurse -Force
    Write-Ok $_.Name
    $installedPlugins += $_.Name
}

Write-Step "Verifying"
$checks = @(
    @{ path = (Join-Path $ValheimDir "winhttp.dll"); label = "BepInEx loader (winhttp.dll)" }
    @{ path = (Join-Path $ValheimDir "BepInEx\core\BepInEx.dll"); label = "BepInEx core" }
    @{ path = (Join-Path $PluginsDir "richard-ValheimQoL\ValheimQoL.dll"); label = "ValheimQoL plugin" }
    @{ path = (Join-Path $PluginsDir "Azumatt-AzuCraftyBoxes\AzuCraftyBoxes.dll"); label = "AzuCraftyBoxes plugin" }
)
$allGood = $true
foreach ($c in $checks) {
    if (Test-Path $c.path) {
        Write-Ok $c.label
    } else {
        Write-Host "    MISSING: $($c.label)" -ForegroundColor Red
        $allGood = $false
    }
}
$pluginCount = (Get-ChildItem $PluginsDir -Directory).Count
Write-Host "    $pluginCount plugin folders present in BepInEx\plugins"

Write-Host ""
if ($allGood) {
    Write-Host "=== Install complete ===" -ForegroundColor Green
} else {
    Write-Host "=== Install finished with problems -- see above ===" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Launch Valheim through Steam normally (no special launch options needed)."
Write-Host "  2. Join Game -> Direct Connect, using the address/password whoever's"
Write-Host "     running the server gave you."
Write-Host ""
Write-Host "  NOTE: if that's a LAN IP (192.168.x.x / 10.x.x.x), you need to be on the"
Write-Host "  same network unless the router has UDP 2456-2457 forwarded."
Write-Host ""
Read-Host "Press Enter to close"
