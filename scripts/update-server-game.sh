#!/usr/bin/env bash
# Deliberate, on-demand check for a new Valheim DEDICATED SERVER build (the
# base game, not this pack's mods -- see scripts/build-server.sh for that).
#
# The server is deliberately PINNED, not auto-updating (UPDATE_CRON="" in
# docker-compose.yml) -- same "manual, version-pinned" philosophy this repo
# already uses for mods (see modpack.yaml's header). Steam pushes the client
# and dedicated-server depots independently and not always in lockstep; an
# unattended server auto-update is just as likely to CAUSE a version
# mismatch (server jumps ahead of a player's client) as fix one. Run this
# script by hand when you hear about an "Incompatible version" / "Network
# version check" error, or whenever you want to check.
#
# Usage:
#   REMOTE_HOST=your-gameserver-ssh-alias ./scripts/update-server-game.sh
set -euo pipefail

REMOTE_HOST="${REMOTE_HOST:-your-gameserver-ssh-alias-or-host}"
REMOTE_VALHEIM_DIR="${REMOTE_VALHEIM_DIR:-~/valheim}"

if [ "$REMOTE_HOST" = "your-gameserver-ssh-alias-or-host" ]; then
  echo "Set REMOTE_HOST first, e.g.: REMOTE_HOST=gameserver $0" >&2
  exit 1
fi

get_buildid() {
  ssh -n "$REMOTE_HOST" "find $REMOTE_VALHEIM_DIR/data/server -iname 'appmanifest_896660*' -exec grep buildid {} \;" \
    | grep -o '[0-9]\+' | head -1
}

echo "==> Current pinned build"
BEFORE=$(get_buildid)
echo "    $BEFORE"

echo "==> Checking Steam for a newer dedicated-server build (this always re-verifies ~2GB even with no change -- that's normal)"
ssh -n "$REMOTE_HOST" "docker exec -u valheim valheim-server bash -c 'cd /opt/steamcmd && ./steamcmd.sh +force_install_dir /opt/valheim/server +login anonymous +app_update 896660 validate +quit'" | tail -5

AFTER=$(get_buildid)

if [ "$BEFORE" = "$AFTER" ]; then
  echo ""
  echo "=== No update available (still $AFTER) -- nothing to do ==="
  exit 0
fi

echo ""
echo "==> New build available: $BEFORE -> $AFTER"
echo "==> Syncing the BepInEx-side copy (two separate live copies when BEPINEX=true -- see SETUP.md's two-install-paths gotcha)"
ssh -n "$REMOTE_HOST" "rsync -a --delete --exclude 'BepInEx/' --exclude 'doorstop_libs/' \
  --exclude 'doorstop_config.ini' --exclude 'winhttp.dll' \
  --exclude 'start_server_bepinex.sh' --exclude 'start_game_bepinex.sh' \
  --exclude '.doorstop_version' --exclude 'changelog.txt' \
  $REMOTE_VALHEIM_DIR/data/server/ $REMOTE_VALHEIM_DIR/data/bepinex/"

echo "==> Restarting server"
T0=$(date -u +%Y-%m-%dT%H:%M:%SZ)
ssh -n "$REMOTE_HOST" "cd $REMOTE_VALHEIM_DIR && docker compose restart"

echo "    Waiting for it to come back up..."
ssh -n "$REMOTE_HOST" "cd $REMOTE_VALHEIM_DIR && timeout 90 bash -c 'until docker compose logs --since \"$T0\" 2>&1 | grep -qiE \"Game server connected\"; do sleep 3; done' && echo '    READY'"

echo ""
echo "==> Checking for new errors since restart"
ERRORS=$(ssh -n "$REMOTE_HOST" "cd $REMOTE_VALHEIM_DIR && docker compose logs --since \"$T0\" 2>&1 | grep -iE '\\[Error|Exception' | grep -viE 'ArgumentNullException: Value cannot be null'" || true)
if [ -z "$ERRORS" ]; then
  echo "    (none)"
else
  echo "$ERRORS"
  echo ""
  echo "!! Errors found above -- review before trusting this update." >&2
fi

echo ""
echo "=== Server updated to build $AFTER ==="
echo "Players need to be on a matching client build before reconnecting -- if"
echo "anyone's client auto-updated ahead of this, they were already fine;"
echo "if the server just moved ahead of someone's client, they'll see the"
echo "same 'Network version check' mismatch until Steam updates their end."
