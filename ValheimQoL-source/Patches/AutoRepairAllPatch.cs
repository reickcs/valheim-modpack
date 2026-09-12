using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimQoL.Patches
{
    // Vanilla's InventoryGui.RepairOneItem() genuinely repairs only one item
    // per click (decompile-confirmed: it returns immediately after fixing the
    // first eligible item, showing "No more item to repair" on the next click
    // instead of continuing). This repairs everything at once the moment the
    // player connects to any crafting station, using the exact same eligibility
    // logic InventoryGui.CanRepair uses -- decompiled and reimplemented here
    // since every field/method it touches (Player, ObjectDB, CraftingStation,
    // Recipe) is already public; no InventoryGui instance is needed.
    [HarmonyPatch(typeof(Player), "SetCraftingStation")]
    internal static class AutoRepairAllPatch
    {
        private static void Postfix(Player __instance, CraftingStation station)
        {
            if (!ValheimQoLPlugin.AutoRepairAllOnInteract.Value || station == null)
            {
                return;
            }

            List<ItemDrop.ItemData> worn = new List<ItemDrop.ItemData>();
            __instance.GetInventory().GetWornItems(worn);

            int repaired = 0;
            foreach (ItemDrop.ItemData item in worn)
            {
                if (!CanRepair(__instance, station, item))
                {
                    continue;
                }
                __instance.RaiseSkill(Skills.SkillType.Crafting, 1f - item.m_durability / item.GetMaxDurability());
                item.m_durability = item.GetMaxDurability();
                repaired++;
            }

            if (repaired > 0)
            {
                station.m_repairItemDoneEffects.Create(station.transform.position, Quaternion.identity);
                __instance.Message(MessageHud.MessageType.Center, $"Repaired {repaired} item{(repaired == 1 ? "" : "s")}");
            }
        }

        // Mirrors InventoryGui.CanRepair(ItemDrop.ItemData) exactly (decompiled),
        // taking the station we already have instead of re-fetching it via
        // Player.GetCurrentCraftingStation().
        private static bool CanRepair(Player player, CraftingStation station, ItemDrop.ItemData item)
        {
            if (!item.m_shared.m_canBeReparied)
            {
                return false;
            }
            if (player.NoCostCheat())
            {
                return true;
            }

            Recipe recipe = ObjectDB.instance.GetRecipe(item);
            if (recipe == null)
            {
                return false;
            }
            if (recipe.m_craftingStation == null && recipe.m_repairStation == null)
            {
                return false;
            }

            bool eligible = (recipe.m_repairStation != null && recipe.m_repairStation.m_name == station.m_name)
                || (recipe.m_craftingStation != null && recipe.m_craftingStation.m_name == station.m_name)
                || item.m_worldLevel < Game.m_worldLevel;
            if (!eligible)
            {
                return false;
            }

            return Mathf.Min(station.GetLevel(), 4) >= recipe.m_minStationLevel;
        }
    }
}
