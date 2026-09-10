using HarmonyLib;

namespace ValheimQoL.Patches
{
    // m_placeRotationDegrees is private; it controls how many degrees each
    // rotation-key press turns a placement ghost (vanilla: 22.5 = 16 steps
    // per full circle). Lowering it via reflection gives much finer control,
    // approximating V+'s "free rotation mode".
    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class BuildRotationPatch
    {
        private static readonly AccessTools.FieldRef<Player, float> RotationDegreesRef =
            AccessTools.FieldRefAccess<Player, float>("m_placeRotationDegrees");

        private static void Postfix(Player __instance)
        {
            RotationDegreesRef(__instance) = ValheimQoLPlugin.BuildRotationDegrees.Value;
        }
    }
}
