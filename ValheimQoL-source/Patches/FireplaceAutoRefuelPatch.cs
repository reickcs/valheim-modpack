using HarmonyLib;
using UnityEngine;

namespace ValheimQoL.Patches
{
    // Attaches a small companion behaviour to every Fireplace instance that
    // periodically pulls fuel from nearby containers, mirroring V+'s
    // "automatic wood pulling from nearby chests" fire-source feature.
    [HarmonyPatch(typeof(Fireplace), "Awake")]
    internal static class FireplaceAutoRefuelPatch
    {
        private static void Postfix(Fireplace __instance)
        {
            if (ValheimQoLPlugin.FireplaceInfiniteFuel.Value)
            {
                __instance.m_infiniteFuel = true;
            }

            if (__instance.GetComponent<FireplaceAutoRefuel>() == null)
            {
                __instance.gameObject.AddComponent<FireplaceAutoRefuel>();
            }
        }
    }

    internal class FireplaceAutoRefuel : MonoBehaviour
    {
        private Fireplace _fireplace;
        private ZNetView _nview;

        private void Awake()
        {
            _fireplace = GetComponent<Fireplace>();
            _nview = GetComponent<ZNetView>();
            float interval = Mathf.Max(1f, ValheimQoLPlugin.FireplaceAutoRefuelInterval.Value);
            InvokeRepeating(nameof(TryRefuel), Random.Range(0f, interval), interval);
        }

        private void TryRefuel()
        {
            if (!ValheimQoLPlugin.FireplaceAutoRefuelEnabled.Value)
            {
                return;
            }
            if (_fireplace == null || _nview == null || !_nview.IsValid() || !_nview.IsOwner())
            {
                return;
            }
            if (_fireplace.m_infiniteFuel || !_fireplace.m_canRefill || _fireplace.m_fuelItem == null)
            {
                return;
            }

            float currentFuel = _nview.GetZDO().GetFloat(ZDOVars.s_fuel);
            if (currentFuel >= _fireplace.m_maxFuel)
            {
                return;
            }

            string fuelName = _fireplace.m_fuelItem.m_itemData.m_shared.m_name;
            float range = ValheimQoLPlugin.FireplaceAutoRefuelRange.Value;
            Collider[] hits = Physics.OverlapSphere(transform.position, range);
            foreach (Collider hit in hits)
            {
                Container container = hit.GetComponentInParent<Container>();
                if (container == null)
                {
                    continue;
                }
                Inventory inventory = container.GetInventory();
                if (inventory == null || !inventory.HaveItem(fuelName))
                {
                    continue;
                }
                inventory.RemoveItem(fuelName, 1);
                _fireplace.AddFuel(1f);
                return;
            }
        }
    }
}
