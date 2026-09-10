using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Minimap.m_exploreRadius (vanilla 100) is the radius around the player
    // that gets revealed every m_exploreInterval seconds (Minimap.UpdateExplore).
    // Scaling it here reveals more map per step, same idea as V+'s map
    // exploration radius setting.
    [HarmonyPatch(typeof(Minimap), "Awake")]
    internal static class MinimapExplorePatch
    {
        private static void Postfix(Minimap __instance)
        {
            __instance.m_exploreRadius *= ValheimQoLPlugin.MapExploreRadiusMultiplier.Value;
        }
    }
}
