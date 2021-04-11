using BepInEx;
using HarmonyLib;
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
        private const string Z_GivenName = "RR_GivenName";
        private const string Z_AiStatus = "RR_AiStatus";
        private const string Z_CharacterId = "RR_CharId";
        private const string Z_UpdateCharacterHUD = "RR_UpdateCharacterHUD";
        private static readonly bool isDebug = false;

        private void Awake()
        {
            GreylingsConfig.Init(Config);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
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
                string currentAiStatus = nview?.GetZDO()?.GetString(Z_AiStatus);
                if (currentAiStatus != newStatus)
                {
                    string name = nview?.GetZDO()?.GetString(Z_GivenName);
                    Debug.Log($"{name}: {newStatus}");
                    nview.GetZDO().Set(Z_AiStatus, newStatus);
                }
                return newStatus;
            }

            static MonsterAI_UpdateAI_Patch()
            {
                m_assignment = new Dictionary<int, MaxStack<Assignment>>();
                m_assigned = new Dictionary<int, bool>();
                m_containers = new Dictionary<int, MaxStack<Container>>();
                m_searchcontainer = new Dictionary<int, bool>();
                m_fetchitems = new Dictionary<int, List<ItemDrop.ItemData>>();
                m_carrying = new Dictionary<int, ItemDrop.ItemData>();
                m_spottedItem = new Dictionary<int, ItemDrop>();
                m_aiStatus = new Dictionary<int, string>();
                m_assignedTimer = new Dictionary<int, float>();

                m_acceptedContainerNames = new List<string>();
                m_acceptedContainerNames.AddRange(GreylingsConfig.IncludedContainersList.Value.Split());
            }
            public static Dictionary<int, MaxStack<Assignment>> m_assignment;
            public static Dictionary<int, MaxStack<Container>> m_containers;
            public static Dictionary<int, bool> m_assigned;
            public static Dictionary<int, bool> m_searchcontainer;
            public static Dictionary<int, List<ItemDrop.ItemData>> m_fetchitems;
            public static Dictionary<int, ItemDrop.ItemData> m_carrying;
            public static Dictionary<int, ItemDrop> m_spottedItem;
            public static Dictionary<int, string> m_aiStatus;
            public static Dictionary<int, float> m_assignedTimer;

            private static Character m_attacker = null;
            private static List<string> m_acceptedContainerNames;

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
                    Invoke(__instance, "SetAlerted", new object[] { false });
                    m_assignment[instanceId].Clear();
                    m_fetchitems[instanceId].Clear();
                    m_assigned[instanceId] = false;
                    m_spottedItem[instanceId] = null;
                    m_containers[instanceId].Clear();
                    m_searchcontainer[instanceId] = false;
                    return false;
                }
                if (AvoidFire(__instance, dt, m_assigned[instanceId] ? m_assignment[instanceId].Peek().Position : __instance.transform.position))
                {
                    ___m_aiStatus = UpdateAiStatus(___m_nview, "Avoiding fire");
                    if (m_assignment[instanceId].Peek().IsClose(___m_character.transform.position))
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

                // Here starts the fun.
                
                //Assigned timeout-function 
                m_assignedTimer[instanceId] += dt;
                if (m_assignedTimer[instanceId] > GreylingsConfig.TimeLimitOnAssignment.Value) m_assigned[instanceId] = false;
                
                //Assignment timeout-function
                foreach (Assignment assignment in m_assignment[instanceId])
                {
                    assignment.AssignmentTime += dt;
                    int multiplicator = 1;
                    if (assignment.TypeOfAssignment.ComponentType == typeof(Fireplace))
                    {
                        multiplicator = 3;
                    }
                    if (assignment.AssignmentTime > GreylingsConfig.TimeBeforeAssignmentCanBeRepeated.Value * multiplicator)
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, $"removing outdated Assignment of {m_assignment[instanceId].Count()}");
                        m_assignment[instanceId].Remove(assignment);
                        ___m_aiStatus = UpdateAiStatus(___m_nview, $"remaining Assignments {m_assignment[instanceId].Count()}");
                        break;
                    }
                }

                Vector3 greylingPosition = ___m_character.transform.position;
                if (!m_assigned[instanceId])
                {
                    if (FindRandomNearbyAssignment(instanceId, greylingPosition))
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, $"Doing assignment: {m_assignment[instanceId].Peek().TypeOfAssignment.Name}");
                        return false;
                    }
                    else
                    {
                        //___m_aiStatus = UpdateAiStatus(___m_nview, $"No new assignments found");
                        m_assignment[instanceId].Clear();
                    }
                }

                if (m_assigned[instanceId])
                {
                    var humanoid = ___m_character as Humanoid;
                    Assignment assignment = m_assignment[instanceId].Peek();
                    bool assignmentIsInvalid = assignment?.AssignmentObject?.GetComponent<ZNetView>()?.IsValid() == false;
                    if (assignmentIsInvalid)
                    {
                        m_assignment[instanceId].Pop();
                        m_assigned[instanceId] = false;
                        return false;
                    }

                    bool knowWhattoFetch = m_fetchitems[instanceId].Any();
                    bool isCarryingItem = m_carrying[instanceId] != null;
                    if ((!knowWhattoFetch || isCarryingItem) && !assignment.IsClose(greylingPosition))
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, $"Move To Assignment: {assignment.TypeOfAssignment.Name} ");
                        Invoke(__instance, "MoveAndAvoid", new object[] { dt, assignment.Position, 0.5f, false });
                        return false;
                    }

                    bool isLookingAtAssignment = (bool)Invoke(__instance,"IsLookingAt", new object[] { assignment.Position, 20f });
                    if (isCarryingItem && assignment.IsClose(greylingPosition) && !isLookingAtAssignment)
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, $"Looking at Assignment: {assignment.TypeOfAssignment.Name} ");
                        Invoke(__instance, "LookAt", new object[] { assignment.Position });
                        return false;
                    }

                    if (isCarryingItem && assignment.IsClose(greylingPosition))
                    {
                        var needFuel = assignment.NeedFuel;
                        var needOre = assignment.NeedOre;
                        bool isCarryingFuel = m_carrying[instanceId].m_shared.m_name == needFuel?.m_shared?.m_name;
                        bool isCarryingMatchingOre = needOre?.Any(c => m_carrying[instanceId].m_shared.m_name == c?.m_shared?.m_name) ?? false;

                        if (isCarryingFuel)
                        { 
                            ___m_aiStatus = UpdateAiStatus(___m_nview, $"Unload to {assignment.TypeOfAssignment.Name} -> Fuel");
                            assignment.AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
                            humanoid.GetInventory().RemoveOneItem(m_carrying[instanceId]);
                        }
                        else if (isCarryingMatchingOre)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, $"Unload to {assignment.TypeOfAssignment.Name} -> Ore");
                            assignment.AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddOre", new object[] { GetPrefabName(m_carrying[instanceId].m_dropPrefab.name) });
                            humanoid.GetInventory().RemoveOneItem(m_carrying[instanceId]);
                        }
                        else
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, Localization.instance.Localize($"Dropping {m_carrying[instanceId].m_shared.m_name} on the ground"));
                            humanoid.DropItem(humanoid.GetInventory(), m_carrying[instanceId], 1);
                        }

                        humanoid.UnequipItem(m_carrying[instanceId], false);
                        m_carrying[instanceId] = null;
                        m_fetchitems[instanceId].Clear();
                        return false;
                    }

                    if (!knowWhattoFetch && assignment.IsClose(greylingPosition))
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, "Checking assignment for task");
                        var needFuel = assignment.NeedFuel;
                        var needOre = assignment.NeedOre;
                        Dbgl($"Ore:{needOre.Join(j => j.m_shared.m_name)}, Fuel:{needFuel?.m_shared.m_name}");
                        if (needFuel != null)
                        {
                            m_fetchitems[instanceId].Add(needFuel);
                            ___m_aiStatus = UpdateAiStatus(___m_nview, Localization.instance.Localize($"Adding {needFuel.m_shared.m_name} to search list"));
                        }
                        if (needOre.Any())
                        {
                            m_fetchitems[instanceId].AddRange(needOre);
                            ___m_aiStatus = UpdateAiStatus(___m_nview, Localization.instance.Localize($"Adding {needOre.Join(o => o.m_shared.m_name)} to search list"));
                        }
                        if (!m_fetchitems[instanceId].Any())
                        {
                            m_assigned[instanceId] = false;
                        }
                        return false;
                    }

                    bool hasSpottedAnItem = m_spottedItem[instanceId] != null;
                    bool searchForItemToPickup = knowWhattoFetch && !hasSpottedAnItem && !isCarryingItem && !m_searchcontainer[instanceId];
                    if (searchForItemToPickup)
                    {
                        ___m_aiStatus = UpdateAiStatus(___m_nview, "Search the ground for item to pickup");
                        ItemDrop spottedItem = GetNearbyItem(greylingPosition, m_fetchitems[instanceId], GreylingsConfig.ItemSearchRadius.Value);
                        if (spottedItem != null)
                        {
                            m_spottedItem[instanceId] = spottedItem;
                            return false;
                        }
                        
                        ___m_aiStatus = UpdateAiStatus(___m_nview, "Trying to remeber content of known Chests");
                        foreach (Container chest in m_containers[instanceId])
                        {
                            foreach (var fetchItem in m_fetchitems[instanceId])
                            {
                                ItemDrop.ItemData item = chest?.GetInventory()?.GetItem(fetchItem.m_shared.m_name);
                                if (item == null) continue;
                                else
                                {
                                    ___m_aiStatus = UpdateAiStatus(___m_nview, "Item found in old chest");
                                    m_containers[instanceId].Remove(chest);
                                    m_containers[instanceId].Push(chest);
                                    m_searchcontainer[instanceId] = true;
                                    return false;
                                }
                            }
                        }

                        ___m_aiStatus = UpdateAiStatus(___m_nview, "Search for nerby Chests") ;
                        Container nearbyChest = FindRandomNearbyContainer(greylingPosition, m_containers[instanceId]);
                        if (nearbyChest != null)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, "Chest found");
                            m_containers[instanceId].Push(nearbyChest);
                            m_searchcontainer[instanceId] = true;
                            return false;
                        }
                    }

                    if (m_searchcontainer[instanceId])
                    {
                        bool containerIsInvalid = m_containers[instanceId].Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
                        if (containerIsInvalid)
                        {
                            m_containers[instanceId].Pop();
                            m_searchcontainer[instanceId] = false;
                            return false;
                        }
                        bool isCloseToContainer = Vector3.Distance(greylingPosition, m_containers[instanceId].Peek().transform.position) < 2.0;
                        if (!isCloseToContainer)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, "Heading to Container");
                            Invoke(__instance, "MoveAndAvoid", new object[] { dt, m_containers[instanceId].Peek().transform.position, 0.5f, false });
                            return false;
                        }
                        else
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, $"Chest inventory:{m_containers[instanceId].Peek()?.GetInventory().GetAllItems().Join(i => i.m_shared.m_name)} from Chest ");
                            var wantedItemsInChest = m_containers[instanceId].Peek()?.GetInventory()?.GetAllItems()?.Where(i => m_fetchitems[instanceId].Contains(i));
                            foreach (var fetchItem in m_fetchitems[instanceId])
                            {
                                ItemDrop.ItemData item = m_containers[instanceId].Peek()?.GetInventory()?.GetItem(fetchItem.m_shared.m_name);
                                if (item == null) continue;
                                else
                                {
                                    ___m_aiStatus = UpdateAiStatus(___m_nview, $"Trying to Pickup {item} from Chest ");
                                    var pickedUpInstance = humanoid.PickupPrefab(item.m_dropPrefab);
                                    humanoid.GetInventory().Print();
                                    humanoid.EquipItem(pickedUpInstance);
                                    m_containers[instanceId].Peek().GetInventory().RemoveItem(item, 1);
                                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m_containers[instanceId].Peek(), new object[] { });
                                    typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m_containers[instanceId].Peek().GetInventory(), new object[] { });
                                    m_carrying[instanceId] = pickedUpInstance;
                                    m_spottedItem[instanceId] = null;
                                    m_fetchitems[instanceId].Clear();
                                    m_searchcontainer[instanceId] = false;
                                    return false;
                                }
                            }

                            m_searchcontainer[instanceId] = false;
                            return false;
                        }
                    }

                    if (hasSpottedAnItem)
                    {
                        bool isCloseToPickupItem = Vector3.Distance(greylingPosition, m_spottedItem[instanceId].transform.position) > 1;
                        if (isCloseToPickupItem)
                        {
                            ___m_aiStatus = UpdateAiStatus(___m_nview, "Heading to pickup item");
                            Invoke(__instance, "MoveAndAvoid", new object[] { dt, m_spottedItem[instanceId].transform.position, 0.5f , false });
                            return false;
                        }
                        else // Pickup item from ground
                        {
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
                    if (m_carrying[instanceId] != null)
                    {
                        humanoid.UnequipItem(m_carrying[instanceId], false);
                        m_carrying[instanceId] = null;
                        ___m_aiStatus = UpdateAiStatus(___m_nview, $"Dropping unused item");
                    }
                    m_fetchitems[instanceId].Clear();
                    m_spottedItem[instanceId] = null;
                    m_containers[instanceId].Clear();
                    m_searchcontainer[instanceId] = false;
                    m_assigned[instanceId] = false;
                    return false;
                }

                ___m_aiStatus = UpdateAiStatus(___m_nview, "Random movement (No new assignments found)");
                typeof(MonsterAI).GetMethod("IdleMovement", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { dt });
                return false;
            }

            public static ItemDrop GetNearbyItem(Vector3 center, List<ItemDrop.ItemData> acceptedNames , int range = 10) 
            {
                ItemDrop ClosestObject = null;
                foreach (Collider collider in Physics.OverlapSphere(center, range, LayerMask.GetMask(new string[] { "item" })))
                {
                    ItemDrop item = collider.transform.parent?.parent?.gameObject?.GetComponent<ItemDrop>();
                    if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                    {
                        item = collider.transform.parent?.gameObject?.GetComponent<ItemDrop>();
                        if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                        {
                            item = collider.transform?.gameObject?.GetComponent<ItemDrop>();
                            if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                            {
                                continue;
                            }
                        }
                    }
                    if (item?.transform?.position != null && acceptedNames.Select(n => n.m_shared.m_name).Contains(item.m_itemData.m_shared.m_name) && (ClosestObject == null || Vector3.Distance(center, item.transform.position) < Vector3.Distance(center, ClosestObject.transform.position)))
                    {
                        ClosestObject = item;
                    }
                }
                return ClosestObject;
            }

            private static bool FindRandomNearbyAssignment(int instanceId, Vector3 greylingPosition)
            {
                Dbgl($"Enter {nameof(FindRandomNearbyAssignment)}");
                //Generate list of acceptable assignments
                var pieceList = new List<Piece>();
                Piece.GetAllPiecesInRadius(greylingPosition, (float)GreylingsConfig.AssignmentSearchRadius.Value, pieceList);
                var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => GetPrefabName(p.name) == a.PieceName && a.Activated));
                // no assignments detekted, return false
                if (!allAssignablePieces.Any())
                {
                    return false;
                }

                // filter out assignments already in list
                var newAssignments = allAssignablePieces.Where(p => !m_assignment[instanceId].Any(a => a.AssignmentObject == p.gameObject));

                // filter out inaccessible assignments
                //newAssignments = newAssignments.Where(p => Pathfinding.instance.GetPath(greylingPosition, p.gameObject.transform.position, null, Pathfinding.AgentType.Humanoid, true, true));

                if (!newAssignments.Any())
                {
                    return false;
                }

                // select random piece
                var random = new System.Random();
                int index = random.Next(newAssignments.Count());
                Assignment randomAssignment = new Assignment(instanceId, newAssignments.ElementAt(index));
                // Create assignment and return true
                m_assignment[instanceId].Push(randomAssignment);
                m_assigned[instanceId] = true;
                m_assignedTimer[instanceId] = 0;
                m_fetchitems[instanceId].Clear();
                m_spottedItem[instanceId] = null;
                return true;
            }

            private static Container FindRandomNearbyContainer(Vector3 greylingPosition, MaxStack<Container> knownContainers)
            {
                Dbgl($"Enter {nameof(FindRandomNearbyContainer)}");
                var pieceList = new List<Piece>();
                Piece.GetAllPiecesInRadius(greylingPosition, (float)GreylingsConfig.ContainerSearchRadius.Value, pieceList);
                var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(GetPrefabName(p.name)));
                // no containers detected, return false

                var containers = allcontainerPieces?.Select(p => p.gameObject.GetComponent<Container>()).Where(c => !knownContainers.Contains(c));
                if (!containers.Any())
                {
                    return null;
                }

                // select random piece
                var random = new System.Random();
                int index = random.Next(containers.Count());
                return containers.ElementAt(index);
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
                    m_assignment.Add(instanceId, new MaxStack<Assignment>(20));
                    m_containers.Add(instanceId, new MaxStack<Container>(GreylingsConfig.MaxContainersInMemory.Value));
                    m_assigned.Add(instanceId, false);
                    m_searchcontainer.Add(instanceId, false);
                    m_fetchitems.Add(instanceId, new List<ItemDrop.ItemData>());
                    m_carrying.Add(instanceId, null);
                    m_spottedItem.Add(instanceId, null);
                    m_aiStatus.Add(instanceId, "Init");
                    m_assignedTimer.Add(instanceId, 0);
                }
                return instanceId;
            }

            static bool AvoidFire(MonsterAI instance, float dt, Vector3 targetPosition)
            {
                EffectArea effectArea2 = EffectArea.IsPointInsideArea(instance.transform.position, EffectArea.Type.Burning, 2f);
                if ((bool)effectArea2)
                {
                    typeof(MonsterAI).GetMethod("RandomMovementArroundPoint", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { dt, effectArea2.transform.position, effectArea2.GetRadius() + 3f, true });
                    return true;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        static class Character_Awake_Patch
        {
            private static Dictionary<string, int> m_allGreylings = new Dictionary<string, int>();

            static void Postfix(Character __instance, ref ZNetView ___m_nview)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    Debug.Log($"A {__instance.name} just spawned!");
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
                    
                    var uniqueId = ___m_nview.GetZDO().GetString(Z_CharacterId);
                    Debug.Log($"Greyling guid:{uniqueId ?? string.Empty}");
                    if (string.IsNullOrEmpty(uniqueId))
                    {
                        uniqueId = System.Guid.NewGuid().ToString();
                        ___m_nview.GetZDO().Set(Z_CharacterId, uniqueId);
                        Debug.Log($"Created new guid:{uniqueId}");
                    }
                    if (m_allGreylings.ContainsKey(uniqueId))
                    {
                        m_allGreylings[uniqueId] = __instance.GetInstanceID();
                    }
                    else
                    {
                        m_allGreylings.Add(uniqueId, __instance.GetInstanceID());
                    }
                    ___m_nview.Register<string, string>(Z_UpdateCharacterHUD, RPC_UpdateCharacterName);

                    var ai = __instance.GetBaseAI() as MonsterAI;
                    if (__instance.IsTamed())
                    {
                        ai.m_consumeItems.Clear();
                        ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Resin").FirstOrDefault());
                        ai.m_randomMoveRange = 5;
                        ai.m_consumeSearchRange = 50;
                        string givenName = ___m_nview?.GetZDO()?.GetString(Z_GivenName);
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
                nview.InvokeRPC(ZNetView.Everybody, Z_UpdateCharacterHUD, nview.GetZDO().GetString(Z_CharacterId), text);
            }

            public static void RPC_UpdateCharacterName(long sender, string uniqueId, string text)
            {
                if (!m_allGreylings.ContainsKey(uniqueId)) return;

                var greylingToUpdate = Character.GetAllCharacters().Where(c => c.GetInstanceID() == m_allGreylings[uniqueId]).FirstOrDefault();
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
                    Dbgl($"{__instance.name} was tamed!");
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
                return m_nview.GetZDO().GetString(Z_GivenName);
            }

            public void SetText(string text)
            {
                m_nview.ClaimOwnership();
                m_nview.GetZDO().Set(Z_GivenName, text);
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
                string aiStatus = ___m_nview.GetZDO().GetString(Z_AiStatus) ?? Traverse.Create(__instance).Method("GetStatusString").GetValue() as string;
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

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(SlaveGreylings).Namespace + " " : "") + str);
        }

    }
}