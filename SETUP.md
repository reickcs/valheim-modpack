# Setup

## 1. Server

Requires Docker + Docker Compose on the host.

```bash
git clone <this-repo> valheim && cd valheim
cp .env.example .env
# edit .env: SERVER_NAME, WORLD_NAME, SERVER_PASS at minimum
docker compose up -d
```

That pulls the vanilla dedicated server first. Once it's up, build the
modpack (see part 3 below for each mod's build command) and copy the
resulting DLLs into place:

```bash
mkdir -p config/bepinex/plugins/richard-ValheimQoL config/bepinex/plugins/Azumatt-AzuCraftyBoxes
cp ValheimQoL-source/bin/Release/ValheimQoL.dll config/bepinex/plugins/richard-ValheimQoL/
cp AzuCraftyBoxes-source/bin/Release/AzuCraftyBoxes.dll AzuCraftyBoxes-source/bin/Release/YamlDotNet.dll \
   config/bepinex/plugins/Azumatt-AzuCraftyBoxes/
docker compose restart
```

Only these two go on the server — `PlantEasily` and `ConfigurationManager`
are client-only (see `modpack.yaml`'s `client_only_mods`), they'd be dead
weight here.

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
  `rm -rf` its folder from both `config/bepinex/plugins/` and
  `data/bepinex/BepInEx/plugins/` before restarting, or the stale copy
  keeps loading.
- **A mod that logs "This mod is client-side only and is not needed on
  a dedicated server"** belongs in `modpack.yaml`'s `client_only_mods`,
  not `mods` — it's dead weight on the server (see `PlantEasily`'s entry
  for exactly this).

## 2. Clients

See [CLIENT-INSTALL.md](CLIENT-INSTALL.md), or hand out
`windows-installer/ValheimModpack.zip` for the one-click Windows path.

## 3. Building the mods from source

Needs the .NET 8 SDK (or newer). Every project here (`ValheimQoL-source/`,
`AzuCraftyBoxes-source/`, `PlantEasily-source/`,
`ConfigurationManager-source/`) needs reference DLLs this repo
intentionally doesn't ship in `<project>/libs/` — see
[`ValheimQoL-source/libs/README.md`](ValheimQoL-source/libs/README.md)
for the base set and where to get them (your own Valheim/dedicated-server
install, plus BepInExPack's `core/` folder).

**`AzuCraftyBoxes-source/`, `PlantEasily-source/`, and
`ConfigurationManager-source/` additionally need "publicized" copies**
of `assembly_valheim.dll`/`assembly_utils.dll`/`assembly_guiutils.dll`
(all private/internal members made public — they reach into game
internals more directly than ValheimQoL's Harmony/AccessTools-only
approach). Produce these yourself:
```bash
dotnet tool install -g BepInEx.AssemblyPublicizer.Cli
assembly-publicizer assembly_valheim.dll assembly_utils.dll assembly_guiutils.dll -o <project>/libs -f
```
(On Windows, if you hit "You must install or update .NET to run this
application" even with a newer SDK installed, set
`$env:DOTNET_ROLL_FORWARD = "LatestMajor"` first — the tool pins an
exact old runtime version by default.)

Then, for any of the four projects:
```bash
cd <project>-source
dotnet build -c Release
# -> bin/Release/<AssemblyName>.dll (+ any NuGet-resolved DLLs alongside it --
#    AzuCraftyBoxes and ConfigurationManager both need YamlDotNet.dll deployed
#    next to them; ConfigurationManager also needs Newtonsoft.Json.dll. These
#    aren't ILRepack-merged in, unlike upstream's own build -- simpler, at the
#    cost of a couple of extra loose DLLs per plugin folder.)
```

**Adding a new patch, or fixing a Valheim-update break:** decompile the
target class first — [`ilspycmd`](https://github.com/icsharpcode/ILSpy)
(`dotnet tool install -g ilspycmd`) against your own copy of
`assembly_valheim.dll` — and confirm the field/method actually exists
and is named what you think before writing code against it. Valheim's
internal API isn't stable enough between patches to guess from memory
or from an older version's source; every fix already made across these
four projects (see each's `build_fixes`/`stripped` entry in
`modpack.yaml`) was found this way, not guessed. `ilspycmd -t ClassName
assembly_valheim.dll` decompiles one class; `-l c` lists every type in
the assembly.

**Deploying a rebuilt plugin to the server** (only `ValheimQoL` and
`AzuCraftyBoxes` run there — see part 1):
```bash
scp bin/Release/<Name>.dll <server>:~/valheim/config/bepinex/plugins/<namespace-name>/<Name>.dll
scp bin/Release/<Name>.dll <server>:~/valheim/data/bepinex/BepInEx/plugins/<namespace-name>/<Name>.dll
ssh <server> 'cd ~/valheim && docker compose restart'
```
(Both copies — see the two-install-paths gotcha above.) Then watch the
logs for `Game server connected` and grep for exceptions before calling
it done.

## 4. Rebuilding the Windows installer zip

After any plugin change, rebuild whichever project changed (part 3),
then re-copy its output into `windows-installer/plugins/<namespace-name>/`
and re-zip:
```bash
cp ValheimQoL-source/bin/Release/ValheimQoL.dll windows-installer/plugins/richard-ValheimQoL/
cp AzuCraftyBoxes-source/bin/Release/AzuCraftyBoxes.dll AzuCraftyBoxes-source/bin/Release/YamlDotNet.dll \
   windows-installer/plugins/Azumatt-AzuCraftyBoxes/
cp PlantEasily-source/bin/Release/Advize_PlantEasily.dll windows-installer/plugins/Advize-PlantEasily/
cp ConfigurationManager-source/bin/Release/ConfigurationManager.dll \
   ConfigurationManager-source/bin/Release/Newtonsoft.Json.dll \
   ConfigurationManager-source/bin/Release/YamlDotNet.dll \
   windows-installer/plugins/shudnal-ConfigurationManager/

# PowerShell: Compress-Archive -Path windows-installer\* -DestinationPath ValheimModpack.zip -Force
```
`install.ps1` itself only needs to change if a plugin's *folder name*
changes (it copies whatever's under `plugins/` verbatim — no hardcoded
mod list to keep in sync anymore, unlike the old Thunderstore-download
version). It still downloads BepInEx itself from Thunderstore — that's
infrastructure, not one of this pack's mods.

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
