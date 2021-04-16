using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Humanoid), "EquipItem")]
        static class Humanoid_EquipItem_Patch
        {
            static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, ref ItemDrop.ItemData ___m_rightItem, ref ZNetView ___m_nview, ref VisEquipment ___m_visEquipment)
            {
                if (!__instance.name.Contains("Greyling")) return true;
                if (!__instance.IsTamed()) return true;

                ___m_rightItem = item;
                ___m_rightItem.m_equiped = item != null;
                ___m_visEquipment.SetRightItem(item?.m_dropPrefab?.name);
                Debug.Log($"Set right item prefab to {item?.m_dropPrefab?.name}");
                ___m_visEquipment.GetType().GetMethod("UpdateEquipmentVisuals", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(___m_visEquipment, new object[] { });
                return false;
            }

            private static bool HasAttachTransform(GameObject itemPrefab)
            {
                for (int i = 0; i < itemPrefab.transform.childCount; i++)
                {
                    var childTransform = itemPrefab.transform.GetChild(i);
                    if (childTransform.gameObject.name.Contains("attach"))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}