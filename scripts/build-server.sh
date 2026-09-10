#!/usr/bin/env bash
# Server build: compiles ONLY the two mods that actually run server-side
# (ValheimQoL, AzuCraftyBoxes -- PlantEasily and ConfigurationManager are
# client-only, see modpack.yaml, and would just be dead weight here),
# deploys the DLLs to the live server over SSH, restarts it, and verifies
# the boot log came back clean. Needs SSH access to the server; the client
# build (scripts/build-client.ps1) needs none.
#
# Usage:
#   REMOTE_HOST=your-gameserver-ssh-alias-or-host ./scripts/build-server.sh
# or set REMOTE_HOST/REMOTE_VALHEIM_DIR permanently in your shell profile.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REMOTE_HOST="${REMOTE_HOST:-your-gameserver-ssh-alias-or-host}"
REMOTE_VALHEIM_DIR="${REMOTE_VALHEIM_DIR:-~/valheim}"

DOTNET="dotnet"
if ! command -v dotnet >/dev/null 2>&1; then
  if [ -f "/c/Program Files/dotnet/dotnet.exe" ]; then
    DOTNET="/c/Program Files/dotnet/dotnet.exe"
  else
    echo "dotnet not found on PATH and not at the default install location. Install the .NET SDK first." >&2
    exit 1
  fi
fi

if [ "$REMOTE_HOST" = "your-gameserver-ssh-alias-or-host" ]; then
  echo "Set REMOTE_HOST first, e.g.: REMOTE_HOST=gameserver $0" >&2
  exit 1
fi

# name = source project folder; csproj = its project file; plugin =
# destination folder name under BepInEx/plugins/ on the server (must match
# the BepInEx.Plugin GUID's namespace-name convention).
declare -a NAMES=("ValheimQoL-source" "AzuCraftyBoxes-source")
declare -a CSPROJS=("ValheimQoL.csproj" "AzuCraftyBoxes.csproj")
declare -a PLUGINS=("richard-ValheimQoL" "Azumatt-AzuCraftyBoxes")

for i in "${!NAMES[@]}"; do
  name="${NAMES[$i]}"
  csproj="${CSPROJS[$i]}"
  plugin="${PLUGINS[$i]}"
  proj_dir="$REPO_ROOT/$name"

  echo "==> Building $name"
  if [ ! -f "$proj_dir/$csproj" ]; then
    echo "    $proj_dir/$csproj not found -- skipping."
    continue
  fi
  "$DOTNET" build "$proj_dir/$csproj" -c Release

  echo "==> Deploying $plugin to $REMOTE_HOST (both live copies -- see SETUP.md's two-install-paths gotcha)"
  for dll in "$proj_dir"/bin/Release/*.dll; do
    fname="$(basename "$dll")"
    ssh -n "$REMOTE_HOST" "mkdir -p $REMOTE_VALHEIM_DIR/config/bepinex/plugins/$plugin $REMOTE_VALHEIM_DIR/data/bepinex/BepInEx/plugins/$plugin"
    scp -q "$dll" "$REMOTE_HOST:$REMOTE_VALHEIM_DIR/config/bepinex/plugins/$plugin/$fname"
    scp -q "$dll" "$REMOTE_HOST:$REMOTE_VALHEIM_DIR/data/bepinex/BepInEx/plugins/$plugin/$fname"
    echo "    $fname"
  done
done

echo ""
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
  echo "!! Errors found above -- review before trusting this deploy." >&2
fi

echo ""
echo "=== Server build & deploy complete ==="
