using HarmonyLib;

namespace ValheimQoL.Patches
{
    [HarmonyPatch(typeof(Game), "Awake")]
    internal static class DifficultyScalingPatch
    {
        private static void Postfix(Game __instance)
        {
            __instance.m_damageScalePerPlayer = ValheimQoLPlugin.DifficultyDamageScalePerPlayer.Value;
            __instance.m_healthScalePerPlayer = ValheimQoLPlugin.DifficultyHealthScalePerPlayer.Value;
            __instance.m_difficultyScaleMaxPlayers = ValheimQoLPlugin.DifficultyMaxScalingPlayers.Value;
        }
    }
}
