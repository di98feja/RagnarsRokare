using BepInEx;
using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace SlaveGreylings
{
    [BepInPlugin("RagnarsRokare.SlaveGreylings", "SlaveGreylings", "0.4")]
    public partial class SlaveGreylings : BaseUnityPlugin
    {

        private static Character m_attacker = null;

        private void Awake()
        {
            GreylingsConfig.Init(Config);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }



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

        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        static class MonsterAI_MakeTame_Patch
        {
            static void Postfix(MonsterAI __instance)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    __instance.m_consumeItems.Clear();
                    __instance.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Resin").FirstOrDefault());
                    __instance.m_consumeSearchRange = 50;
                }
            }
        }

        class MyTextReceiver : TextReceiver
        {
            private ZNetView m_nview;
            private readonly Character m_character;

            public MyTextReceiver(Character character)
            {
                this.m_nview = character.GetComponent<ZNetView>();
                this.m_character = character;
            }

            public string GetText()
            {
                return m_nview.GetZDO().GetString(Constants.Z_GivenName);
            }

            public void SetText(string text)
            {
                m_nview.ClaimOwnership();
                m_nview.GetZDO().Set(Constants.Z_GivenName, text);
                Character_Awake_Patch.BroadcastUpdateCharacterName(ref m_nview, text);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "AttachItem")]
        static class VisEquipment_AttachItem_Patch
        {
            public static bool Prefix(VisEquipment __instance, int itemHash, int variant, Transform joint, ref GameObject __result, ref SkinnedMeshRenderer ___m_bodyModel, bool enableEquipEffects = true)
            {
                if (!__instance.name.Contains("Greyling")) return true;

                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemHash);
                if (itemPrefab == null)
                {
                    __result = null;
                    return false;
                }
                if (itemPrefab.transform.childCount == 0)
                {
                    __result = null;
                    return false;
                }

                Transform child = null;
                for (int i = 0; i < itemPrefab.transform.childCount; i++)
                {
                    child = itemPrefab.transform.GetChild(i);
                    if (child.gameObject.name == "attach" || child.gameObject.name == "attach_skin")
                    {
                        break;
                    }
                }

                if (null == child)
                {
                    child = itemPrefab.transform.GetChild(0);
                }

                var gameObject = child.gameObject;
                if (gameObject == null)
                {
                    __result = null;
                    return false;
                }
                GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
                gameObject2.SetActive(value: true);
                Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>();
                for (int i = 0; i < componentsInChildren.Length; i++)
                {
                    componentsInChildren[i].enabled = false;
                }
                gameObject2.transform.SetParent(joint);
                gameObject2.transform.localPosition = Vector3.zero;
                gameObject2.transform.localRotation = Quaternion.identity;

                __result = gameObject2;
                return false;
            }
        }

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
        [HarmonyPatch(typeof(Tameable), "GetHoverText")]
        static class Tameable_GetHoverName_Patch
        {
            static bool Prefix(Tameable __instance, ref string __result, ZNetView ___m_nview, Character ___m_character)
            {
                if (!__instance.name.Contains("Greyling")) return true;
                if (!___m_character.IsTamed()) return true;
                if (!___m_nview.IsValid())
                {
                    __result = string.Empty;
                    return true;
                }
                string aiStatus = ___m_nview.GetZDO().GetString(Constants.Z_AiStatus) ?? Traverse.Create(__instance).Method("GetStatusString").GetValue() as string;
                string str = Localization.instance.Localize(___m_character.GetHoverName());
                str += Localization.instance.Localize(" ( $hud_tame, " + aiStatus + " )");
                __result = str + Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] $hud_pet" + "\n[<color=yellow>Hold E</color>] to change name");

                return false;
            }
        }
        [HarmonyPatch(typeof(Tameable), "Interact")]
        static class Tameable_Interact_Patch
        {
            static bool Prefix(Tameable __instance, ref bool __result, Humanoid user, bool hold, ZNetView ___m_nview, ref Character ___m_character,
                ref float ___m_lastPetTime)
            {
                if (!__instance.name.Contains("Greyling")) return true;

                if (!___m_nview.IsValid())
                {
                    __result = false;
                    return true;
                }
                string hoverName = ___m_character.GetHoverName();
                if (___m_character.IsTamed())
                {
                    if (hold)
                    {
                        TextInput.instance.RequestText(new MyTextReceiver(___m_character), "Name", 15);
                        __result = false;
                        return false;
                    }

                    if (Time.time - ___m_lastPetTime > 1f)
                    {
                        ___m_lastPetTime = Time.time;
                        __instance.m_petEffect.Create(___m_character.GetCenterPoint(), Quaternion.identity);
                        if (__instance.m_commandable)
                        {
                            typeof(Tameable).GetMethod("Command", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { user });
                        }
                        else
                        {
                            user.Message(MessageHud.MessageType.Center, hoverName + " $hud_tamelove");
                        }
                        __result = true;
                        return false;
                    }
                    __result = false;
                    return false;
                }
                __result = false;
                return false;
            }
        }
    }
}