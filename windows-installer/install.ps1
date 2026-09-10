# Client-side installer for this modpack. Finds your Valheim install,
# installs BepInEx + the pinned mod list, drops in the custom ValheimQoL
# plugin bundled in this folder, and verifies the result. Safe to re-run
# if something goes wrong partway through.
#
# Server admins: this $Mods list is hand-copied from ../modpack.yaml
# (server mods + client_only_mods combined) because PowerShell has no
# built-in YAML parser. If you change modpack.yaml, update this list to
# match and rebuild ValheimModpack.zip -- see ../SETUP.md.

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ApiBase = "https://thunderstore.io/api/experimental/package"
$Headers = @{ "User-Agent" = "curl/8.5.0" }

$BepInExPack = @{ ns = "denikson"; name = "BepInExPack_Valheim"; ver = "5.4.2333" }

# Exact pinned modpack (mirrors modpack.yaml + its resolved dependencies).
# Kept as flat data here on purpose -- no dependency-resolution logic to
# fail on someone else's machine.
$Mods = @(
    @{ ns = "Azumatt";    name = "AzuCraftyBoxes";                  ver = "1.8.15" }
    @{ ns = "Smoothbrain";name = "ServerCharacters";                 ver = "1.4.16" }
    @{ ns = "Advize";     name = "PlantEasily";                      ver = "2.1.1"  }
    @{ ns = "shudnal";    name = "ConfigurationManager";             ver = "1.1.16" }
    @{ ns = "ValheimModding"; name = "YamlDotNet";                   ver = "16.3.0" }
    @{ ns = "shudnal";    name = "ConditionalConfigSync";            ver = "1.0.4"  }
)

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

Write-Step "Installing BepInEx"
try {
    Install-Package $BepInExPack.ns $BepInExPack.name $BepInExPack.ver $TempDir
    $bepinexExtract = Join-Path $TempDir "$($BepInExPack.ns)-$($BepInExPack.name)\BepInExPack_Valheim"
    Copy-Item (Join-Path $bepinexExtract "winhttp.dll") $ValheimDir -Force
    Copy-Item (Join-Path $bepinexExtract "doorstop_config.ini") $ValheimDir -Force
    $bepinexCore = Join-Path $ValheimDir "BepInEx"
    if (Test-Path $bepinexCore) { Remove-Item $bepinexCore -Recurse -Force }
    Copy-Item (Join-Path $bepinexExtract "BepInEx") $ValheimDir -Recurse -Force
    Write-Ok "BepInEx installed"
} catch {
    Write-Host ""
    Write-Host "Failed installing BepInEx: $_" -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

Write-Step "Installing pinned mods ($($Mods.Count) packages)"
$PluginsDir = Join-Path $ValheimDir "BepInEx\plugins"
New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
$failed = @()
foreach ($mod in $Mods) {
    try {
        Install-Package $mod.ns $mod.name $mod.ver $PluginsDir
    } catch {
        Write-Warn "Failed: $($mod.ns)-$($mod.name)-$($mod.ver) -- $_"
        $failed += "$($mod.ns)-$($mod.name)"
    }
}
if ($failed.Count -eq 0) {
    Write-Ok "All mods installed"
} else {
    Write-Warn "Some mods failed to install: $($failed -join ', ')"
    Write-Warn "Re-run this installer to retry -- it's safe to run again."
}

Write-Step "Installing ValheimQoL (custom plugin, bundled in this folder)"
$QolSource = Join-Path $ScriptDir "ValheimQoL.dll"
if (-not (Test-Path $QolSource)) {
    Write-Host ""
    Write-Host "ValheimQoL.dll not found next to this script -- something's missing from the zip." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}
$QolDest = Join-Path $PluginsDir "richard-ValheimQoL"
New-Item -ItemType Directory -Path $QolDest -Force | Out-Null
Copy-Item $QolSource $QolDest -Force
Write-Ok "ValheimQoL.dll installed"

Write-Step "Verifying"
$checks = @(
    @{ path = (Join-Path $ValheimDir "winhttp.dll"); label = "BepInEx loader (winhttp.dll)" }
    @{ path = (Join-Path $ValheimDir "BepInEx\core\BepInEx.dll"); label = "BepInEx core" }
    @{ path = (Join-Path $QolDest "ValheimQoL.dll"); label = "ValheimQoL plugin" }
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
if ($allGood -and $failed.Count -eq 0) {
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
