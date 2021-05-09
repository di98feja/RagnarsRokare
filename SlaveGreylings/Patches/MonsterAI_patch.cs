using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        static class MonsterAI_MakeTame_Patch
        {
            static void Postfix(MonsterAI __instance, ZNetView ___m_nview, Character ___m_character)
            {
                if (MobConfigManager.IsControllableMob(__instance.name))
                {
                    var mobInfo = MobConfigManager.GetMobConfig(__instance.name);
                    __instance.m_consumeItems.Clear();
                    __instance.m_consumeItems.AddRange(mobInfo.PostTameConsumables);
                    __instance.m_consumeSearchRange = 50;
                    try
                    {
                        var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_CharacterId);
                        MobManager.RegisterMob(___m_character, uniqueId, mobInfo.AIType, mobInfo.AIConfig);
                    }
                    catch (ArgumentException e)
                    {
                        Debug.LogError($"Failed to register Mob AI ({mobInfo.AIType}). {e.Message}");
                        return;
                    }

                }
            }
        }
    }
}
