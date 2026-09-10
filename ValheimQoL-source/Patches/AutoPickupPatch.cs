using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Vanilla Valheim already has auto-pickup (Player.m_autoPickupRange, default 2m).
    // We just override the range from config once the player object is created.
    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class AutoPickupPatch
    {
        private static void Postfix(Player __instance)
        {
            __instance.m_autoPickupRange = ValheimQoLPlugin.AutoPickupRange.Value;
        }
    }
}
