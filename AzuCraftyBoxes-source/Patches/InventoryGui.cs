using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using TMPro;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirementList))]
static class InventoryGuiCollectRequirements
{
    public static Dictionary<Piece.Requirement, int> actualAmounts = new();

    private static void Prefix(InventoryGui __instance)
    {
        actualAmounts.Clear();

        if (!MiscFunctions.ShouldPrevent() && Player.m_localPlayer)
        {
            var near = Boxes.QueryFrame.Get(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            UiItemBank.Begin(near);
        }
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
static class InventoryGuiSetupRequirementPatch
{
    static void Postfix(InventoryGui __instance, Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier = 1)
    {
        if (MiscFunctions.ShouldPrevent() || req?.m_resItem?.m_itemData?.m_shared == null) return;

        var text = elementRoot.transform.Find("res_amount")?.GetComponent<TextMeshProUGUI>();
        if (!text) return;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10;
        text.fontSizeMax = 16f;

        if (!int.TryParse(text.text, out int amount))
            amount = req.GetAmount(quality) * craftMultiplier;
        if (amount <= 0) return;

        string sharedName = req.m_resItem.m_itemData.m_shared.m_name;

        // Count once via bank -- prefab name needed so the bank applies the
        // same yaml pull rules the actual craft gate does (PlayerPatches.cs'
        // HaveRequirementItems); otherwise this can flash "available" for an
        // item a chest's config excludes, while the Craft button stays
        // disabled because the gate correctly filtered it out.
        int have = UiItemBank.GetTotalAnyQuality(sharedName, req.m_resItem.name);

        if (have >= amount)
        {
            text.color = (Mathf.Sin(Time.time * 10f) > 0f)
                ? AzuCraftyBoxesPlugin.flashColor.Value
                : AzuCraftyBoxesPlugin.unFlashColor.Value;

            InventoryGuiCollectRequirements.actualAmounts[req] = amount;
        }

        string haveStr = FormatThousands(have);
        text.text = AzuCraftyBoxesPlugin.resourceString.Value.Trim().Length > 0
            ? string.Format(AzuCraftyBoxesPlugin.resourceString.Value, haveStr, amount)
            : amount.ToString();
    }

    public static string FormatThousands(int number) => 
        number < 1000 
            ? number.ToString() 
            : (number < 1_000_000 
                ? (number / 1000.0).ToString("0.#") + "K" 
                : (number / 1_000_000.0).ToString("0.#") + "M");
}