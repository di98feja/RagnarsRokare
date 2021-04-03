using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace SlaveGreylings
{
    [BepInPlugin("RagnarsRokare.SlaveGreylings", "SlaveGreylings", "0.1")]
    public class SlaveGreylings : BaseUnityPlugin
    {
        private const string GivenName = "RR_GivenName";
        private const string AiStatus = "RR_AiStatus";

        private static readonly bool isDebug = false;

        public static ConfigEntry<string> lastServerIPAddress;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(SlaveGreylings).Namespace + " " : "") + str);
        }

        private void Awake()
        {
            lastServerIPAddress = Config.Bind<string>("General", "LastUsedServerIPAdress", "", "The last used IP adress of server");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private static string GetPrefabName(string name)
        {
            char[] anyOf = new char[] { '(', ' ' };
            int num = name.IndexOfAny(anyOf);
            string result;
            if (num >= 0)
                result = name.Substring(0, num);
            else
                result = name;
            return result;
        }

        [HarmonyPatch(typeof(BaseAI), "UpdateAI")]
        class BaseAI_UpdateAI_ReversePatch
        {
            [HarmonyReversePatch]
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void UpdateAI(BaseAI instance, float dt, ZNetView m_nview, ref float m_jumpInterval, ref float m_jumpTimer,
                ref float m_randomMoveUpdateTimer, ref float m_timeSinceHurt, ref bool m_alerted)
            {
                if (m_nview.IsOwner())
                {
                    instance.UpdateTakeoffLanding(dt);
                    if (m_jumpInterval > 0f)
                    {
                        m_jumpTimer += dt;
                    }
                    if (m_randomMoveUpdateTimer > 0f)
                    {
                        m_randomMoveUpdateTimer -= dt;
                    }
                    typeof(BaseAI).GetMethod("UpdateRegeneration", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { dt });
                    m_timeSinceHurt += dt;
                }
                else
                {
                    m_alerted = m_nview.GetZDO().GetBool("alert");
                }
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        static class MonsterAI_UpdateAI_Patch
        {
            public static string UpdateAiStatus(ZNetView nview, string newStatus)
            {
                string currentAiStatus = nview?.GetZDO()?.GetString(AiStatus);
                if (currentAiStatus != newStatus)
                {
                    string name = nview?.GetZDO()?.GetString(GivenName);
                    Debug.Log($"{name}: {newStatus}");
                    nview.GetZDO().Set(AiStatus, newStatus);
                }
                return newStatus;
            }

            static MonsterAI_UpdateAI_Patch()
            {
                m_assignment = new Dictionary<int, MaxStack<GameObject>>();
                m_assigned = new Dictionary<int, bool>();
                m_container1 = new Dictionary<int, Container>();
                m_container2 = new Dictionary<int, Container>();
                m_container3 = new Dictionary<int, Container>();
                m_container4 = new Dictionary<int, Container>();
                m_container5 = new Dictionary<int, Container>();
                m_fetchitems = new Dictionary<int, List<string>>();
                m_carrying = new Dictionary<int, ItemDrop.ItemData>();
                m_spottedItem = new Dictionary<int, ItemDrop>();
                m_aiStatus = new Dictionary<int, string>();
            }
            public static Dictionary<int, MaxStack<GameObject>> m_assignment;

            public static Dictionary<int, bool> m_assigned;
            public static Dictionary<int, Container> m_container1;
            public static Dictionary<int, Container> m_container2;
            public static Dictionary<int, Container> m_container3;
            public static Dictionary<int, Container> m_container4;
            public static Dictionary<int, Container> m_container5;
            public static Dictionary<int, List<string>> m_fetchitems;
            public static Dictionary<int, ItemDrop.ItemData> m_carrying;
            public static Dictionary<int, ItemDrop> m_spottedItem;
            public static Dictionary<int, string> m_aiStatus;

            private static Character m_attacker = null;
            private static List<string> m_fireplaces = new List<string>() { "fire_pit", "groundtorch", "walltorch" };
            private static List<string> m_smelters = new List<string>() { "smelter", "charcoal_kiln" };

            static bool Prefix(MonsterAI __instance, float dt, ref ZNetView ___m_nview, ref Character ___m_character, ref float ___m_fleeIfLowHealth,
                ref float ___m_timeSinceHurt, ref string ___m_aiStatus, ref Vector3 ___arroundPointTarget, ref float ___m_jumpInterval, ref float ___m_jumpTimer,
                ref float ___m_randomMoveUpdateTimer, ref bool ___m_alerted, ref Tameable ___m_tamable)
            {
                if (!___m_nview.IsOwner())
                {
                    return false;
                }
                if (!___m_character.IsTamed())
                {
                    return true;
                }
                if (!__instance.name.Contains("Greyling"))
                {
                    return true;
                }
                if (__instance.IsSleeping())
                {
                    Invoke(__instance, "UpdateSleep", new object[] { dt });
                    Dbgl($"{___m_character.GetHoverName()}: Sleep updated");
                    return false;
                }

                BaseAI_UpdateAI_ReversePatch.UpdateAI(__instance, dt, ___m_nview, ref ___m_jumpInterval, ref ___m_jumpTimer, ref ___m_randomMoveUpdateTimer, ref ___m_timeSinceHurt, ref ___m_alerted);

                int instanceId = InitInstanceIfNeeded(__instance);
                Dbgl("GetInstanceID ok");

                ___m_aiStatus = "";

                if (___m_character.GetHealthPercentage() < ___m_fleeIfLowHealth && ___m_timeSinceHurt < 20f && m_attacker != null)
                {
                    Invoke(__instance, "Flee", new object[] { dt, m_attacker.transform.position });
                    ___m_aiStatus = UpdateAiStatus(___m_nview, "Low health, flee");
                    return false;
                }
                if ((bool)__instance.GetFollowTarget())
                {
                    Invoke(__instance, "Follow", new object[] { __instance.GetFollowTarget(), dt });
                    ___m_aiStatus = UpdateAiStatus(___m_nview, "Follow");
                    m_fetchitems[instanceId].Clear();
                    m_assigned[instanceId] = false;
                    m_spottedItem[instanceId] = null;
                    return false;
                }
                if (m_assignment[instanceId].Any() && AvoidFire(__instance, dt, m_assignment[instanceId].Peek().transform.position))
                {
                    ___m_aiStatus = UpdateAiStatus(___m_nview, "Avoiding fire");
                    if (Vector3.Distance(___m_character.transform.position, m_assignment[instanceId].Peek().transform.position) < 5.0f)
                    {
                        m_assigned[instanceId] = false;
                    }
                    return false;
                }
                if (!__instance.IsAlerted() && (bool)Invoke(__instance, "UpdateConsumeItem", new object[] { ___m_character as Humanoid, dt }))
                {
                    ___m_aiStatus = UpdateAiStatus(___m_nview, "Consume item");
                    return false;
                }
                if (___m_tamable.IsHungry())
                {
                    ___m_aiStatus = UpdateAiStatus(___m_nview, "Is hungry, no work a do");

                    return false;
                }

                if (!m_assigned[instanceId])
                {
                    foreach (Collider collider in Physics.OverlapSphere(___m_character.transform.position, 50, LayerMask.GetMask(new string[] { "piece" })))
                    {
                        Smelter smelter = collider.transform?.parent?.gameObject.GetComponent<Smelter>();
                        Fireplace fireplace = collider?.gameObject?.GetComponent<Fireplace>();
                        Fireplace torch = collider.transform?.parent?.gameObject.GetComponent<Fireplace>();
                        GameObject gameObject = null;
                        if ((smelter?.GetComponent<ZNetView>()?.IsValid() ?? false) || (torch?.GetComponent<ZNetView>()?.IsValid() ?? false))
                        {
                            gameObject = collider.transform?.parent?.gameObject;
                        }
                        else if (fireplace?.GetComponent<ZNetView>()?.IsValid() ?? false)
                        {
                            gameObject = collider?.gameObject;
                        }
                        else
                        {
                            continue;
                        }
                        if (gameObject.transform.position != null && !m_assignment[instanceId].Contains(gameObject))
                        {
                            m_assignment[instanceId].Push(gameObject);
                            m_assigned[instanceId] = true;
                            ___m_aiStatus = UpdateAiStatus(___m_nview, $"Doing assignment: {gameObject.GetComponent<ZNetView>().GetPrefabName()}");

                            return false;
                        }
                    }
                    m_assignment[instanceId].Clear();
                }
                if (m_assigned[instanceId])
                {
                    GameObject assignment = m_assignment[instanceId].Peek();
                    Vector3 assignmentPosition = assignment.transform.position;
                    Smelter smelter = assignment?.GetComponent<Smelter>();
                    Fireplace fireplace = assignment?.GetComponent<Fireplace>();

                    bool fireplaceAssignment = fireplace?.GetComponent<ZNetView>()?.IsValid() == true;
                    bool smelterAssignment = smelter?.GetComponent<ZNetView>()?.IsValid() == true;
                    if (smelterAssignment)
                    {
                        assignmentPosition = smelter.m_outputPoint.position;
                    }

                    bool isCloseToAssignment = Vector3.Distance(___m_character.transform.position, assignmentPosition) < 2.0f;
                    if ((!m_fetchitems[instanceId].Any() || m_carrying[instanceId] != null) && m_spottedItem[instanceId] == null && !isCloseToAssignment)
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, $"Move To Assignment: {assignment.GetComponent<ZNetView>().GetPrefabName()} ");
                        Invoke(__instance, "MoveAndAvoid", new object[] { dt, assignmentPosition, 0.5f, false });
                        return false;
                    }

                    if (m_carrying[instanceId] != null && isCloseToAssignment)
                    {
                        var humanoid = ___m_character as Humanoid;
                        bool isCarryingFuel = false;
                        bool isCarryingMatchingOre = false;
                        bool OreisNotFull = false;
                        bool FuelisNotFull = false;

                        if (smelterAssignment)
                        {
                            isCarryingFuel = smelterAssignment && smelter.m_maxFuel > 0 && m_carrying[instanceId].m_dropPrefab.name == smelter.m_fuelItem.gameObject.name;
                            isCarryingMatchingOre = smelterAssignment && smelter.m_conversion.Any(c => m_carrying[instanceId].m_dropPrefab.name == c.m_from.gameObject.name);
                            OreisNotFull = Traverse.Create(smelter).Method("GetQueueSize").GetValue<int>() < smelter.m_maxOre;
                            FuelisNotFull = Mathf.CeilToInt(smelter.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f)) < smelter.m_maxFuel;
                        }

                        if (fireplaceAssignment)
                        {
                            isCarryingFuel = fireplaceAssignment && m_carrying[instanceId].m_dropPrefab.name == fireplace.m_fuelItem.gameObject.name;
                            FuelisNotFull = Mathf.CeilToInt(fireplace.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f)) < fireplace.m_maxFuel;
                        }

                        if (smelterAssignment && isCarryingFuel && FuelisNotFull)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, "Unload to Smelter -> Fuel");
                            smelter.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
                            humanoid.GetInventory().RemoveOneItem(m_carrying[instanceId]);
                        }
                        else if (fireplaceAssignment && isCarryingFuel && FuelisNotFull)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, "Taking Care of the Fireplace");
                            fireplace.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
                            humanoid.GetInventory().RemoveOneItem(m_carrying[instanceId]);
                        }
                        else if (smelterAssignment && isCarryingMatchingOre && OreisNotFull)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, "Unload to Smelter -> Ore");
                            assignment.GetComponent<ZNetView>().InvokeRPC("AddOre", new object[] { GetPrefabName(m_carrying[instanceId].m_dropPrefab.name) });
                            humanoid.GetInventory().RemoveOneItem(m_carrying[instanceId]);
                        }
                        else
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, $"Dropping {m_carrying[instanceId].m_dropPrefab.name} on the ground");
                            humanoid.DropItem(humanoid.GetInventory(), m_carrying[instanceId], 1);
                        }

                        humanoid.UnequipItem(m_carrying[instanceId], false);
                        m_carrying[instanceId] = null;
                        m_fetchitems[instanceId].Clear();
                        return false;
                    }

                    bool isEmptyHanded = !m_fetchitems[instanceId].Any();
                    if (isEmptyHanded && isCloseToAssignment && smelterAssignment)
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, "Checking assignment for task");
                        int missingOre = smelter.m_maxOre - Traverse.Create(smelter).Method("GetQueueSize").GetValue<int>();
                        int missingFuel = smelter.m_maxFuel - Mathf.CeilToInt(smelter.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f));
                        Debug.Log($"Ore:{Traverse.Create(smelter).Method("GetQueueSize").GetValue<int>()}/{smelter.m_maxOre}, Fuel:{Mathf.CeilToInt(smelter.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f))}/{smelter.m_maxFuel}");
                        if (missingOre != 0)
                        {
                            foreach (Smelter.ItemConversion itemConversion in smelter.m_conversion)
                            {
                                string ore = GetPrefabName(itemConversion.m_from.gameObject.name);
                                m_fetchitems[instanceId].Add(ore);
                            }
                        }
                        if (missingFuel != 0)
                        {
                            string fuel = GetPrefabName(smelter.m_fuelItem.gameObject.name);
                            m_fetchitems[instanceId].Add(fuel);
                        }
                        m_assigned[instanceId] = false;
                        return false;
                    }
                    if (isEmptyHanded && isCloseToAssignment && fireplaceAssignment)
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, "Checking assignment for task");
                        int missingFuel = Mathf.FloorToInt(fireplace.m_maxFuel - fireplace.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f));
                        Debug.Log($"Fuel:{Mathf.CeilToInt(fireplace.GetComponent<ZNetView>().GetZDO().GetFloat("fuel", 0f))}/{fireplace.m_maxFuel}");
                        if (missingFuel != 0)
                        {
                            string fuel = GetPrefabName(fireplace.m_fuelItem.gameObject.name);
                            m_fetchitems[instanceId].Add(fuel);
                        }
                        m_assigned[instanceId] = false;
                        return false;
                    }

                    bool searchGroundForItemToPickup = m_fetchitems[instanceId].Any() && m_spottedItem[instanceId] == null && m_carrying[instanceId] == null;
                    if (searchGroundForItemToPickup)
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, "Search the ground for item to pickup");
                        foreach (Collider collider in Physics.OverlapSphere(___m_character.transform.position, 20, LayerMask.GetMask(new string[] { "item" })))
                        {
                            if (collider?.attachedRigidbody)
                            {
                                ItemDrop item = collider.attachedRigidbody.GetComponent<ItemDrop>();
                                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                                    continue;

                                string name = GetPrefabName(item.gameObject.name);
                                if (m_fetchitems[instanceId].Contains(name))
                                {
                                    Debug.Log($"nearby item spotted: {name}");
                                    m_spottedItem[instanceId] = item;
                                    return false;
                                }
                            }
                        }
                    }

                    bool hasSpottedAnItem = m_spottedItem[instanceId] != null;
                    if (hasSpottedAnItem)
                    {
                        bool isHeadingToPickupItem = Vector3.Distance(___m_character.transform.position, m_spottedItem[instanceId].transform.position) > 2.5;
                        if (isHeadingToPickupItem)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, "Heading to pickup item");
                            Invoke(__instance, "MoveAndAvoid", new object[] { dt, m_spottedItem[instanceId].transform.position, 0, false });
                            return false;
                        }
                        else // Pickup item from ground
                        {
                            var humanoid = ___m_character as Humanoid;
                            ___m_aiStatus = UpdateAiStatus(___m_nview, $"Trying to Pickup {m_spottedItem[instanceId].gameObject.name}");
                            var pickedUpInstance = humanoid.PickupPrefab(m_spottedItem[instanceId].m_itemData.m_dropPrefab);

                            humanoid.GetInventory().Print();

                            humanoid.EquipItem(pickedUpInstance);
                            if (m_spottedItem[instanceId].m_itemData.m_stack == 1)
                            {
                                if (___m_nview.GetZDO() == null)
                                {
                                    Destroy(m_spottedItem[instanceId].gameObject);
                                }
                                else
                                {
                                    ZNetScene.instance.Destroy(m_spottedItem[instanceId].gameObject);
                                }
                            }
                            else
                            {
                                m_spottedItem[instanceId].m_itemData.m_stack--;
                                Traverse.Create(m_spottedItem[instanceId]).Method("Save").GetValue();
                            }
                            m_carrying[instanceId] = pickedUpInstance;
                            m_spottedItem[instanceId] = null;
                            m_fetchitems[instanceId].Clear();
                            return false;
                        }
                    }
                    ___m_aiStatus = UpdateAiStatus(___m_nview, $"Done with assignment");
                    m_fetchitems[instanceId].Clear();
                    m_assigned[instanceId] = false;
                    return false;
                }

                ___m_aiStatus = UpdateAiStatus(___m_nview, "Random movement");
                typeof(MonsterAI).GetMethod("IdleMovement", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { dt });
                return false;
            }

            private static object Invoke(MonsterAI instance, string methodName, object[] argumentList)
            {
                return typeof(MonsterAI).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
            }

            private static int InitInstanceIfNeeded(MonsterAI instance)
            {
                int instanceId = instance.GetInstanceID();
                bool isNewInstance = !m_assignment.ContainsKey(instanceId);
                if (isNewInstance)
                {
                    m_assignment.Add(instanceId, new MaxStack<GameObject>(4));
                    m_assigned.Add(instanceId, false);
                    m_fetchitems.Add(instanceId, new List<string>());
                    m_carrying.Add(instanceId, null);
                    m_spottedItem.Add(instanceId, null);
                    m_aiStatus.Add(instanceId, "Init");
                }
                return instanceId;
            }

            static bool AvoidFire(MonsterAI instance, float dt, Vector3 targetPosition)
            {
                EffectArea effectArea2 = EffectArea.IsPointInsideArea(instance.transform.position, EffectArea.Type.Burning, 3f);
                if ((bool)effectArea2)
                {
                    typeof(MonsterAI).GetMethod("RandomMovementArroundPoint", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { dt, effectArea2.transform.position, effectArea2.GetRadius() + 3f + 1f, true });
                    return true;
                }
                return false;
            }
        }



        [HarmonyPatch(typeof(Character), "Awake")]
        static class Character_Awake_Patch
        {
            static void Postfix(Character __instance)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    Debug.Log($"A {__instance.name} just spawned!");
                    var tameable = __instance.gameObject.GetComponent<Tameable>();
                    if (tameable == null)
                    {
                        tameable = __instance.gameObject.AddComponent<Tameable>();
                    }

                    tameable.m_fedDuration = 500;
                    tameable.m_tamingTime = 1000;
                    tameable.m_commandable = true;
                    var ai = __instance.GetBaseAI() as MonsterAI;
                    if (__instance.IsTamed())
                    {
                        ai.m_consumeItems.Clear();
                        ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Resin").FirstOrDefault());
                        ai.m_randomMoveRange = 5;
                        ai.m_consumeSearchRange = 50;
                    }
                    else
                    {
                        ai.m_consumeItems.Clear();
                        ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "SilverNecklace").FirstOrDefault());
                        ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Ruby").FirstOrDefault());
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        static class MonsterAI_MakeTame_Patch
        {
            static void Postfix(MonsterAI __instance)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    Dbgl($"{__instance.name} was tamed!");
                    __instance.m_consumeItems.Clear();
                    __instance.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Resin").FirstOrDefault());
                    __instance.m_consumeSearchRange = 50;
                }
            }
        }

        [HarmonyPatch(typeof(Character), "GetHoverName")]
        static class Character_GetHoverName_Patch
        {
            static bool Prefix(Character __instance, ref string __result, ref ZNetView ___m_nview)
            {
                string givenName = ___m_nview?.GetZDO()?.GetString(GivenName);
                if (__instance.name.Contains("Greyling") && __instance.IsTamed() && !string.IsNullOrEmpty(givenName))
                {
                    __result = givenName;
                    return false;
                }
                else
                {
                    // Run original method
                    return true;
                }
            }
        }


        class MyTextReceiver : TextReceiver
        {
            private readonly ZNetView m_nview;

            public MyTextReceiver(ZNetView nview)
            {
                this.m_nview = nview;
            }

            public string GetText()
            {
                return m_nview.GetZDO().GetString(GivenName);
            }

            public void SetText(string text)
            {
                m_nview.ClaimOwnership();
                m_nview.GetZDO().Set(GivenName, text);
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

                if (___m_visEquipment == null)
                {
                    __instance.gameObject.AddComponent<VisEquipment>();
                    ___m_visEquipment = __instance.gameObject.GetComponent<VisEquipment>();
                    //_NetSceneRoot/Greyling(Clone)/Visual/Armature.001/root/spine1/spine2/spine3/r_shoulder/r_arm1/r_arm2/r_hand
                    var rightHand = __instance.gameObject.GetComponentsInChildren<Transform>().Where(c => c.name == "r_hand").Single();
                    ___m_visEquipment.m_rightHand = rightHand;
                }

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
                    return false;
                }
                string aiStatus = ___m_nview.GetZDO().GetString(AiStatus) ?? Traverse.Create(__instance).Method("GetStatusString").GetValue() as string;
                string str = Localization.instance.Localize(___m_character.GetHoverName());
                str += Localization.instance.Localize(" ( $hud_tame, " + aiStatus + " )");
                __result = str + Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] $hud_pet" + "\n[<color=yellow>Hold E</color>] to change name");

                return false;
            }
        }
        [HarmonyPatch(typeof(Tameable), "Interact")]
        static class Tameable_Interact_Patch
        {
            static bool Prefix(Tameable __instance, ref bool __result, Humanoid user, bool hold, ZNetView ___m_nview, Character ___m_character,
                ref float ___m_lastPetTime)
            {
                if (!__instance.name.Contains("Greyling")) return true;

                if (!___m_nview.IsValid())
                {
                    __result = false;
                    return false;
                }
                string hoverName = ___m_character.GetHoverName();
                if (___m_character.IsTamed())
                {
                    if (hold)
                    {
                        TextInput.instance.RequestText(new MyTextReceiver(___m_character.GetComponent<ZNetView>()), "Name", 15);
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

        //[HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        //static class Fireplace_UpdateFireplace_Patch
        //{
        //    static void Postfix(Fireplace __instance, ZNetView ___m_nview)
        //    {
        //        if (!Player.m_localPlayer || !isOn.Value || !___m_nview.IsOwner() || (__instance.name.Contains("groundtorch") && !refuelStandingTorches.Value) || (__instance.name.Contains("walltorch") && !refuelWallTorches.Value) || (__instance.name.Contains("fire_pit") && !refuelFirePits.Value))
        //            return;

        //        int maxFuel = (int)(__instance.m_maxFuel - Mathf.Ceil(___m_nview.GetZDO().GetFloat("fuel", 0f)));

        //        List<Container> nearbyContainers = GetNearbyContainers(__instance.transform.position);

        //        Vector3 position = __instance.transform.position + Vector3.up;
        //        foreach (Collider collider in Physics.OverlapSphere(position, dropRange.Value, LayerMask.GetMask(new string[] { "item" })))
        //        {
        //            if (collider?.attachedRigidbody)
        //            {
        //                ItemDrop item = collider.attachedRigidbody.GetComponent<ItemDrop>();
        //                //Dbgl($"nearby item name: {item.m_itemData.m_dropPrefab.name}");

        //                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
        //                    continue;

        //                string name = GetPrefabName(item.gameObject.name);

        //                if (item.m_itemData.m_shared.m_name == __instance.m_fuelItem.m_itemData.m_shared.m_name && maxFuel > 0)
        //                {

        //                    if (fuelDisallowTypes.Value.Split(',').Contains(name))
        //                    {
        //                        //Dbgl($"ground has {item.m_itemData.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"auto adding fuel {name} from ground");

        //                    int amount = Mathf.Min(item.m_itemData.m_stack, maxFuel);
        //                    maxFuel -= amount;

        //                    for (int i = 0; i < amount; i++)
        //                    {
        //                        if (item.m_itemData.m_stack <= 1)
        //                        {
        //                            if (___m_nview.GetZDO() == null)
        //                                Destroy(item.gameObject);
        //                            else
        //                                ZNetScene.instance.Destroy(item.gameObject);
        //                            ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                            break;

        //                        }

        //                        item.m_itemData.m_stack--;
        //                        ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                        Traverse.Create(item).Method("Save").GetValue();
        //                    }
        //                }
        //            }
        //        }

        //        foreach (Container c in nearbyContainers)
        //        {
        //            if (__instance.m_fuelItem && maxFuel > 0)
        //            {
        //                ItemDrop.ItemData fuelItem = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
        //                if (fuelItem != null)
        //                {
        //                    maxFuel--;
        //                    if (fuelDisallowTypes.Value.Split(',').Contains(fuelItem.m_dropPrefab.name))
        //                    {
        //                        //Dbgl($"container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name}, taking one");

        //                    ___m_nview.InvokeRPC("AddFuel", new object[] { });

        //                    c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);
        //                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
        //                    typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
        //                }
        //            }
        //        }
        //    }
        //}

        //[HarmonyPatch(typeof(Smelter), "FixedUpdate")]
        //static class Smelter_FixedUpdate_Patch
        //{
        //    static void Postfix(Smelter __instance, ZNetView ___m_nview)
        //    {
        //        if (!Player.m_localPlayer || !isOn.Value || !___m_nview.IsOwner())
        //            return;

        //        int maxOre = __instance.m_maxOre - Traverse.Create(__instance).Method("GetQueueSize").GetValue<int>();
        //        int maxFuel = __instance.m_maxFuel - Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel", 0f));


        //        List<Container> nearbyContainers = GetNearbyContainers(__instance.transform.position);

        //        Vector3 position = __instance.transform.position + Vector3.up;
        //        foreach (Collider collider in Physics.OverlapSphere(position, dropRange.Value, LayerMask.GetMask(new string[] { "item" })))
        //        {
        //            if (collider?.attachedRigidbody)
        //            {
        //                ItemDrop item = collider.attachedRigidbody.GetComponent<ItemDrop>();
        //                //Dbgl($"nearby item name: {item.m_itemData.m_dropPrefab.name}");

        //                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
        //                    continue;

        //                string name = GetPrefabName(item.gameObject.name);

        //                foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
        //                {
        //                    if (item.m_itemData.m_shared.m_name == itemConversion.m_from.m_itemData.m_shared.m_name && maxOre > 0)
        //                    {

        //                        if (oreDisallowTypes.Value.Split(',').Contains(name))
        //                        {
        //                            //Dbgl($"container at {c.transform.position} has {item.m_itemData.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
        //                            continue;
        //                        }

        //                        Dbgl($"auto adding ore {name} from ground");

        //                        int amount = Mathf.Min(item.m_itemData.m_stack, maxOre);
        //                        maxOre -= amount;

        //                        for (int i = 0; i < amount; i++)
        //                        {
        //                            if (item.m_itemData.m_stack <= 1)
        //                            {
        //                                if (___m_nview.GetZDO() == null)
        //                                    Destroy(item.gameObject);
        //                                else
        //                                    ZNetScene.instance.Destroy(item.gameObject);
        //                                ___m_nview.InvokeRPC("AddOre", new object[] { name });
        //                                break;
        //                            }

        //                            item.m_itemData.m_stack--;
        //                            ___m_nview.InvokeRPC("AddOre", new object[] { name });
        //                            Traverse.Create(item).Method("Save").GetValue();
        //                        }
        //                    }
        //                }

        //                if (__instance.m_fuelItem && item.m_itemData.m_shared.m_name == __instance.m_fuelItem.m_itemData.m_shared.m_name && maxFuel > 0)
        //                {

        //                    if (fuelDisallowTypes.Value.Split(',').Contains(name))
        //                    {
        //                        //Dbgl($"ground has {item.m_itemData.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"auto adding fuel {name} from ground");

        //                    int amount = Mathf.Min(item.m_itemData.m_stack, maxFuel);
        //                    maxFuel -= amount;

        //                    for (int i = 0; i < amount; i++)
        //                    {
        //                        if (item.m_itemData.m_stack <= 1)
        //                        {
        //                            if (___m_nview.GetZDO() == null)
        //                                Destroy(item.gameObject);
        //                            else
        //                                ZNetScene.instance.Destroy(item.gameObject);
        //                            ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                            break;

        //                        }

        //                        item.m_itemData.m_stack--;
        //                        ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                        Traverse.Create(item).Method("Save").GetValue();
        //                    }
        //                }
        //            }
        //        }

        //        foreach (Container c in nearbyContainers)
        //        {
        //            foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
        //            {
        //                ItemDrop.ItemData oreItem = c.GetInventory().GetItem(itemConversion.m_from.m_itemData.m_shared.m_name);

        //                if (oreItem != null && maxOre > 0)
        //                {
        //                    maxOre--;
        //                    if (oreDisallowTypes.Value.Split(',').Contains(oreItem.m_dropPrefab.name))
        //                        continue;

        //                    Dbgl($"container at {c.transform.position} has {oreItem.m_stack} {oreItem.m_dropPrefab.name}, taking one");

        //                    ___m_nview.InvokeRPC("AddOre", new object[] { oreItem.m_dropPrefab?.name });
        //                    c.GetInventory().RemoveItem(itemConversion.m_from.m_itemData.m_shared.m_name, 1);
        //                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
        //                    typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
        //                }
        //            }

        //            if (__instance.m_fuelItem && maxFuel > 0)
        //            {
        //                ItemDrop.ItemData fuelItem = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
        //                if (fuelItem != null)
        //                {
        //                    maxFuel--;
        //                    if (fuelDisallowTypes.Value.Split(',').Contains(fuelItem.m_dropPrefab.name))
        //                    {
        //                        //Dbgl($"container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name}, taking one");

        //                    ___m_nview.InvokeRPC("AddFuel", new object[] { });

        //                    c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);
        //                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
        //                    typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
        //                }
        //            }
        //        }
        //    }
        //}

        //[HarmonyPatch(typeof(Console), "InputText")]
        //static class InputText_Patch
        //{
        //    static bool Prefix(Console __instance)
        //    {
        //        if (!modEnabled.Value)
        //            return true;
        //        string text = __instance.m_input.text;
        //        if (text.ToLower().Equals("autofuel reset"))
        //        {
        //            context.Config.Reload();
        //            context.Config.Save();

        //            Traverse.Create(__instance).Method("AddString", new object[] { text }).GetValue();
        //            Traverse.Create(__instance).Method("AddString", new object[] { "AutoFuel config reloaded" }).GetValue();
        //            return false;
        //        }
        //        return true;
        //    }
        //}
    }
}