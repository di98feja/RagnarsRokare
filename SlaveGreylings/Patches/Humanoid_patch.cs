using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Reflection;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Humanoid), "EquipItem")]
        public static class Humanoid_EquipItem_Patch
        {
            public static bool Prefix(ItemDrop.ItemData item, ref ItemDrop.ItemData ___m_rightItem, ref ZNetView ___m_nview, ref VisEquipment ___m_visEquipment)
            {
                if (!___m_nview.IsValid() || !___m_nview.IsOwner()) return true;
                if (!MobManager.IsAliveMob(___m_nview.GetZDO().GetString(Constants.Z_UniqueId))) return true;

                ___m_rightItem = item;
                ___m_rightItem.m_equiped = item != null;
                ___m_visEquipment.SetRightItem(item?.m_dropPrefab?.name);
                ___m_visEquipment.GetType().GetMethod("UpdateEquipmentVisuals", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(___m_visEquipment, new object[] { });
                return false;
            }
        }
    }
}