using HarmonyLib;
using System.Reflection;

namespace RagnarsRokare.MobAI
{
    public partial class MobAILib
    {
        [HarmonyPatch(typeof(Humanoid), "EquipItem")]
        static class Humanoid_EquipItem_Patch
        {
            static bool Prefix(ItemDrop.ItemData item, ref ItemDrop.ItemData ___m_rightItem, ref ZNetView ___m_nview, ref VisEquipment ___m_visEquipment)
            {
                if (!___m_nview.IsValid() || !___m_nview.IsOwner()) return true;
                if (!MobManager.IsControlledMob(___m_nview.GetZDO().GetString(Constants.Z_CharacterId))) return true;

                ___m_rightItem = item;
                ___m_rightItem.m_equiped = item != null;
                ___m_visEquipment.SetRightItem(item?.m_dropPrefab?.name);
                ___m_visEquipment.GetType().GetMethod("UpdateEquipmentVisuals", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(___m_visEquipment, new object[] { });
                return false;
            }
        }
    }
}