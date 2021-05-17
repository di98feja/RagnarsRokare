using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Character), "Damage")]
        static class Character_Damaged_Patch
        {
            static void Prefix(ref Character __instance, ref ZNetView ___m_nview, ref HitData hit)
            {
                var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_CharacterId);
                if (string.IsNullOrEmpty(uniqueId)) return;

                if (MobManager.IsAliveMob(uniqueId))
                {
                    var attacker = hit.GetAttacker();
                    if (attacker != null && attacker.IsPlayer())
                    {
                        hit.m_damage.Modify(0.1f);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        static class Character_Awake_Patch
        {
            static void Postfix(Character __instance, ref ZNetView ___m_nview)
            {
                if (MobConfigManager.IsControllableMob(__instance.name))
                {
                    string uniqueId = GetOrCreateUniqueId(___m_nview);
                    var mobInfo = MobConfigManager.GetMobConfig(__instance.name);
                    Tameable tameable = GetOrAddTameable(__instance);
                    tameable.m_tamingTime = mobInfo.TamingTime;
                    tameable.m_commandable = true;

                    AddVisualEquipmentCapability(__instance);

                    ___m_nview.Register<string, string>(Constants.Z_UpdateCharacterHUD, RPC_UpdateCharacterName);
                    var ai = __instance.GetBaseAI() as MonsterAI;
                    if (__instance.IsTamed())
                    {
                        try
                        {
                            MobManager.RegisterMob(__instance, uniqueId, mobInfo.AIType, mobInfo.AIConfig);
                        }
                        catch (ArgumentException e)
                        {
                            Debug.LogError($"Failed to register Mob AI ({mobInfo.AIType}). {e.Message}");
                            return;
                        }
                        __instance.m_faction = Character.Faction.Players;
                        ai.m_consumeItems.Clear();
                        ai.m_consumeItems.AddRange(mobInfo.PostTameConsumables);
                        ai.m_consumeSearchRange = GreylingsConfig.ItemSearchRadius.Value;
                        ai.m_randomMoveRange = 20;
                        ai.m_randomMoveInterval = 5;
                        string givenName = ___m_nview?.GetZDO()?.GetString(Constants.Z_GivenName);
                        if (!string.IsNullOrEmpty(givenName))
                        {
                            __instance.m_name = givenName;
                        }
                    }
                    else
                    {
                        tameable.m_fedDuration = mobInfo.PreTameFeedDuration;
                        ai.m_consumeItems.Clear();
                        ai.m_consumeItems.AddRange(mobInfo.PreTameConsumables);
                    }
                }
            }

            private static Tameable GetOrAddTameable(Character __instance)
            {
                var tameable = __instance.gameObject.GetComponent<Tameable>();
                if (tameable == null)
                {
                    tameable = __instance.gameObject.AddComponent<Tameable>();
                }

                return tameable;
            }

            private static string GetOrCreateUniqueId(ZNetView ___m_nview)
            {
                var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_CharacterId);
                if (string.IsNullOrEmpty(uniqueId))
                {
                    uniqueId = System.Guid.NewGuid().ToString();
                    ___m_nview.GetZDO().Set(Constants.Z_CharacterId, uniqueId);
                }
                return uniqueId;
            }

            public static void AddVisualEquipmentCapability(Character __instance)
            {
                var visEquipment = __instance.gameObject.GetComponent<VisEquipment>();
                if (visEquipment == null)
                {
                    __instance.gameObject.AddComponent<VisEquipment>();
                    visEquipment = __instance.gameObject.GetComponent<VisEquipment>();
                    //_NetSceneRoot/Greyling(Clone)/Visual/Armature.001/root/spine1/spine2/spine3/r_shoulder/r_arm1/r_arm2/r_hand
                    var rightHand = __instance.gameObject.GetComponentsInChildren<Transform>().Where(c => c.name == "r_hand").Single();
                    visEquipment.m_rightHand = rightHand;
                }
            }

            public static void BroadcastUpdateCharacterName(ref ZNetView nview, string text)
            {
                nview.InvokeRPC(ZNetView.Everybody, Constants.Z_UpdateCharacterHUD, nview.GetZDO().GetString(Constants.Z_CharacterId), text);
            }

            public static void RPC_UpdateCharacterName(long sender, string uniqueId, string text)
            {
                if (!MobManager.IsAliveMob(uniqueId)) return;
                Character greylingToUpdate;
                try
                {
                    greylingToUpdate = MobManager.AliveMobs[uniqueId].Character;
                }
                catch (System.Exception)
                { 
                    return; 
                }

                greylingToUpdate.m_name = text;
                var hudsDictObject = EnemyHud.instance.GetType().GetField("m_huds", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(EnemyHud.instance);
                var hudsDict = hudsDictObject as System.Collections.IDictionary;
                if (!hudsDict.Contains(greylingToUpdate)) return;
                var hudObject = hudsDict[greylingToUpdate];
                var hudText = hudObject.GetType().GetField("m_name", BindingFlags.Public | BindingFlags.Instance).GetValue(hudObject) as Text;
                if (hudText == null) return;
                hudText.text = text;
            }
        }
    }
}
