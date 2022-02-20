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
                    ___m_character.m_faction = Character.Faction.Players;
                    string uniqueId = GetOrCreateUniqueId(___m_nview);

                    AddVisualEquipmentCapability(___m_character);
                    ___m_nview.Register<string, string>(Constants.Z_UpdateCharacterHUD, RPC_UpdateCharacterName);

                    try
                    {
                        MobManager.RegisterMob(___m_character, uniqueId, mobInfo.AIType, mobInfo.AIConfig);
                    }
                    catch (ArgumentException e)
                    {
                        Debug.LogError($"Failed to register Mob AI ({mobInfo.AIType}). {e.Message}");
                        return;
                    }
                    ___m_character.m_faction = Character.Faction.Players;
                    var ai = ___m_character.GetBaseAI() as MonsterAI; 
                    ai.m_consumeItems.Clear();
                    ai.m_consumeItems.AddRange(mobInfo.PostTameConsumables);
                    ai.m_consumeSearchRange = mobInfo.AIConfig.Awareness * 5;
                    ai.m_randomMoveRange = mobInfo.AIConfig.Mobility * 2;
                    ai.m_randomMoveInterval = 15 - mobInfo.AIConfig.Mobility;
                }
            }
        }
    }
}
