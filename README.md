# valheim-modpack

A small, deliberately-trimmed Valheim BepInEx modpack, plus a
hand-written QoL plugin (`ValheimQoL`) that fills in the rest — carry
weight, stack size, workbench range/roof requirement, fireplace fuel,
map exploration radius, screen shake, free-stamina building tools, and
more. Built for a Docker-hosted dedicated server
([`community-valheim-tools/valheim-server-docker`](https://github.com/community-valheim-tools/valheim-server-docker))
running BepInEx.

**What makes this different from just installing a pile of mods:**
server and client settings **stay in sync automatically**. `ValheimQoL`
vendors the same [ServerSync](https://github.com/AzumattDev/AzuCraftyBoxes/blob/master/ConfigSync.cs)
mechanism `AzuCraftyBoxes` and `ServerCharacters` already use — the
server pushes its live config to every connecting client. Change a
setting on the server, restart, and everyone picks it up on next
connect. No distributing a matching `.cfg` file, no drift.

## What's here

| Path | What |
|---|---|
| [`modpack.yaml`](modpack.yaml) | Pinned mod versions (server + client-only) and the upgrade workflow, documented inline. Source of truth. |
| [`install_mods.py`](install_mods.py) | Resolves `modpack.yaml`'s dependency graph via the Thunderstore API and installs into a target plugins directory. |
| [`docker-compose.yml`](docker-compose.yml) + [`.env.example`](.env.example) | The server stack. |
| [`ValheimQoL-source/`](ValheimQoL-source/) | The custom plugin's C# source (Harmony patches over decompiled `assembly_valheim.dll`, never guessed from memory — see [SETUP.md](SETUP.md)). |
| [`windows-installer/`](windows-installer/) | Self-contained installer for Windows players — auto-detects Steam, installs BepInEx, pulls every pinned mod from Thunderstore, drops in `ValheimQoL.dll`. |
| [`CLIENT-INSTALL.md`](CLIENT-INSTALL.md) | Manual/Gale-based client install (Linux, or if the automated installer doesn't fit). |
| [`push-config.sh`](push-config.sh) | Pushes a locally-tuned `.cfg` up to the server, with a diff preview and backup. |

Full setup instructions: **[SETUP.md](SETUP.md)**.

## Current modpack

- **Server-side (shared):** `AzuCraftyBoxes` (craft from nearby
  containers), `ServerCharacters` (server-side character storage).
- **Client-only:** `PlantEasily` (grid-snap crop planting),
  `ConfigurationManager` (in-game live config editor/preview).
- **Custom:** `ValheimQoL` — see its feature list in `modpack.yaml`'s
  `custom_plugins` entry, or the doc comments in
  [`ValheimQoL-source/Plugin.cs`](ValheimQoL-source/Plugin.cs).

Nothing here is pinned to a specific person's server — every real
IP/password/world-name lives in your own `.env`, which is gitignored.

## License

MIT (see [`LICENSE`](LICENSE)) for the original code in this repo.
`ValheimQoL-source/ConfigSync.cs` is vendored, unmodified, from
[AzumattDev/AzuCraftyBoxes](https://github.com/AzumattDev/AzuCraftyBoxes)
under MIT-0 (No Attribution) — see the header comment in that file.
