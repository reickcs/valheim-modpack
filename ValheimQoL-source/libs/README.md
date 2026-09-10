# `libs/` — not included, build this folder yourself

This project builds against Valheim's own game assemblies and BepInEx's
core DLLs. Neither is redistributable — they belong to Iron Gate / Coffee
Stain and to the BepInEx project's binary release respectively — so this
folder is empty in the repo and gitignored.

Before building, put these files here (all referenced by `HintPath` in
`ValheimQoL.csproj`):

| File | Source |
|---|---|
| `assembly_valheim.dll`, `assembly_utils.dll`, `Assembly-CSharp.dll`, `UnityEngine*.dll`, `Unity.TextMeshPro.dll` | Your own Valheim install's `valheim_Data/Managed/` (Windows client) or a dedicated server's `valheim_server_Data/Managed/` (Linux) — same managed IL either way for a given game version. |
| `BepInEx.dll`, `0Harmony.dll` | Extract from [`denikson-BepInExPack_Valheim`](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (Thunderstore) — `BepInExPack_Valheim/BepInEx/core/`. |

Quickest path if you already run the server from this repo: `docker cp`
(or just `cp`, since the volumes are bind-mounted) the same files out of
`./data/server/valheim_server_Data/Managed/` and
`./data/bepinex/BepInEx/core/` on the host running `docker-compose.yml`.
