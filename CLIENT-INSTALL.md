# Installing the modpack (client-side, manual/Gale path)

Every player's client needs the **exact same mods at the exact same
versions** as the server, or you won't be able to connect (or worse,
connect and desync). The list below matches [`modpack.yaml`](modpack.yaml).

This is the manual fallback. Most players should use the [automated
Windows installer](windows-installer/) instead — see its `README.txt`.
Use this path on Linux, or if you want an in-game live config editor
(`ConfigurationManager`, below) alongside it.

We use **Gale** (<https://galemodmanager.com>) to manage the
Thunderstore-hosted mods — actively maintained, native installers for
Windows and Linux, profile codes cross-compatible with
r2modman/Thunderstore Mod Manager.

One mod, `ValheimQoL`, is **hand-written, not on Thunderstore** — Gale
can't fetch it. Get `ValheimQoL.dll` from whoever built it for your
server (it's also bundled inside `windows-installer/ValheimModpack.zip`
in this repo if you built that yourself — see [SETUP.md](SETUP.md)).

## Windows

1. Download and run the Gale installer from <https://galemodmanager.com>.
2. Select **Valheim** as the game.
3. Create a new profile (e.g. name it after the server).
4. Install each mod below via Gale's search — for each one, click the
   **version dropdown** and pick the exact pinned version (not "latest"):

   | Mod | Version |
   |---|---|
   | Azumatt-AzuCraftyBoxes | 1.8.15 |
   | Smoothbrain-ServerCharacters | 1.4.16 |

   Gale pulls in the shared BepInEx dependency automatically.

   Optional, your own machine only, doesn't need to match the server:

   | Mod | Version |
   |---|---|
   | Advize-PlantEasily | 2.1.1 |
   | shudnal-ConfigurationManager | 1.1.16 |

5. **Get `ValheimQoL.dll`** and drop it into:
   `<Gale profile folder>/BepInEx/plugins/richard-ValheimQoL/ValheimQoL.dll`
   Gale shows the profile folder location under the profile's settings —
   look for a "Browse profile folder" button. Create the
   `richard-ValheimQoL` subfolder if it doesn't already exist.
6. Launch Valheim **through Gale** (not through Steam directly) — this is
   what actually injects the mods.
7. Connect: **Join Game → IP/Direct connect**, using the address and
   password whoever's running the server gave you.

## Linux

Same idea, native package instead of an installer — download the
`.rpm`/`.deb` from Gale's releases page, install it, then follow steps
3-7 above.

## Keeping in sync

If the server's modpack ever changes (see `modpack.yaml`'s upgrade
workflow at the top of that file), this table needs to be regenerated to
match, and everyone needs to update their Gale profile (mods *and*
`ValheimQoL.dll`) before the next session. Don't update mid-session.

## Once ValheimQoL is installed: settings sync automatically

As of ValheimQoL 0.8.0, every setting syncs from the server the moment
you connect (the same [ServerSync](https://github.com/AzumattDev/AzuCraftyBoxes/blob/master/ConfigSync.cs)
mechanism AzuCraftyBoxes and ServerCharacters already use) — you don't
need to hand-edit `richard.valheimqol.cfg` to match the server, and it
won't drift over time. `ConfigurationManager` (optional, above) is still
useful for *previewing* what a setting does locally, but whatever the
server has wins once you're connected.
