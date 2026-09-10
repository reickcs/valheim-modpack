using System.Linq;
using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Humanoid.EquipItem is shared by the player and AI-controlled humanoids
    // (Draugr, Fenring, ...); restrict to Player so monsters don't start
    // auto-equipping shields. One-handed weapons always land in the right
    // hand (confirmed in EquipItem's own branch for ItemType.OneHandedWeapon);
    // shields go in the left. Guarding on ItemType.OneHandedWeapon specifically
    // also makes this naturally non-recursive: our own call to equip the
    // shield re-enters this postfix with a Shield item, which doesn't match
    // and just returns.
    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    internal static class ShieldAutoEquipPatch
    {
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || !ValheimQoLPlugin.AutoEquipShieldWithOneHandedWeapon.Value)
            {
                return;
            }
            if (!(__instance is Player player))
            {
                return;
            }
            if (item?.m_shared == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon)
            {
                return;
            }
            if (player.LeftItem != null)
            {
                return;
            }

            ItemDrop.ItemData shield = player.GetInventory()
                .GetAllItemsOfType(ItemDrop.ItemData.ItemType.Shield)
                .FirstOrDefault(i => !i.m_equipped);
            if (shield != null)
            {
                player.EquipItem(shield);
            }
        }
    }
}
