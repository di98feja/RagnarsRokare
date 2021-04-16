using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SlaveGreylings
{
    public class GreylingAI : MobAIBase
    {
        public MaxStack<Assignment> m_assignment;
        public MaxStack<Container> m_containers;
        public bool m_assigned;
        public bool m_searchcontainer;
        public List<ItemDrop.ItemData> m_fetchitems;
        public ItemDrop.ItemData m_carrying;
        public ItemDrop m_spottedItem;
        public float m_assignedTimer;
        public float m_stateChangeTimer;
        private string[] m_acceptedContainerNames;

        public GreylingAI()
        {
            m_assignment = new MaxStack<Assignment>(20);
            m_assigned = false;
            m_containers = new MaxStack<Container>(GreylingsConfig.MaxContainersInMemory.Value);
            m_searchcontainer = false;
            m_fetchitems = new List<ItemDrop.ItemData>();
            m_carrying = null;
            m_spottedItem = null;
            m_assignedTimer = 0f;
            m_stateChangeTimer = 0f;
            m_acceptedContainerNames = GreylingsConfig.IncludedContainersList.Value.Split();

        }
        public override void UpdateAI(BaseAI instance, float dt)
        {
            base.UpdateAI(instance, dt);
            var monsterAi = instance as MonsterAI;
            Vector3 greylingPosition = this.Character.transform.position;

            if (TimeSinceHurt < 20f)
            {
                instance.Alert();
                //var fleeFrom = m_attacker == null ? ___m_character.transform.position : m_attacker.transform.position;
                Invoke<MonsterAI>(instance, "Flee", dt, Character.transform.position);
                UpdateAiStatus(NView, "Got hurt, flee!");
                return;
            }
            else
            {
                //m_attacker = null;
                Invoke<MonsterAI>(instance, "SetAlerted", false );
            }
            if ((bool)monsterAi.GetFollowTarget())
            {
                Invoke<MonsterAI>(instance, "Follow", monsterAi.GetFollowTarget(), dt );
                UpdateAiStatus(NView, "Follow");
                Invoke<MonsterAI>(instance, "SetAlerted", false );
                m_assignment.Clear();
                m_fetchitems.Clear();
                m_assigned = false;
                m_spottedItem = null;
                m_containers.Clear();
                m_searchcontainer = false;
                m_stateChangeTimer = 0;
                return;
            }
            if (AvoidFire(dt))
            {
                UpdateAiStatus(NView, "Avoiding fire");
                if (m_assignment.Any() && m_assignment.Peek().IsClose(this.Character.transform.position))
                {
                    m_assigned = false;
                }
                return;
            }
            if (!monsterAi.IsAlerted() && (bool)Invoke<MonsterAI>(monsterAi, "UpdateConsumeItem", this.Character as Humanoid, dt))
            {
                UpdateAiStatus(NView, "Consume item");
                return;
            }
            if (monsterAi.Tameable().IsHungry())
            {
                UpdateAiStatus(___m_nview, "Is hungry, no work a do");
                if (m_searchcontainer && m_containers.Any())
                {
                    bool containerIsInvalid = m_containers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
                    if (containerIsInvalid)
                    {
                        m_containers.Pop();
                        m_searchcontainer = false;
                        return;
                    }
                    bool isCloseToContainer = Vector3.Distance(greylingPosition, m_containers.Peek().transform.position) < 1.5;
                    if (!isCloseToContainer)
                    {
                        Invoke(monsterAi, "MoveAndAvoid", new object[] { dt, m_containers.Peek().transform.position, 0.5f, false });
                        return;
                    }
                    else
                    {
                        ItemDrop foodItem = monsterAi.m_consumeItems.ElementAt<ItemDrop>(0);
                        ItemDrop.ItemData item = m_containers.Peek()?.GetInventory()?.GetItem(foodItem.m_itemData.m_shared.m_name);
                        if (item == null)
                        {
                            UpdateAiStatus(___m_nview, "No Resin in chest");
                            Container nearbyChest = FindRandomNearbyContainer(greylingPosition, m_containers);
                            if (nearbyChest != null)
                            {
                                m_containers.Push(nearbyChest);
                                m_searchcontainer = true;
                                return;
                            }
                            else
                            {
                                m_containers.Clear();
                                m_searchcontainer = false;
                                return;
                            }
                        }
                        else
                        {
                            UpdateAiStatus(___m_nview, "Resin in chest");
                            m_containers.Peek().GetInventory().RemoveItem(item, 1);
                            typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m_containers.Peek(), new object[] { });
                            typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m_containers.Peek().GetInventory(), new object[] { });
                            monsterAi.m_onConsumedItem(foodItem);
                            UpdateAiStatus(___m_nview, "Consume item");
                            m_assigned = false;
                            m_spottedItem = null;
                            m_searchcontainer = false;
                            m_stateChangeTimer = 0;
                            return;
                        }
                    }
                }
                else
                {
                    Container nearbyChest = FindRandomNearbyContainer(greylingPosition, m_containers);
                    if (nearbyChest != null)
                    {
                        m_containers.Push(nearbyChest);
                        m_searchcontainer = true;
                        return;
                    }
                    else
                    {
                        m_searchcontainer = false;
                        return;
                    }
                }
            }

            // Here starts the fun.

            //Assigned timeout-function 
            m_assignedTimer += dt;
            if (m_assignedTimer > GreylingsConfig.TimeLimitOnAssignment.Value) m_assigned = false;

            //Assignment timeout-function
            foreach (Assignment assignment in m_assignment)
            {
                assignment.AssignmentTime += dt;
                int multiplicator = 1;
                if (assignment.TypeOfAssignment.ComponentType == typeof(Fireplace))
                {
                    multiplicator = 3;
                }
                if (assignment.AssignmentTime > GreylingsConfig.TimeBeforeAssignmentCanBeRepeated.Value * multiplicator)
                {
                    UpdateAiStatus(___m_nview, $"removing outdated Assignment of {m_assignment.Count()}");
                    m_assignment.Remove(assignment);
                    UpdateAiStatus(___m_nview, $"remaining Assignments {m_assignment.Count()}");
                    if (!m_assignment.Any())
                    {
                        m_assigned = false;
                    }
                    break;
                }
            }

            //stateChangeTimer Updated
            m_stateChangeTimer += dt;
            if (m_stateChangeTimer < 1) return;


            if (!m_assigned)
            {
                if (FindRandomNearbyAssignment(instanceId, greylingPosition))
                {
                    UpdateAiStatus(___m_nview, $"Doing assignment: {m_assignment.Peek().TypeOfAssignment.Name}");
                    return;
                }
                else
                {
                    //UpdateAiStatus(___m_nview, $"No new assignments found");
                    m_assignment.Clear();
                }
            }

            if (m_assigned)
            {
                var humanoid = ___m_character as Humanoid;
                Assignment assignment = m_assignment.Peek();
                bool assignmentIsInvalid = assignment?.AssignmentObject?.GetComponent<ZNetView>()?.IsValid() == false;
                if (assignmentIsInvalid)
                {
                    m_assignment.Pop();
                    m_assigned = false;
                    return;
                }

                bool knowWhattoFetch = m_fetchitems.Any();
                bool isCarryingItem = m_carrying != null;
                if ((!knowWhattoFetch || isCarryingItem) && !assignment.IsClose(greylingPosition))
                {
                    UpdateAiStatus(___m_nview, $"Move To Assignment: {assignment.TypeOfAssignment.Name} ");
                    Invoke(monsterAi, "MoveAndAvoid", new object[] { dt, assignment.Position, 0.5f, false });
                    if (m_stateChangeTimer < 30)
                    {
                        return;
                    }
                }

                bool isLookingAtAssignment
                    = (bool)Invoke(monsterAi, "IsLookingAt", new object[] { assignment.Position, 20f });
                if (isCarryingItem && assignment.IsClose(greylingPosition) && !isLookingAtAssignment)
                {
                    UpdateAiStatus(___m_nview, $"Looking at Assignment: {assignment.TypeOfAssignment.Name} ");
                    humanoid.SetMoveDir(Vector3.zero);
                    Invoke(monsterAi, "LookAt", new object[] { assignment.Position });
                    return;
                }

                if (isCarryingItem && assignment.IsCloseEnough(greylingPosition))
                {
                    humanoid.SetMoveDir(Vector3.zero);
                    var needFuel = assignment.NeedFuel;
                    var needOre = assignment.NeedOre;
                    bool isCarryingFuel = m_carrying.m_shared.m_name == needFuel?.m_shared?.m_name;
                    bool isCarryingMatchingOre = needOre?.Any(c => m_carrying.m_shared.m_name == c?.m_shared?.m_name) ?? false;

                    if (isCarryingFuel)
                    {
                        UpdateAiStatus(___m_nview, $"Unload to {assignment.TypeOfAssignment.Name} -> Fuel");
                        assignment.AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
                        humanoid.GetInventory().RemoveOneItem(m_carrying);
                    }
                    else if (isCarryingMatchingOre)
                    {
                        UpdateAiStatus(___m_nview, $"Unload to {assignment.TypeOfAssignment.Name} -> Ore");
                        assignment.AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddOre", new object[] { GetPrefabName(m_carrying.m_dropPrefab.name) });
                        humanoid.GetInventory().RemoveOneItem(m_carrying);
                    }
                    else
                    {
                        UpdateAiStatus(___m_nview, Localization.instance.Localize($"Dropping {m_carrying.m_shared.m_name} on the ground"));
                        humanoid.DropItem(humanoid.GetInventory(), m_carrying, 1);
                    }

                    humanoid.UnequipItem(m_carrying, false);
                    m_carrying = null;
                    m_fetchitems.Clear();
                    m_stateChangeTimer = 0;
                    return;
                }

                if (!knowWhattoFetch && assignment.IsCloseEnough(greylingPosition))
                {
                    humanoid.SetMoveDir(Vector3.zero);
                    UpdateAiStatus(___m_nview, "Checking assignment for task");
                    var needFuel = assignment.NeedFuel;
                    var needOre = assignment.NeedOre;
                    Dbgl($"Ore:{needOre.Join(j => j.m_shared.m_name)}, Fuel:{needFuel?.m_shared.m_name}");
                    if (needFuel != null)
                    {
                        m_fetchitems.Add(needFuel);
                        UpdateAiStatus(___m_nview, Localization.instance.Localize($"Adding {needFuel.m_shared.m_name} to search list"));
                    }
                    if (needOre.Any())
                    {
                        m_fetchitems.AddRange(needOre);
                        UpdateAiStatus(___m_nview, Localization.instance.Localize($"Adding {needOre.Join(o => o.m_shared.m_name)} to search list"));
                    }
                    if (!m_fetchitems.Any())
                    {
                        m_assigned = false;
                    }
                    m_stateChangeTimer = 0;
                    return;
                }

                bool hasSpottedAnItem = m_spottedItem != null;
                bool searchForItemToPickup = knowWhattoFetch && !hasSpottedAnItem && !isCarryingItem && !m_searchcontainer;
                if (searchForItemToPickup)
                {
                    UpdateAiStatus(___m_nview, "Search the ground for item to pickup");
                    ItemDrop spottedItem = GetNearbyItem(greylingPosition, m_fetchitems, GreylingsConfig.ItemSearchRadius.Value);
                    if (spottedItem != null)
                    {
                        m_spottedItem = spottedItem;
                        m_stateChangeTimer = 0;
                        return;
                    }

                    UpdateAiStatus(___m_nview, "Trying to remeber content of known Chests");
                    foreach (Container chest in m_containers)
                    {
                        foreach (var fetchItem in m_fetchitems)
                        {
                            ItemDrop.ItemData item = chest?.GetInventory()?.GetItem(fetchItem.m_shared.m_name);
                            if (item == null) continue;
                            else
                            {
                                UpdateAiStatus(___m_nview, "Item found in old chest");
                                m_containers.Remove(chest);
                                m_containers.Push(chest);
                                m_searchcontainer = true;
                                m_stateChangeTimer = 0;
                                return;
                            }
                        }
                    }

                    UpdateAiStatus(___m_nview, "Search for nerby Chests");
                    Container nearbyChest = FindRandomNearbyContainer(greylingPosition, m_containers);
                    if (nearbyChest != null)
                    {
                        UpdateAiStatus(___m_nview, "Chest found");
                        m_containers.Push(nearbyChest);
                        m_searchcontainer = true;
                        m_stateChangeTimer = 0;
                        return;
                    }
                }

                if (m_searchcontainer)
                {
                    bool containerIsInvalid = m_containers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
                    if (containerIsInvalid)
                    {
                        m_containers.Pop();
                        m_searchcontainer = false;
                        return;
                    }
                    bool isCloseToContainer = Vector3.Distance(greylingPosition, m_containers.Peek().transform.position) < 1.5;
                    if (!isCloseToContainer)
                    {
                        UpdateAiStatus(___m_nview, "Heading to Container");
                        Invoke(monsterAi, "MoveAndAvoid", new object[] { dt, m_containers.Peek().transform.position, 0.5f, false });
                        return;
                    }
                    else
                    {
                        humanoid.SetMoveDir(Vector3.zero);
                        UpdateAiStatus(___m_nview, $"Chest inventory:{m_containers.Peek()?.GetInventory().GetAllItems().Join(i => i.m_shared.m_name)} from Chest ");
                        var wantedItemsInChest = m_containers.Peek()?.GetInventory()?.GetAllItems()?.Where(i => m_fetchitems.Contains(i));
                        foreach (var fetchItem in m_fetchitems)
                        {
                            ItemDrop.ItemData item = m_containers.Peek()?.GetInventory()?.GetItem(fetchItem.m_shared.m_name);
                            if (item == null) continue;
                            else
                            {
                                UpdateAiStatus(___m_nview, $"Trying to Pickup {item} from Chest ");
                                var pickedUpInstance = humanoid.PickupPrefab(item.m_dropPrefab);
                                humanoid.GetInventory().Print();
                                humanoid.EquipItem(pickedUpInstance);
                                m_containers.Peek().GetInventory().RemoveItem(item, 1);
                                typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m_containers.Peek(), new object[] { });
                                typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(m_containers.Peek().GetInventory(), new object[] { });
                                m_carrying = pickedUpInstance;
                                m_spottedItem = null;
                                m_fetchitems.Clear();
                                m_searchcontainer = false;
                                m_stateChangeTimer = 0;
                                return;
                            }
                        }

                        m_searchcontainer = false;
                        m_stateChangeTimer = 0;
                        return;
                    }
                }

                if (hasSpottedAnItem)
                {
                    bool isNotCloseToPickupItem = Vector3.Distance(greylingPosition, m_spottedItem.transform.position) > 1;
                    if (isNotCloseToPickupItem)
                    {
                        UpdateAiStatus(___m_nview, "Heading to pickup item");
                        Invoke(monsterAi, "MoveAndAvoid", new object[] { dt, m_spottedItem.transform.position, 0.5f, false });
                        return;
                    }
                    else // Pickup item from ground
                    {
                        humanoid.SetMoveDir(Vector3.zero);
                        UpdateAiStatus(___m_nview, $"Trying to Pickup {m_spottedItem.gameObject.name}");
                        var pickedUpInstance = humanoid.PickupPrefab(m_spottedItem.m_itemData.m_dropPrefab);

                        humanoid.GetInventory().Print();

                        humanoid.EquipItem(pickedUpInstance);
                        if (m_spottedItem.m_itemData.m_stack == 1)
                        {
                            if (___m_nview.GetZDO() == null)
                            {
                                Destroy(m_spottedItem.gameObject);
                            }
                            else
                            {
                                ZNetScene.instance.Destroy(m_spottedItem.gameObject);
                            }
                        }
                        else
                        {
                            m_spottedItem.m_itemData.m_stack--;
                            Traverse.Create(m_spottedItem).Method("Save").GetValue();
                        }
                        m_carrying = pickedUpInstance;
                        m_spottedItem = null;
                        m_fetchitems.Clear();
                        m_stateChangeTimer = 0;
                        return;
                    }
                }

                UpdateAiStatus(___m_nview, $"Done with assignment");
                if (m_carrying != null)
                {
                    humanoid.UnequipItem(m_carrying, false);
                    m_carrying = null;
                    UpdateAiStatus(___m_nview, $"Dropping unused item");
                }
                m_fetchitems.Clear();
                m_spottedItem = null;
                m_containers.Clear();
                m_searchcontainer = false;
                m_assigned = false;
                m_stateChangeTimer = 0;
                return;
            }

            UpdateAiStatus(___m_nview, "Random movement (No new assignments found)");
            typeof(MonsterAI).GetMethod("IdleMovement", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(monsterAi, new object[] { dt });

        }
        public static string UpdateAiStatus(ZNetView nview, string newStatus)
        {
            string currentAiStatus = nview?.GetZDO()?.GetString(Constants.Z_AiStatus);
            if (currentAiStatus != newStatus)
            {
                string name = nview?.GetZDO()?.GetString(Constants.Z_GivenName);
                Debug.Log($"{name}: {newStatus}");
                nview.GetZDO().Set(Constants.Z_AiStatus, newStatus);
            }
            return newStatus;
        }



    }
}
