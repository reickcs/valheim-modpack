# Setup

## 1. Server

Requires Docker + Docker Compose on the host.

```bash
git clone <this-repo> valheim && cd valheim
cp .env.example .env
# edit .env: SERVER_NAME, WORLD_NAME, SERVER_PASS at minimum
docker compose up -d
```

That pulls the vanilla dedicated server first. Once it's up, install the
modpack:

```bash
python3 install_mods.py
```

This resolves `modpack.yaml`'s dependency graph via the Thunderstore API
and installs into `config/bepinex/plugins/`. Add `ValheimQoL.dll` (build
it yourself, see part 3 below, or grab a pre-built one) into
`config/bepinex/plugins/richard-ValheimQoL/ValheimQoL.dll`, then:

```bash
docker compose restart
```

Set `BEPINEX=true` in `.env` first if you haven't — env var changes need
`docker compose up -d` (recreate), not `restart`, to actually take
effect.

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
  `install_mods.py` says so itself: `rm -rf` its folder from both
  `config/bepinex/plugins/` and `data/bepinex/BepInEx/plugins/` before
  restarting, or the stale copy keeps loading.
- **A mod that logs "This mod is client-side only and is not needed on
  a dedicated server"** belongs in `modpack.yaml`'s `client_only_mods`,
  not `mods` — it's dead weight on the server (see `PlantEasily`'s entry
  for exactly this).

## 2. Clients

See [CLIENT-INSTALL.md](CLIENT-INSTALL.md), or hand out
`windows-installer/ValheimModpack.zip` for the one-click Windows path.

## 3. Building ValheimQoL from source

Needs the .NET 8 SDK (or newer) and reference DLLs this repo
intentionally doesn't ship — see
[`ValheimQoL-source/libs/README.md`](ValheimQoL-source/libs/README.md)
for exactly which files and where to get them (short version: your own
Valheim/dedicated-server install, plus BepInExPack's `core/` folder).

```bash
cd ValheimQoL-source
dotnet build -c Release
# -> bin/Release/ValheimQoL.dll
```

**Adding a new patch:** decompile the target class first —
[`ilspycmd`](https://github.com/icsharpcode/ILSpy) (`dotnet tool install
-g ilspycmd`) against your own copy of `assembly_valheim.dll` — and
confirm the field/method you're about to patch actually exists and is
named what you think. Valheim's internal API isn't stable enough
between patches to guess from memory or from an older version's source.
`ilspycmd -t ClassName assembly_valheim.dll` decompiles one class;
`-l c` lists every type in the assembly.

**Deploying a rebuilt plugin:**
```bash
scp bin/Release/ValheimQoL.dll <server>:~/valheim/config/bepinex/plugins/richard-ValheimQoL/ValheimQoL.dll
scp bin/Release/ValheimQoL.dll <server>:~/valheim/data/bepinex/BepInEx/plugins/richard-ValheimQoL/ValheimQoL.dll
ssh <server> 'cd ~/valheim && docker compose restart'
```
(Both copies — see the two-install-paths gotcha above.) Then watch the
logs for `Game server connected` and grep for exceptions before calling
it done.

## 4. Rebuilding the Windows installer zip

After any modpack/plugin change:
```bash
cp ValheimQoL-source/bin/Release/ValheimQoL.dll windows-installer/
# hand-edit the $Mods array in windows-installer/install.ps1 to match modpack.yaml
cd windows-installer && zip -r ../ValheimModpack.zip . -x ".*" && cd ..
```
The mod list in `install.ps1` is hand-copied from `modpack.yaml`, not
read dynamically — PowerShell has no built-in YAML parser, and a
hardcoded list is safer than dependency-resolution logic running
unsupervised on a player's machine. Test it before handing it out:
install PowerShell Core locally and actually run the download/extract
logic against the live Thunderstore API, don't just eyeball the diff.

## 5. Tuning settings live

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
