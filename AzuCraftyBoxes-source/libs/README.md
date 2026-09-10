# `libs/` — not included, build this folder yourself

See [`ValheimQoL-source/libs/README.md`](../../ValheimQoL-source/libs/README.md)
for the base set of files and where to get them, and
[`SETUP.md`](../../SETUP.md) part 3 for the publicizer command. This
project additionally needs, all sourced the same way (your own Valheim/
dedicated-server `Managed/` folder):

- `assembly_valheim.dll`, `assembly_utils.dll`, `assembly_guiutils.dll`
  as their **publicized** variants (this project reaches into game
  internals more directly than ValheimQoL's Harmony/AccessTools-only
  approach)
- `Splatform.dll`
- `UnityEngine.JSONSerializeModule.dll`, `UnityEngine.TextRenderingModule.dll`

`YamlDotNet` is restored via NuGet (see the `.csproj`), not placed here.
