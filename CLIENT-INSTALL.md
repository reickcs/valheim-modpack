# Installing the modpack (manual / Linux path)

Every player's client needs the **same build** of every mod as the
server, or you won't be able to connect (or worse, connect and desync).
Windows players should just use the [automated installer](windows-installer/)
instead — see its `README.txt`. This page is for Linux, or anyone who'd
rather do it by hand.

**Don't install these from Thunderstore/Gale** — this pack's copies are
built from modified source (see `modpack.yaml` for exactly what changed
per mod) and aren't the same files as the public Thunderstore releases.
Grab the actual DLLs this pack ships instead, either from
`windows-installer/plugins/` in this repo (platform-independent, despite
the folder name — plain .NET Framework DLLs), or by building them
yourself (see [SETUP.md](SETUP.md) part 3).

## Steps

1. Install BepInEx for Valheim if you haven't — either via
   [Gale](https://galemodmanager.com) (create a profile, it installs
   BepInEx as a side effect of adding any mod) or manually from
   [`denikson/BepInExPack_Valheim`](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
   on Thunderstore. BepInEx itself is infrastructure, not one of this
   pack's own mods — fine to get it the normal way.

2. Copy each plugin folder from this repo's `windows-installer/plugins/`
   into your `BepInEx/plugins/` directory:

   | Folder | Required? |
   |---|---|
   | `richard-ValheimQoL/` | Yes — must match the server |
   | `Azumatt-AzuCraftyBoxes/` | Yes — must match the server |
   | `Advize-PlantEasily/` | Optional, your own machine only |
   | `shudnal-ConfigurationManager/` | Optional, your own machine only |

   Each folder already contains its DLL plus any extra DLLs it needs
   alongside it (e.g. `YamlDotNet.dll`) — copy the whole folder, not
   just the main DLL.

3. Launch Valheim normally through Steam — `winhttp.dll` (installed by
   BepInEx) injects the mod loader automatically, no special launch
   options needed.

4. Connect: **Join Game → IP/Direct connect**, using the address and
   password whoever's running the server gave you.

## Once ValheimQoL is installed: settings sync automatically

Every `ValheimQoL` setting syncs from the server the moment you connect
(the same [ServerSync](https://github.com/AzumattDev/AzuCraftyBoxes/blob/master/ConfigSync.cs)
mechanism `AzuCraftyBoxes` also uses) — you don't need to hand-edit
`richard.valheimqol.cfg` to match the server, and it won't drift over
time. `ConfigurationManager` (optional, above) is still useful for
*previewing* what a setting does locally, but whatever the server has
wins once you're connected.

## Keeping in sync

If the server's modpack ever changes, rebuild/re-copy per
[SETUP.md](SETUP.md) parts 3-4, and everyone needs the matching new
build before the next session. Don't update mid-session.
