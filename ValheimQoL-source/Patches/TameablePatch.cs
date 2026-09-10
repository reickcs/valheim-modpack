using HarmonyLib;
using UnityEngine;

namespace ValheimQoL.Patches
{
    // Character.Damage(HitData) is the single entry point for all combat damage.
    // If the target is a tamed creature and this hit would kill it, scale the hit
    // down so it leaves at least 1 HP — same net effect as V+'s "essential
    // creature" protection, without touching the death state machine at all.
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class TameablePatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (!ValheimQoLPlugin.TamedPetsCantDie.Value)
            {
                return;
            }
            Tameable tameable = __instance.GetComponent<Tameable>();
            if (tameable == null || !tameable.IsTamed())
            {
                return;
            }

            float health = __instance.GetHealth();
            float totalDamage = hit.GetTotalDamage();
            if (totalDamage <= 0f || totalDamage < health)
            {
                return;
            }

            float safeScale = Mathf.Clamp01((health - 1f) / totalDamage);
            hit.ApplyModifier(safeScale);
        }
    }
}
