# Setup

## Two build targets — read this first

This repo has two separate things you might build, and they're not the
same:

| | Client build | Server build |
|---|---|---|
| Script | [`scripts/build-client.ps1`](scripts/build-client.ps1) | [`scripts/build-server.sh`](scripts/build-server.sh) |
| Builds | All four mods (ValheimQoL, AzuCraftyBoxes, PlantEasily, ConfigurationManager) | Only the two that run server-side (ValheimQoL, AzuCraftyBoxes) |
| Output | `ValheimModpack.zip` — self-contained, hand it to a player | Deployed straight to the live server over SSH, then restarted |
| Needs | Nothing but the .NET SDK and `libs/` populated (below) | SSH access to the server too |

Building on your own PC does **not** touch the server, and building
against the server does **not** produce a client installer — run the
one that matches what you're actually trying to do. `PlantEasily` and
`ConfigurationManager` are client-only and never go near the server at
all (see `modpack.yaml`'s `client_only_mods` — a mod that logs "This
mod is client-side only and is not needed on a dedicated server" belongs
there, it'd be dead weight on a headless server).

## 0. Prerequisites (both build targets)

The .NET 8 SDK (or newer), and reference DLLs this repo intentionally
doesn't ship in each `<project>-source/libs/` — see
[`ValheimQoL-source/libs/README.md`](ValheimQoL-source/libs/README.md)
for the base set and where to get them (your own Valheim/dedicated-server
install, plus BepInExPack's `core/` folder). `AzuCraftyBoxes-source/`,
`PlantEasily-source/`, and `ConfigurationManager-source/` additionally
need **publicized** copies of `assembly_valheim.dll`/`assembly_utils.dll`/
`assembly_guiutils.dll` (all private/internal members made public — they
reach into game internals more directly than ValheimQoL's Harmony/
AccessTools-only approach):
```bash
dotnet tool install -g BepInEx.AssemblyPublicizer.Cli
assembly-publicizer assembly_valheim.dll assembly_utils.dll assembly_guiutils.dll -o <project>/libs -f
```
(On Windows, if you hit "You must install or update .NET to run this
application" even with a newer SDK installed, set
`$env:DOTNET_ROLL_FORWARD = "LatestMajor"` first — the tool pins an
exact old runtime version by default.) Each project's own
`<project>-source/libs/README.md` lists anything extra it needs beyond
the base set.

## 1. Client build

```powershell
powershell -File scripts\build-client.ps1
```
Builds all four projects, copies each one's output DLLs into
`windows-installer/plugins/<namespace-name>/`, and rebuilds
`ValheimModpack.zip`. Hand that zip to a player — see
[`windows-installer/README.txt`](windows-installer/README.txt) for what
they do with it, or [CLIENT-INSTALL.md](CLIENT-INSTALL.md) for the
manual/Linux path.

**Publishing it** (what the README's download link points at) is a
GitHub Release, not the committed copy in the repo tree — a Release
gives players a clean, single-file download page instead of having to
browse source code to find the zip:
```bash
gh release create v2 ValheimModpack.zip --title "ValheimModpack v2" --notes "..."
```
Bump the tag each time (`v2`, `v3`, ...) — `releases/latest/download/...`
(what the README links to) always resolves to whichever release was
published most recently, no link to update.

## 2. Server setup + build

First time, bring the server stack up:
```bash
git clone <this-repo> valheim && cd valheim
cp .env.example .env
# edit .env: SERVER_NAME, WORLD_NAME, SERVER_PASS at minimum, BEPINEX=true
docker compose up -d
```
That pulls the vanilla dedicated server. Then, every time after (first
deploy included):
```bash
REMOTE_HOST=your-gameserver-ssh-alias ./scripts/build-server.sh
```
Builds `ValheimQoL` and `AzuCraftyBoxes`, `scp`s each one's DLLs to
**both** live copies on the server (see the two-install-paths gotcha
below — this genuinely matters, it's not redundant), restarts the
container, waits for `Game server connected` in the log, and greps for
new errors. `REMOTE_VALHEIM_DIR` defaults to `~/valheim`; override it
the same way if yours lives elsewhere.

### Gotchas we actually hit running this

- **The image's own SteamCMD auto-updater can silently no-op.** If you
  see `Error! App '896660' state is 0x6 after update job` followed by
  `Failed to update... using existing local files` in the logs, the
  update did not actually happen — it fell back to whatever was already
  installed. Force it manually:
  ```bash
  docker exec -u valheim valheim-server bash -c \
    'cd /opt/steamcmd && ./steamcmd.sh +force_install_dir /opt/valheim/server +login anonymous +app_update 896660 validate +quit'
  docker compose restart
  ```
- **With `BEPINEX=true`, there are *two separate* game-file copies** —
  one at `data/server/` (plain), one at `data/bepinex/` (the modded
  boot path). Updating one does **not** update the other. If you hit
  the SteamCMD flake above while running modded, `rsync` the fix across
  (excluding BepInEx's own files):
  ```bash
  rsync -a --delete --exclude 'BepInEx/' --exclude 'doorstop_libs/' \
    --exclude 'doorstop_config.ini' --exclude 'winhttp.dll' \
    --exclude 'start_server_bepinex.sh' --exclude 'start_game_bepinex.sh' \
    --exclude '.doorstop_version' --exclude 'changelog.txt' \
    data/server/ data/bepinex/
  ```
- **`config/bepinex/plugins/` doesn't always sync into the live BepInEx
  install on its own.** If a freshly-started container logs `0 plugins
  to load` right after `Chainloader startup complete`, copy plugins in
  directly and restart once more:
  ```bash
  cp -r config/bepinex/plugins/* data/bepinex/BepInEx/plugins/
  docker compose restart
  ```
- **Dropping a mod from `modpack.yaml` doesn't remove its old files.**
  `rm -rf` its folder from both `config/bepinex/plugins/` and
  `data/bepinex/BepInEx/plugins/` before restarting, or the stale copy
  keeps loading. (`scripts/build-server.sh` doesn't do this for you —
  it only ever adds/overwrites the two folders it knows about.)

## 3. Adding a new patch, or fixing a Valheim-update break

Decompile the target class first —
[`ilspycmd`](https://github.com/icsharpcode/ILSpy) (`dotnet tool install
-g ilspycmd`) against your own copy of `assembly_valheim.dll` — and
confirm the field/method actually exists and is named what you think
before writing code against it. Valheim's internal API isn't stable
enough between patches to guess from memory or from an older version's
source; every fix already made across these four projects (see each
mod's `build_fixes`/`stripped` entry in `modpack.yaml`) was found this
way, not guessed. `ilspycmd -t ClassName assembly_valheim.dll`
decompiles one class; `-l c` lists every type in the assembly. Once the
patch is written, rebuild with whichever of parts 1/2 matches where it
needs to go.

## 4. Tuning settings live

Once `ValheimQoL` is deployed, its config auto-syncs to every connecting
client (see the README). To change a setting:

```bash
ssh <server> 'vi ~/valheim/config/bepinex/richard.valheimqol.cfg'
ssh <server> 'cd ~/valheim && docker compose restart'
```

or use `push-config.sh` to push a locally-tuned copy up (with a diff
preview and automatic backup of whatever it's about to overwrite).
Either way, a restart is required — every `ValheimQoL` patch reads its
config once at `Awake()`, not live.
