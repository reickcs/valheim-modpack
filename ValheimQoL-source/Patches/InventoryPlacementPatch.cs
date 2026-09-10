using HarmonyLib;

namespace ValheimQoL.Patches
{
    // Inventory.TopFirst(item) decides whether a picked-up item fills from the
    // first grid slot or the last, per item type. Forcing it true replicates
    // V+'s "place new items in the first slot" inventory behaviour.
    [HarmonyPatch(typeof(Inventory), "TopFirst")]
    internal static class InventoryPlacementPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (ValheimQoLPlugin.InventoryFillFirstSlot.Value)
            {
                __result = true;
            }
        }
    }
}
