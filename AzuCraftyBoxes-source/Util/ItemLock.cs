namespace AzuCraftyBoxes.Util;

// Per-item "don't auto-move this" flag, stored in ItemDrop.ItemData's own
// m_customData (decompile-confirmed: a Dictionary<string,string> that's
// already part of the item's own save data -- same pattern this repo
// already uses for Player.m_customData's pull-prevention toggle, just on
// the item instead of the player). Moves with the item (inventory to
// inventory, through a chest, etc.) since it's carried in the item's own
// serialized data, not tracked externally.
public static class ItemLock
{
    private const string LockedKey = "ACB_ItemLocked";

    public static bool IsLocked(ItemDrop.ItemData item) =>
        item?.m_customData != null && item.m_customData.ContainsKey(LockedKey);

    public static bool ToggleLocked(ItemDrop.ItemData item)
    {
        if (IsLocked(item))
        {
            item.m_customData.Remove(LockedKey);
            return false;
        }

        item.m_customData[LockedKey] = "1";
        return true;
    }
}
