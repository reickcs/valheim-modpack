using HarmonyLib;

namespace ValheimQoL.Patches
{
    [HarmonyPatch(typeof(Vagon), "Awake")]
    internal static class VagonPatch
    {
        private static void Postfix(Vagon __instance)
        {
            __instance.m_baseMass *= ValheimQoLPlugin.WagonBaseMassMultiplier.Value;
            __instance.m_itemWeightMassFactor *= ValheimQoLPlugin.WagonItemWeightMultiplier.Value;
        }
    }
}
