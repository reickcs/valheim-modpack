# valheim-modpack

A small Valheim BepInEx modpack — **built from source, not downloaded from
Thunderstore.** Every mod here is vendored from its own upstream GitHub
repo (each under its own license, see below) and compiled by this repo's
own build, plus a hand-written QoL plugin (`ValheimQoL`) that fills in
the rest — carry weight, stack size, workbench range/roof requirement,
fireplace fuel, map exploration radius, screen shake, free-stamina
building tools, server-side character storage, and more. Built for a
Docker-hosted dedicated server
([`community-valheim-tools/valheim-server-docker`](https://github.com/community-valheim-tools/valheim-server-docker))
running BepInEx.

**Why build instead of download:** full ownership of the codebase. A
bug or a Valheim-update breakage gets fixed here, directly, the moment
it's found — not whenever (or if) an upstream author gets to it. The
only thing still pulled from Thunderstore is BepInEx itself (the mod
loader — infrastructure, not one of this pack's mods).

**What makes this different from just installing a pile of mods:**
server and client settings **stay in sync automatically**. `ValheimQoL`
vendors the same [ServerSync](https://github.com/AzumattDev/AzuCraftyBoxes/blob/master/ConfigSync.cs)
mechanism `AzuCraftyBoxes` already uses — the server pushes its live
config to every connecting client. Change a setting on the server,
restart, and everyone picks it up on next connect. No distributing a
matching `.cfg` file, no drift.

## What's here

| Path | What |
|---|---|
| [`scripts/build-client.ps1`](scripts/build-client.ps1) | Builds all four mods, bundles them, rebuilds `ValheimModpack.zip`. No server access needed. |
| [`scripts/build-server.sh`](scripts/build-server.sh) | Builds the two server-side mods, deploys over SSH, restarts, verifies. Needs `REMOTE_HOST`. |
| [`modpack.yaml`](modpack.yaml) | Every mod: source repo, license, why it's here, what was stripped/fixed to build it. Source of truth. |
| [`docker-compose.yml`](docker-compose.yml) + [`.env.example`](.env.example) | The server stack. |
| [`ValheimQoL-source/`](ValheimQoL-source/) | Hand-written QoL plugin — Harmony patches over decompiled `assembly_valheim.dll`, never guessed from memory. |
| [`AzuCraftyBoxes-source/`](AzuCraftyBoxes-source/) | Craft from nearby containers. Vendored from [AzumattDev/AzuCraftyBoxes](https://github.com/AzumattDev/AzuCraftyBoxes) (MIT-0), optional integrations for mods this pack doesn't run stripped out. |
| [`PlantEasily-source/`](PlantEasily-source/) | Grid-snap crop planting (client-only). Vendored from [AdvizeGH/Advize_ValheimMods](https://github.com/AdvizeGH/Advize_ValheimMods) (GPL-3.0). |
| [`ConfigurationManager-source/`](ConfigurationManager-source/) | In-game live config editor (client-only). Vendored from [shudnal/ConfigurationManager](https://github.com/shudnal/ConfigurationManager) (LGPL-3.0). |
| [`windows-installer/`](windows-installer/) | Self-contained installer for Windows players — auto-detects Steam, installs BepInEx, copies in the four plugins above (bundled, not downloaded). |
| [`CLIENT-INSTALL.md`](CLIENT-INSTALL.md) | Manual/Gale-based client install (Linux, or if the automated installer doesn't fit). |
| [`push-config.sh`](push-config.sh) | Pushes a locally-tuned `.cfg` up to the server, with a diff preview and backup. |
| [`install_mods.py`](install_mods.py) | Historical fallback only — resolves a Thunderstore dependency graph. Not part of the current deploy path; see its own header comment. |

Full setup and build instructions: **[SETUP.md](SETUP.md)**.

## Current modpack

- **Server-side (shared):** `AzuCraftyBoxes`.
- **Client-only:** `PlantEasily`, `ConfigurationManager`.
- **Custom:** `ValheimQoL` — carry weight, stamina, stack size,
  workbench, fireplace, map exploration, screen shake, **server-side
  character storage** (built from scratch, replaces the old
  `ServerCharacters` mod — its source has no license, so it couldn't be
  vendored; see `modpack.yaml`). Full feature list in `modpack.yaml`'s
  `custom_plugins` entry, or the doc comments in
  [`ValheimQoL-source/Plugin.cs`](ValheimQoL-source/Plugin.cs).

Nothing here is pinned to a specific person's server — every real
IP/password/world-name lives in your own `.env`, which is gitignored.

## License

MIT (see [`LICENSE`](LICENSE)) for the original code in this repo
(`ValheimQoL-source/`). Each vendored mod keeps its own upstream
license, recorded per-entry in `modpack.yaml` and in a `LICENSE`/
`LICENSE.txt` file inside its own `*-source/` folder:

| Mod | License |
|---|---|
| AzuCraftyBoxes | MIT-0 |
| PlantEasily | GPL-3.0 |
| ConfigurationManager | LGPL-3.0 |

`ConfigSync.cs` (vendored into `ValheimQoL-source/`,
`AzuCraftyBoxes-source/`, and `ConfigurationManager-source/` — each
compiled plugin assembly needs its own copy) is unmodified from
[AzumattDev/AzuCraftyBoxes](https://github.com/AzumattDev/AzuCraftyBoxes)
under MIT-0 (No Attribution).
