using HarmonyLib;
using UnityEngine;

namespace ValheimQoL.Patches
{
    // Player.UpdateFood recomputes each active food's m_health/m_stamina/m_eitr
    // every ~1s as food.m_item.m_shared.m_food * (m_time/m_foodBurnTime)^0.3 --
    // a curve that holds close to full value for most of the duration but
    // drops off sharply in the final stretch. This resets each still-active
    // food back to its full bonus after the original runs, then reapplies the
    // totals the same way the original does (GetFoods()/m_baseHP/m_baseStamina
    // are all public; only SetMaxEitr is private, reached via Traverse).
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    internal static class FoodBuffPatch
    {
        private static readonly AccessTools.FieldRef<Player, float> StaminaRef =
            AccessTools.FieldRefAccess<Player, float>("m_stamina");
        private static readonly AccessTools.FieldRef<Player, float> EitrRef =
            AccessTools.FieldRefAccess<Player, float>("m_eitr");

        // Vanilla's own SetMaxHealth/SetMaxStamina/SetMaxEitr clamp the
        // *current* value down whenever the new max is lower, but never
        // restore it afterward. Every tick, vanilla itself briefly sets max
        // to the decayed (lower) value BEFORE this postfix corrects it back
        // to full -- which silently clips the player's real current health/
        // stamina/eitr down to that transient low max, permanently, even
        // though the max a moment later says otherwise. Captured here
        // (Prefix, before vanilla's clamp runs) and restored in the Postfix
        // once the real max is back to full, so a food nearing expiry no
        // longer visibly saws the player's current stats up and down.
        private static void Prefix(Player __instance, out (float health, float stamina, float eitr) __state)
        {
            if (!ValheimQoLPlugin.FoodBuffsDontDecay.Value)
            {
                __state = default;
                return;
            }

            __state = (__instance.GetHealth(), StaminaRef(__instance), EitrRef(__instance));
        }

        private static void Postfix(Player __instance, (float health, float stamina, float eitr) __state)
        {
            if (!ValheimQoLPlugin.FoodBuffsDontDecay.Value)
            {
                return;
            }

            bool changed = false;
            foreach (Player.Food food in __instance.GetFoods())
            {
                if (food.m_time <= 0f || food.m_item?.m_shared == null)
                {
                    continue;
                }
                float fullHealth = food.m_item.m_shared.m_food;
                float fullStamina = food.m_item.m_shared.m_foodStamina;
                float fullEitr = food.m_item.m_shared.m_foodEitr;
                if (food.m_health != fullHealth || food.m_stamina != fullStamina || food.m_eitr != fullEitr)
                {
                    food.m_health = fullHealth;
                    food.m_stamina = fullStamina;
                    food.m_eitr = fullEitr;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            float hp = __instance.m_baseHP;
            float stamina = __instance.m_baseStamina;
            float eitr = 0f;
            foreach (Player.Food food in __instance.GetFoods())
            {
                hp += food.m_health;
                stamina += food.m_stamina;
                eitr += food.m_eitr;
            }

            __instance.SetMaxHealth(hp, flashBar: false);
            __instance.SetMaxStamina(stamina, flashBar: false);
            Traverse.Create(__instance).Method("SetMaxEitr", new object[] { eitr, false }).GetValue();

            __instance.SetHealth(Mathf.Min(__state.health, hp));
            StaminaRef(__instance) = Mathf.Min(__state.stamina, stamina);
            EitrRef(__instance) = Mathf.Min(__state.eitr, eitr);
        }
    }
}
