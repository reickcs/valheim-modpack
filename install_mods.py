#!/usr/bin/env python3
"""HISTORICAL FALLBACK -- not part of the current deploy path.

As of 2026-09-09 every mod in this pack is built from vendored source
(*-source/ folders, see SETUP.md part 3) rather than downloaded from
Thunderstore, so this script currently has nothing to do against
modpack.yaml's `mods`/`client_only_mods` entries (they no longer carry
Thunderstore namespace/name/pinned fields in a form this resolves).
Kept only in case a *future* mod gets added to the pack before someone
gets around to vendoring it -- point it at a modpack.yaml-shaped file
with the old namespace/name/pinned schema and it still works standalone.

Original docstring follows.

Install the pinned Valheim BepInEx modpack from modpack.yaml.

Reads mods[].namespace/name/pinned from modpack.yaml (next to this script),
recursively resolves their dependency graph via the Thunderstore experimental
API, downloads each package zip, and extracts it into
config/bepinex/plugins/<namespace>-<name>/. Skips the BepInExPack itself
since the docker image (or the local client's own BepInEx install) already
provisions it.

Usage:
  python3 install_mods.py [--dry-run]
      Server-side modpack only, installed to ./config/bepinex/plugins/

  python3 install_mods.py --client [--plugins-dir PATH] [--dry-run]
      Server-side modpack + client_only_mods (e.g. ConfigurationManager).
      --plugins-dir defaults to ./config/bepinex/plugins/ if omitted; pass
      the real game's BepInEx/plugins directory to install directly there,
      e.g.:
      python3 install_mods.py --client --plugins-dir \
        "/path/to/Valheim/BepInEx/plugins"
"""
import argparse
import io
import json
import os
import shutil
import urllib.request
import zipfile

import yaml

HERE = os.path.dirname(os.path.abspath(__file__))
API = "https://thunderstore.io/api/experimental/package/{ns}/{name}/{ver}/"
SKIP_PREFIXES = ("denikson-BepInExPack",)
HEADERS = {"User-Agent": "curl/8.5.0"}

resolved = {}
conflicts = []


def fetch_meta(ns, name, ver):
    req = urllib.request.Request(API.format(ns=ns, name=name, ver=ver), headers=HEADERS)
    with urllib.request.urlopen(req, timeout=20) as r:
        return json.load(r)


def resolve(ns, name, ver):
    full = f"{ns}-{name}-{ver}"
    key = f"{ns}-{name}"
    if any(k.startswith(key + "-") for k in resolved):
        existing = next(k for k in resolved if k.startswith(key + "-"))
        if existing != full:
            conflicts.append((existing, full))
        return
    print(f"resolving {full}")
    meta = fetch_meta(ns, name, ver)
    resolved[full] = meta
    for dep in meta.get("dependencies", []):
        parts = dep.rsplit("-", 2)
        if len(parts) != 3:
            print(f"  ! skipping unparseable dependency: {dep}")
            continue
        dns, dname, dver = parts
        if dep.startswith(SKIP_PREFIXES):
            print(f"  - skip (provided by image): {dep}")
            continue
        resolve(dns, dname, dver)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--client", action="store_true",
                         help="also resolve/install client_only_mods")
    parser.add_argument("--plugins-dir", default=None,
                         help="override install target (default: ./config/bepinex/plugins)")
    args = parser.parse_args()

    plugins_dir = args.plugins_dir or os.path.join(HERE, "config", "bepinex", "plugins")

    with open(os.path.join(HERE, "modpack.yaml")) as f:
        pack = yaml.safe_load(f)

    for mod in pack["mods"]:
        resolve(mod["namespace"], mod["name"], str(mod["pinned"]))

    if args.client:
        for mod in pack.get("client_only_mods", []):
            resolve(mod["namespace"], mod["name"], str(mod["pinned"]))

    if conflicts:
        print("\n!!! VERSION CONFLICTS DETECTED (kept the first-seen version of each) !!!")
        for a, b in conflicts:
            print(f"  {a}  vs  {b}")
        print("Review before trusting this install if any of these span a major version.\n")

    print(f"\n{'Would install' if args.dry_run else 'Installing'} {len(resolved)} packages into {plugins_dir}\n")

    for full, meta in resolved.items():
        if full.startswith(SKIP_PREFIXES):
            continue
        dest = os.path.join(plugins_dir, f"{meta['namespace']}-{meta['name']}")
        print(f"{'would install' if args.dry_run else 'installing'} {full} -> {dest}")
        if args.dry_run:
            continue
        # Clear any stale prior version of this mod first.
        if os.path.isdir(dest):
            shutil.rmtree(dest)
        req = urllib.request.Request(meta["download_url"], headers=HEADERS)
        with urllib.request.urlopen(req, timeout=60) as r:
            data = r.read()
        os.makedirs(dest, exist_ok=True)
        with zipfile.ZipFile(io.BytesIO(data)) as zf:
            zf.extractall(dest)

    print("\nDone. Resolved packages:")
    for full in sorted(resolved):
        if not full.startswith(SKIP_PREFIXES):
            print(f"  {full}")

    print(
        "\nNOTE: on the gameserver, config/bepinex/plugins/ is synced (copy, "
        "not mirror) into the running container on start/restart — it does "
        "NOT delete files removed from source. If you drop a mod from "
        "modpack.yaml, also manually rm -rf its folder from both "
        "config/bepinex/plugins/ AND data/bepinex/BepInEx/plugins/ on the "
        "server before restarting, or it'll keep loading the stale copy."
    )


if __name__ == "__main__":
    main()
