using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace SlaveGreylings
{
    public partial class SlaveGreylings
    {
        private static Character m_attacker = null;

        [HarmonyPatch(typeof(Character), "Damage")]
        static class Character_Damaged_Patch
        {
            static void Prefix(ref Character __instance, ref HitData hit)
            {
                if (__instance.name.Contains("Greyling") && __instance.IsTamed())
                {
                    m_attacker = hit.GetAttacker();
                    if (m_attacker != null && m_attacker.IsPlayer())
                    {
                        hit.m_damage.Modify(0.1f);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        static class Character_Awake_Patch
        {
            private static Dictionary<string, int> m_allNamedMobs = new Dictionary<string, int>();

            static void Postfix(Character __instance, ref ZNetView ___m_nview)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    Debug.Log($"A {__instance.name} just spawned!");
                    var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_CharacterId);
                    if (string.IsNullOrEmpty(uniqueId))
                    {
                        uniqueId = System.Guid.NewGuid().ToString();
                        ___m_nview.GetZDO().Set(Constants.Z_CharacterId, uniqueId);
                    }
                    var tameable = __instance.gameObject.GetComponent<Tameable>();
                    if (tameable == null)
                    {
                        tameable = __instance.gameObject.AddComponent<Tameable>();
                    }

                    tameable.m_fedDuration = (float)GreylingsConfig.FeedDuration.Value;
                    tameable.m_tamingTime = (float)GreylingsConfig.TamingTime.Value;

                    tameable.m_commandable = true;

                    var visEquipment = __instance.gameObject.GetComponent<VisEquipment>();
                    if (visEquipment == null)
                    {
                        __instance.gameObject.AddComponent<VisEquipment>();
                        visEquipment = __instance.gameObject.GetComponent<VisEquipment>();
                        //_NetSceneRoot/Greyling(Clone)/Visual/Armature.001/root/spine1/spine2/spine3/r_shoulder/r_arm1/r_arm2/r_hand
                        var rightHand = __instance.gameObject.GetComponentsInChildren<Transform>().Where(c => c.name == "r_hand").Single();
                        visEquipment.m_rightHand = rightHand;
                    }

                    if (m_allNamedMobs.ContainsKey(uniqueId))
                    {
                        m_allNamedMobs[uniqueId] = __instance.GetInstanceID();
                    }
                    else
                    {
                        m_allNamedMobs.Add(uniqueId, __instance.GetInstanceID());
                    }
                    ___m_nview.Register<string, string>(Constants.Z_UpdateCharacterHUD, RPC_UpdateCharacterName);

                    var ai = __instance.GetBaseAI() as MonsterAI;
                    if (__instance.IsTamed())
                    {
                        ai.m_consumeItems.Clear();
                        ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Resin").FirstOrDefault());
                        ai.m_randomMoveRange = 5;
                        ai.m_consumeSearchRange = 50;
                        string givenName = ___m_nview?.GetZDO()?.GetString(Constants.Z_GivenName);
                        if (!string.IsNullOrEmpty(givenName))
                        {
                            __instance.m_name = givenName;
                        }
                    }
                    else
                    {
                        ai.m_consumeItems.Clear();
                        var tamingItemNames = GreylingsConfig.TamingItemList.Value.Split(',');
                        foreach (string consumeItem in tamingItemNames)
                        {
                            ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, consumeItem).FirstOrDefault());
                        }
                    }
                }
            }

            public static void BroadcastUpdateCharacterName(ref ZNetView nview, string text)
            {
                nview.InvokeRPC(ZNetView.Everybody, Constants.Z_UpdateCharacterHUD, nview.GetZDO().GetString(Constants.Z_CharacterId), text);
            }

            public static void RPC_UpdateCharacterName(long sender, string uniqueId, string text)
            {
                if (!m_allNamedMobs.ContainsKey(uniqueId)) return;

                var greylingToUpdate = Character.GetAllCharacters().Where(c => c.GetInstanceID() == m_allNamedMobs[uniqueId]).FirstOrDefault();
                if (null == greylingToUpdate) return;
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
