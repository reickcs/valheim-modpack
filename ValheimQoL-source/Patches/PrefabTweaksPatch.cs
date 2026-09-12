using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimQoL.Patches
{
    // ZNetScene.m_prefabs is the master list of every networked prefab the game
    // knows about (items, pieces, crafting stations, resource nodes, ...),
    // populated once in ZNetScene.Awake. Walking it once here lets us apply
    // blanket tweaks without patching dozens of individual call sites.
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class PrefabTweaksPatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            float stackMult = ValheimQoLPlugin.StackSizeMultiplier.Value;
            float rangeMult = ValheimQoLPlugin.WorkbenchRangeMultiplier.Value;
            bool ignoreRoof = ValheimQoLPlugin.WorkbenchIgnoreRoofRequirement.Value;
            bool ignoreSupport = ValheimQoLPlugin.BuildingIgnoreSupport.Value;
            bool ignoreWeatherDecay = ValheimQoLPlugin.BuildingIgnoreWeatherDecay.Value;
            float prodSpeedMult = ValheimQoLPlugin.ProductionSpeedMultiplier.Value;
            float prodCapMult = ValheimQoLPlugin.ProductionCapacityMultiplier.Value;
            float yieldMult = ValheimQoLPlugin.GatheringYieldMultiplier.Value;
            bool allowOreThroughPortals = ValheimQoLPlugin.AllowOreThroughPortals.Value;
            bool kilnWoodOnly = ValheimQoLPlugin.KilnWoodOnly.Value;

            foreach (GameObject prefab in __instance.m_prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                if (stackMult != 1f || allowOreThroughPortals)
                {
                    ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                    if (itemDrop != null && itemDrop.m_itemData?.m_shared != null)
                    {
                        ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
                        if (stackMult != 1f && shared.m_maxStackSize > 1)
                        {
                            shared.m_maxStackSize = Mathf.Max(1, Mathf.RoundToInt(shared.m_maxStackSize * stackMult));
                        }
                        if (allowOreThroughPortals)
                        {
                            shared.m_teleportable = true;
                        }
                    }
                }

                if (rangeMult != 1f || ignoreRoof)
                {
                    CraftingStation station = prefab.GetComponent<CraftingStation>();
                    if (station != null)
                    {
                        if (rangeMult != 1f)
                        {
                            station.m_rangeBuild *= rangeMult;
                            // Set equal to the scaled build range rather than scaling
                            // discoverRange from its own (smaller) vanilla base -- vanilla
                            // defaults are m_rangeBuild=10, m_discoverRange=4, so even at
                            // the same multiplier they'd never actually match. One range
                            // covering both discovery and building, on request.
                            station.m_discoverRange = station.m_rangeBuild;
                        }
                        if (ignoreRoof)
                        {
                            station.m_craftRequireRoof = false;
                        }
                    }
                }

                if (ignoreSupport || ignoreWeatherDecay)
                {
                    WearNTear wearNTear = prefab.GetComponent<WearNTear>();
                    if (wearNTear != null)
                    {
                        if (ignoreSupport)
                        {
                            wearNTear.m_noSupportWear = false;
                        }
                        if (ignoreWeatherDecay)
                        {
                            wearNTear.m_noRoofWear = false;
                        }
                    }
                }

                if (prodSpeedMult != 1f || prodCapMult != 1f)
                {
                    Smelter smelter = prefab.GetComponent<Smelter>();
                    if (smelter != null)
                    {
                        if (prodSpeedMult != 1f)
                        {
                            smelter.m_secPerProduct *= prodSpeedMult;
                        }
                        if (prodCapMult != 1f)
                        {
                            smelter.m_maxOre = Mathf.Max(1, Mathf.RoundToInt(smelter.m_maxOre * prodCapMult));
                            // m_maxFuel == 0 means "this station doesn't use fuel at all" (e.g.
                            // the Charcoal Kiln, self-fueled by the wood it converts) -- decompile-
                            // confirmed via Smelter's own `m_maxFuel != 0` gate on its production
                            // check. Only scale it if the station already uses fuel; Max(1, ...)
                            // unconditionally was forcing a fuel requirement onto kilns that vanilla
                            // never gave one, and nothing can ever fill it, permanently blocking them.
                            if (smelter.m_maxFuel > 0)
                            {
                                smelter.m_maxFuel = Mathf.Max(1, Mathf.RoundToInt(smelter.m_maxFuel * prodCapMult));
                            }
                        }
                    }

                    Fermenter fermenter = prefab.GetComponent<Fermenter>();
                    if (fermenter != null && prodSpeedMult != 1f)
                    {
                        fermenter.m_fermentationDuration *= prodSpeedMult;
                    }

                    Beehive beehive = prefab.GetComponent<Beehive>();
                    if (beehive != null)
                    {
                        if (prodSpeedMult != 1f)
                        {
                            beehive.m_secPerUnit *= prodSpeedMult;
                        }
                        if (prodCapMult != 1f)
                        {
                            beehive.m_maxHoney = Mathf.Max(1, Mathf.RoundToInt(beehive.m_maxHoney * prodCapMult));
                        }
                    }

                    CookingStation cookingStation = prefab.GetComponent<CookingStation>();
                    if (cookingStation != null && prodSpeedMult != 1f)
                    {
                        cookingStation.m_secPerFuel = Mathf.Max(1, Mathf.RoundToInt(cookingStation.m_secPerFuel * prodSpeedMult));
                    }
                }

                if (kilnWoodOnly && prefab.name == "charcoal_kiln")
                {
                    Smelter kiln = prefab.GetComponent<Smelter>();
                    if (kiln != null && kiln.m_conversion.Count > 1)
                    {
                        // The Charcoal Kiln converts wood into Coal via its ORE slot
                        // (m_conversion), not the fuel slot -- decompile-confirmed
                        // m_maxFuel==0 means it has no fuel slot at all (see the
                        // prodCapMult comment above). If Core Wood/Fine Wood/any
                        // other wood-type item is also listed as a valid conversion
                        // entry, strip everything except the one literally named
                        // "Wood" (regular wood), on request. Only touches the list
                        // when a "Wood" entry is actually present, so a wrong
                        // assumption about that name can't zero out every entry and
                        // brick the kiln -- it just leaves the list untouched instead.
                        string found = string.Join(", ", kiln.m_conversion.Select(c => c.m_from != null ? c.m_from.gameObject.name : "<null>"));
                        var woodOnly = kiln.m_conversion
                            .Where(c => c.m_from != null && c.m_from.gameObject.name == "Wood")
                            .ToList();
                        if (woodOnly.Count > 0)
                        {
                            kiln.m_conversion = woodOnly;
                            ValheimQoLPlugin.Log.LogInfo($"KilnWoodOnly: restricted charcoal_kiln conversion from [{found}] to Wood only.");
                        }
                        else
                        {
                            ValheimQoLPlugin.Log.LogWarning($"KilnWoodOnly: no entry named \"Wood\" found in charcoal_kiln conversion [{found}] -- left untouched.");
                        }
                    }
                }

                if (yieldMult != 1f)
                {
                    TreeBase treeBase = prefab.GetComponent<TreeBase>();
                    if (treeBase != null && treeBase.m_dropWhenDestroyed != null)
                    {
                        ScaleDropTable(treeBase.m_dropWhenDestroyed, yieldMult);
                    }

                    MineRock mineRock = prefab.GetComponent<MineRock>();
                    if (mineRock != null && mineRock.m_dropItems != null)
                    {
                        ScaleDropTable(mineRock.m_dropItems, yieldMult);
                    }

                    MineRock5 mineRock5 = prefab.GetComponent<MineRock5>();
                    if (mineRock5 != null && mineRock5.m_dropItems != null)
                    {
                        ScaleDropTable(mineRock5.m_dropItems, yieldMult);
                    }

                    Pickable pickable = prefab.GetComponent<Pickable>();
                    if (pickable != null)
                    {
                        pickable.m_amount = Mathf.Max(1, Mathf.RoundToInt(pickable.m_amount * yieldMult));
                    }
                }
            }
        }

        private static void ScaleDropTable(DropTable table, float mult)
        {
            table.m_dropMin = Mathf.Max(1, Mathf.RoundToInt(table.m_dropMin * mult));
            table.m_dropMax = Mathf.Max(table.m_dropMin, Mathf.RoundToInt(table.m_dropMax * mult));
        }
    }
}
