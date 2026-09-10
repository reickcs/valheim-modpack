# Deliberate, on-demand check for a new Valheim DEDICATED SERVER build (the
# base game, not this pack's mods -- see build-client.ps1 for that).
#
# The server is deliberately PINNED, not auto-updating (UPDATE_CRON="" in
# docker-compose.yml) -- same "manual, version-pinned" philosophy this repo
# already uses for mods (see modpack.yaml's header). Steam pushes the client
# and dedicated-server depots independently and not always in lockstep; an
# unattended server auto-update is just as likely to CAUSE a version
# mismatch (server jumps ahead of a player's client) as fix one. Run this
# whenever you hear about an "Incompatible version" / "Network version
# check" error, or whenever you want to check.
#
# Usage:
#   powershell -File scripts\update-server-game.ps1 -RemoteHost gameserver
# (or set $env:REMOTE_HOST once in your PowerShell profile and omit -RemoteHost)

param(
    [string]$RemoteHost = $env:REMOTE_HOST,
    [string]$RemoteValheimDir = $(if ($env:REMOTE_VALHEIM_DIR) { $env:REMOTE_VALHEIM_DIR } else { "~/valheim" })
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }

if (-not $RemoteHost) {
    Write-Host "Pass -RemoteHost <alias-or-ip> (or set `$env:REMOTE_HOST once), e.g.:" -ForegroundColor Red
    Write-Host "  powershell -File scripts\update-server-game.ps1 -RemoteHost gameserver"
    exit 1
}

function Get-BuildId {
    $out = ssh -n $RemoteHost "find $RemoteValheimDir/data/server -iname 'appmanifest_896660*' -exec grep buildid {} \;"
    if ($out -match '\d+') { return $Matches[0] }
    return $null
}

Write-Step "Current pinned build"
$before = Get-BuildId
Write-Ok $before

Write-Step "Checking Steam for a newer dedicated-server build (this always re-verifies ~2GB even with no change -- that's normal)"
ssh -n $RemoteHost "docker exec -u valheim valheim-server bash -c 'cd /opt/steamcmd && ./steamcmd.sh +force_install_dir /opt/valheim/server +login anonymous +app_update 896660 validate +quit'" | Select-Object -Last 5

$after = Get-BuildId

if ($before -eq $after) {
    Write-Host ""
    Write-Host "=== No update available (still $after) -- nothing to do ===" -ForegroundColor Green
    exit 0
}

Write-Step "New build available: $before -> $after"
Write-Step "Syncing the BepInEx-side copy (two separate live copies when BEPINEX=true -- see SETUP.md's two-install-paths gotcha)"
ssh -n $RemoteHost "rsync -a --delete --exclude 'BepInEx/' --exclude 'doorstop_libs/' --exclude 'doorstop_config.ini' --exclude 'winhttp.dll' --exclude 'start_server_bepinex.sh' --exclude 'start_game_bepinex.sh' --exclude '.doorstop_version' --exclude 'changelog.txt' $RemoteValheimDir/data/server/ $RemoteValheimDir/data/bepinex/"

Write-Step "Restarting server"
$t0 = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
ssh -n $RemoteHost "cd $RemoteValheimDir && docker compose restart"

Write-Host "    Waiting for it to come back up..."
ssh -n $RemoteHost "cd $RemoteValheimDir && timeout 90 bash -c 'until docker compose logs --since `"$t0`" 2>&1 | grep -qiE `"Game server connected`"; do sleep 3; done' && echo '    READY'"

Write-Step "Checking for new errors since restart"
$errors = ssh -n $RemoteHost "cd $RemoteValheimDir && docker compose logs --since `"$t0`" 2>&1 | grep -iE '\[Error|Exception' | grep -viE 'ArgumentNullException: Value cannot be null'"
if ([string]::IsNullOrWhiteSpace($errors)) {
    Write-Ok "(none)"
} else {
    Write-Host $errors
    Write-Host ""
    Write-Host "!! Errors found above -- review before trusting this update." -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Server updated to build $after ===" -ForegroundColor Green
Write-Host "Players need to be on a matching client build before reconnecting -- if"
Write-Host "anyone's client auto-updated ahead of this, they were already fine;"
Write-Host "if the server just moved ahead of someone's client, they'll see the"
Write-Host "same 'Network version check' mismatch until Steam updates their end."
