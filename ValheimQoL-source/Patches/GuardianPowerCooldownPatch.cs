using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Player.ActivateGuardianPower() sets m_guardianPowerCooldown to the
    // equipped power's own m_cooldown (vanilla's much-longer "real" cooldown,
    // separate from m_ttl, the power's active duration) -- decompile-verified,
    // both fields live on the StatusEffect asset itself
    // (m_guardianSE, private on Player). No per-boss name list needed: this
    // reads whichever power is currently equipped and matches its cooldown to
    // its own duration generically, so it works for every boss power without
    // guessing asset names.
    [HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
    internal static class GuardianPowerCooldownPatch
    {
        private static readonly AccessTools.FieldRef<Player, StatusEffect> GuardianSERef =
            AccessTools.FieldRefAccess<Player, StatusEffect>("m_guardianSE");

        private static void Postfix(Player __instance)
        {
            if (!ValheimQoLPlugin.GuardianPowerCooldownMatchesDuration.Value)
            {
                return;
            }

            StatusEffect guardianEffect = GuardianSERef(__instance);
            if (guardianEffect == null)
            {
                return;
            }

            // Vanilla just set m_guardianPowerCooldown = guardianEffect.m_cooldown
            // on this exact call, on the sole path that actually activates the
            // power (the early-return failure paths never touch this field) --
            // matching that value here is how we detect "it just fired" without
            // needing a second patch to track before/after state.
            if (__instance.m_guardianPowerCooldown == guardianEffect.m_cooldown)
            {
                __instance.m_guardianPowerCooldown = guardianEffect.m_ttl;
            }
        }
    }
}
