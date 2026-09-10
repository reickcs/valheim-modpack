using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Player.m_maxCarryWeight (vanilla 300) is the base carry limit before
    // GetMaxCarryWeight() applies food/status-effect modifiers on top via
    // m_seman.ModifyMaxCarryWeight — those still apply normally on top of
    // whatever base we set here.
    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class CarryWeightPatch
    {
        private static void Postfix(Player __instance)
        {
            __instance.m_maxCarryWeight *= ValheimQoLPlugin.MaxCarryWeightMultiplier.Value;
        }
    }
}
