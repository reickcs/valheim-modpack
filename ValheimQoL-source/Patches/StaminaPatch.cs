using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Player.UseStamina(float v) is the single choke point for every stamina cost:
    // building, running, jumping, attacking, dodging, swimming. Scaling v here
    // scales all of them uniformly, same effect as V+'s per-action stamina sliders
    // collapsed into one multiplier.
    [HarmonyPatch(typeof(Player), "UseStamina")]
    internal static class StaminaPatch
    {
        private static void Prefix(ref float v)
        {
            v *= ValheimQoLPlugin.StaminaUseMultiplier.Value;
        }
    }
}
