using System;
using System.IO;
using BepInEx;
using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Server-side character storage, built from scratch -- the upstream
    // ServerCharacters mod has no license on its source, so it couldn't be
    // vendored (see modpack.yaml). Decompiling confirmed vanilla's
    // PlayerProfile.Load/Save only ever touch the local .fch file
    // (LoadPlayerFromDisk/SavePlayerToDisk are the only writers of the
    // private m_playerData field) -- there's no existing server-authoritative
    // path to hook into, so this adds a parallel one: on connecting to a
    // remote dedicated server, ask it for this SteamID's stored character
    // blob and use that instead of whatever's in the local file; on every
    // local save, also push the current blob up to the server.
    //
    // Pull-based on purpose: the client explicitly requests its data (once
    // it has registered a handler to receive the response) rather than the
    // server pushing unprompted at handshake time, which would race an
    // unregistered receiver on the other end -- notice vanilla's own
    // ServerSyncedPlayerData mechanism (platform display names) avoids the
    // same race by deferring its send to a later trigger rather than doing
    // it inline in RPC_PeerInfo.
    //
    // Safety properties, deliberately:
    //  - The local .fch save always happens first and is never skipped --
    //    this is a Postfix on Game.SavePlayerProfile, running after the
    //    original. Worst case if the network push fails, the player's own
    //    local file is exactly as good as vanilla; nothing extra is lost.
    //  - If the server has no stored data yet for a SteamID (new player, or
    //    the first connect after switching off the old Thunderstore mod --
    //    this does NOT read that mod's storage format, there was no license
    //    to learn it from) the client just keeps using its local file, same
    //    as vanilla. A "no data" response never deletes or overwrites
    //    anything.
    //  - Any failure (bad data, reflection failure, IO error) is caught and
    //    logged, never thrown into gameplay code -- falls back to local.
    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    internal static class ServerProfileHandshakePatch
    {
        private const string RequestRpcName = "richard.serverprofile.request";
        private const string DownloadRpcName = "richard.serverprofile.down";
        private const string UploadRpcName = "richard.serverprofile.up";

        // Set by RPC_Download when a response arrives; consumed and cleared
        // by ServerProfileLoadPatch the next time PlayerProfile.LoadPlayerData
        // runs. There's only ever one local player per client process, so a
        // single static slot is enough -- no per-connection bookkeeping needed.
        internal static byte[] PendingServerProfile;

        private static void Postfix(ZNet __instance, ZRpc rpc)
        {
            rpc.Register<ZPackage>(DownloadRpcName, RPC_Download);
            rpc.Register<ZPackage>(UploadRpcName, RPC_Upload);

            if (__instance.IsServer())
            {
                rpc.Register(RequestRpcName, RPC_Request);
            }
            else
            {
                rpc.Invoke(RequestRpcName);
            }
        }

        private static void RPC_Request(ZRpc rpc)
        {
            if (!ValheimQoLPlugin.ServerCharacterStorageEnabled.Value)
            {
                return;
            }
            try
            {
                string id = SanitizeId(rpc.GetSocket().GetHostName());
                byte[] data = ReadStoredProfile(id);
                ZPackage pkg = new ZPackage();
                pkg.Write(data != null);
                if (data != null)
                {
                    pkg.Write(data);
                }
                rpc.Invoke(DownloadRpcName, pkg);
                ValheimQoLPlugin.Log.LogInfo(data != null
                    ? $"[ServerProfiles] sent stored profile to {id}"
                    : $"[ServerProfiles] no stored profile for {id} yet -- they'll use their local one");
            }
            catch (Exception e)
            {
                ValheimQoLPlugin.Log.LogWarning($"[ServerProfiles] failed handling request: {e}");
            }
        }

        private static void RPC_Download(ZRpc rpc, ZPackage pkg)
        {
            if (!ValheimQoLPlugin.ServerCharacterStorageEnabled.Value)
            {
                return;
            }
            try
            {
                bool hasData = pkg.ReadBool();
                PendingServerProfile = hasData ? pkg.ReadByteArray() : null;
            }
            catch (Exception e)
            {
                PendingServerProfile = null;
                ValheimQoLPlugin.Log.LogWarning($"[ServerProfiles] failed reading server response, using local data instead: {e}");
            }
        }

        private static void RPC_Upload(ZRpc rpc, ZPackage pkg)
        {
            if (!ValheimQoLPlugin.ServerCharacterStorageEnabled.Value)
            {
                return;
            }
            try
            {
                string id = SanitizeId(rpc.GetSocket().GetHostName());
                byte[] data = pkg.ReadByteArray();
                WriteStoredProfile(id, data);
                ValheimQoLPlugin.Log.LogInfo($"[ServerProfiles] stored updated profile for {id}");
            }
            catch (Exception e)
            {
                ValheimQoLPlugin.Log.LogWarning($"[ServerProfiles] failed storing upload: {e}");
            }
        }

        internal static string StorageDir =>
            Path.Combine(Paths.ConfigPath, "richard.valheimqol.serverprofiles");

        private static string SanitizeId(string hostName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = hostName.ToCharArray();
            string clean = new string(Array.FindAll(chars, c => Array.IndexOf(invalid, c) < 0));
            if (string.IsNullOrEmpty(clean))
            {
                throw new InvalidOperationException("peer id was empty after sanitizing");
            }
            return clean;
        }

        private static byte[] ReadStoredProfile(string id)
        {
            string path = Path.Combine(StorageDir, id + ".dat");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        private static void WriteStoredProfile(string id, byte[] data)
        {
            Directory.CreateDirectory(StorageDir);
            string path = Path.Combine(StorageDir, id + ".dat");
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tmp, path); // write-to-temp-then-rename: `path` is never observed half-written
        }
    }

    [HarmonyPatch(typeof(PlayerProfile), "LoadPlayerData")]
    internal static class ServerProfileLoadPatch
    {
        private static readonly AccessTools.FieldRef<PlayerProfile, byte[]> PlayerDataRef =
            AccessTools.FieldRefAccess<PlayerProfile, byte[]>("m_playerData");

        private static void Prefix(PlayerProfile __instance)
        {
            if (!ValheimQoLPlugin.ServerCharacterStorageEnabled.Value)
            {
                return;
            }
            byte[] pending = ServerProfileHandshakePatch.PendingServerProfile;
            if (pending == null)
            {
                return;
            }
            ServerProfileHandshakePatch.PendingServerProfile = null;
            try
            {
                PlayerDataRef(__instance) = pending;
                ValheimQoLPlugin.Log.LogInfo("[ServerProfiles] applied server-stored character data");
            }
            catch (Exception e)
            {
                ValheimQoLPlugin.Log.LogWarning($"[ServerProfiles] failed applying server data, using local instead: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(Game), "SavePlayerProfile")]
    internal static class ServerProfileSavePatch
    {
        private static void Postfix()
        {
            if (!ValheimQoLPlugin.ServerCharacterStorageEnabled.Value)
            {
                return;
            }
            if (ZNet.instance == null || ZNet.instance.IsServer() || !Player.m_localPlayer)
            {
                return; // singleplayer/hosting, or no active character to push
            }
            try
            {
                var peers = ZNet.instance.GetPeers();
                if (peers.Count == 0)
                {
                    return;
                }
                ZPackage pkg = new ZPackage();
                Player.m_localPlayer.Save(pkg);
                peers[0].m_rpc.Invoke("richard.serverprofile.up", pkg);
            }
            catch (Exception e)
            {
                ValheimQoLPlugin.Log.LogWarning($"[ServerProfiles] failed pushing profile to server (local save already succeeded, nothing lost): {e}");
            }
        }
    }
}
