using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Player.GetBuildStamina() is the single choke point for hammer (place/
    // remove/repair), hoe (raise/lower/level ground), and cultivator (cultivate/
    // plant) -- all three place their target through Player.TryPlacePiece or
    // Player.RemovePiece, and both read their stamina cost from this one private
    // method (GetRightItem().m_shared.m_attack.m_attackStamina, i.e. whichever
    // tool is currently equipped). Skipping it entirely and returning 0 zeroes
    // the cost for exactly those three tools, leaving combat/run/jump/dodge/swim
    // stamina (StaminaUseMultiplier) untouched.
    [HarmonyPatch(typeof(Player), "GetBuildStamina")]
    internal static class BuildToolStaminaPatch
    {
        private static bool Prefix(ref float __result)
        {
            if (!ValheimQoLPlugin.FreeBuildToolStamina.Value)
            {
                return true;
            }
            __result = 0f;
            return false;
        }
    }
}
