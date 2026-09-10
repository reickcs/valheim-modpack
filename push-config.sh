#!/usr/bin/env bash
# Push locally-tuned BepInEx .cfg file(s) up to the gameserver and restart it
# to apply them. Deliberately manual/reviewed, not automatic — one player's
# local experimentation shouldn't silently overwrite the shared server.
#
# Usage:
#   ./push-config.sh richard.valheimqol.cfg [more.cfg ...]
#   ./push-config.sh --all
set -euo pipefail

# Edit these three for your own setup.
LOCAL_CFG_DIR="${LOCAL_CFG_DIR:-/path/to/Valheim/BepInEx/config}"
REMOTE_HOST="${REMOTE_HOST:-your-gameserver-ssh-alias-or-host}"
REMOTE_CFG_DIR="${REMOTE_CFG_DIR:-~/valheim/config/bepinex}"

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <file.cfg> [file2.cfg ...]  |  $0 --all"
  exit 1
fi

if [ "$1" = "--all" ]; then
  mapfile -t FILES < <(cd "$LOCAL_CFG_DIR" && ls *.cfg)
else
  FILES=("$@")
fi

for f in "${FILES[@]}"; do
  local_path="$LOCAL_CFG_DIR/$f"
  if [ ! -f "$local_path" ]; then
    echo "!! skipping $f — not found at $local_path"
    continue
  fi

  echo "=== $f ==="
  ts=$(date -u +%Y%m%dT%H%M%SZ)
  ssh -n "$REMOTE_HOST" "test -f $REMOTE_CFG_DIR/$f && cp $REMOTE_CFG_DIR/$f $REMOTE_CFG_DIR/$f.bak.$ts || echo '  (no existing remote copy to back up)'"

  echo "  --- diff (remote current -> local, what's about to change) ---"
  ssh -n "$REMOTE_HOST" "cat $REMOTE_CFG_DIR/$f 2>/dev/null" | diff -u - "$local_path" || true

  scp -q "$local_path" "$REMOTE_HOST:$REMOTE_CFG_DIR/$f"
  echo "  pushed."
done

echo
read -p "Restart the server now to apply? [y/N] " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
  echo "Not restarting — changes are on disk but won't take effect until next restart."
  exit 0
fi

T0=$(date -u +%Y-%m-%dT%H:%M:%SZ)
ssh -n "$REMOTE_HOST" 'cd ~/valheim && docker compose restart'
echo "Waiting for server to come back up..."
ssh -n "$REMOTE_HOST" "cd ~/valheim && timeout 90 bash -c 'until docker compose logs --since \"$T0\" 2>&1 | grep -qiE \"Game server connected\"; do sleep 3; done' && echo READY"
echo "--- any errors since restart ---"
ssh -n "$REMOTE_HOST" "cd ~/valheim && docker compose logs --since \"$T0\" 2>&1 | grep -iE '\\[Error|Exception' | grep -viE 'EpicLoot_UnityLib|ThrowArgumentNullException|AzuCraftyBoxes.Compatibility|ArgumentNullException: Value cannot be null'" || echo "(none)"
