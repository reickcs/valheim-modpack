using HarmonyLib;

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
        private static void Postfix(Player __instance)
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
        }
    }
}
