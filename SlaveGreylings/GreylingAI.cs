using HarmonyLib;
using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlaveGreylings
{
    public class GreylingAI : MobAIBase
    {
        public MaxStack<Assignment> m_assignment;
        public MaxStack<Container> m_containers;
        public ItemDrop.ItemData m_carrying;
        public float m_assignedTimer;
        public float m_stateChangeTimer;
        public string[] m_acceptedContainerNames;

        public enum State
        {
            Idle,
            Flee,
            Follow,
            Assigned,
            Hungry,
            SearchForItems,
            HaveFoodItem,
            HaveNoFoodItem,
            HaveAssignmentItem,
            HaveNoAssignmentItem,
            MoveToAssignment,
            CheckingAssignment,
            DoneWithAssignment,
            UnloadToAssignment
        }

        public enum Trigger
        {
            TakeDamage,
            Follow,
            UnFollow,
            CalmDown,
            Hungry,
            ConsumeItem,
            ItemFound,
            Update,
            ItemNotFound,
            SearchForItems,
            IsCloseToAssignment,
            AssignmentFinished,
            LeaveAssignment
        }

        readonly StateMachine<string,string>.TriggerWithParameters<(MonsterAI instance, float dt)> UpdateTrigger;
        readonly StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>,string,string> LookForItemTrigger;
        State m_parentState;
        private float m_triggerTimer;
        SearchForItemsBehaviour searchForItemsBehaviour;
        public GreylingAI() : base(State.Idle.ToString())
        {
            m_assignment = new MaxStack<Assignment>(20);
            m_containers = new MaxStack<Container>(GreylingsConfig.MaxContainersInMemory.Value);
            m_carrying = null;
            m_assignedTimer = 0f;
            m_stateChangeTimer = 0f;
            m_acceptedContainerNames = GreylingsConfig.IncludedContainersList.Value.Split();
            UpdateTrigger = Brain.SetTriggerParameters<(MonsterAI instance, float dt)>(Trigger.Update.ToString());
            LookForItemTrigger = Brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>,string,string>(Trigger.ItemFound.ToString());

            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Configure(this, Brain, State.SearchForItems.ToString());

            ConfigureFlee();
            ConfigureFollow();
            ConfigureIsHungry();
            ConfigureIdle();
            ConfigureAssigned();
            ConfigureMoveToAssignment();
            ConfigureCheckAsignment();
            ConfigureSearchContainers();
            ConfigureDoneWithAssignment();
            ConfigureUnloadToAssignment();
        }

        private void ConfigureSearchContainers()
        {
            Brain.Configure(State.SearchForItems.ToString())
                .SubstateOf(State.Hungry.ToString())
                .SubstateOf(State.Assigned.ToString())
                .Permit(Trigger.SearchForItems.ToString(), searchForItemsBehaviour.InitState)
                .OnEntry(t =>
                {
                    Debug.Log("ConfigureSearchContainers Initiated");
                    searchForItemsBehaviour.KnownContainers = m_containers;
                    searchForItemsBehaviour.Items = t.Parameters[0] as IEnumerable<ItemDrop.ItemData>;
                    searchForItemsBehaviour.AcceptedContainerNames = m_acceptedContainerNames;
                    searchForItemsBehaviour.SuccessState = t.Parameters[1] as string;
                    searchForItemsBehaviour.FailState = t.Parameters[2] as string;
                    Brain.Fire(Trigger.SearchForItems.ToString());
                });
        }

        private void ConfigureIdle()
        {
            Brain.Configure(State.Idle.ToString())
                .PermitIf(Trigger.TakeDamage.ToString(), State.Flee.ToString(),() => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(Trigger.Hungry.ToString(), State.Hungry.ToString(), () => (Instance as MonsterAI).Tameable().IsHungry())
                .PermitIf(UpdateTrigger, State.Assigned.ToString(), (arg) => AddNewAssignment(arg.instance.transform.position, m_assignment))
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Nothing to do, bored");
                });
        }

        private void ConfigureIsHungry()
        {
            Brain.Configure(State.Hungry.ToString())
                .PermitIf(Trigger.TakeDamage.ToString(), State.Flee.ToString(), () => TimeSinceHurt < 20 )
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .Permit(LookForItemTrigger.Trigger , State.SearchForItems.ToString())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Is hungry, no work a do");
                    Brain.Fire(LookForItemTrigger, (Instance as MonsterAI).m_consumeItems.Select(i => i.m_itemData), State.HaveFoodItem.ToString(), State.HaveNoFoodItem.ToString());
                });

            Brain.Configure(State.HaveFoodItem.ToString())
                .SubstateOf(State.Hungry.ToString())
                .Permit(Trigger.ConsumeItem.ToString(), State.Idle.ToString())
                .OnEntry(t =>
                {
                    Debug.LogWarning($"Prev state:{t.Source}, current state:{Brain.State}");
                    UpdateAiStatus(NView, "*burps*");
                    (Instance as MonsterAI).m_onConsumedItem((Instance as MonsterAI).m_consumeItems.FirstOrDefault());
                    (Instance.GetComponent<Character>() as Humanoid).m_consumeItemEffects.Create(Instance.transform.position, Quaternion.identity);
                    var animator = Instance.GetType().GetField("m_animator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(Instance) as ZSyncAnimation;
                    animator.SetTrigger("consume");
                    float ConsumeHeal = (Instance as MonsterAI).m_consumeHeal;

                    if (ConsumeHeal > 0f)
                    {
                        Instance.GetComponent<Character>().Heal(ConsumeHeal);
                    }
                    Brain.Fire(Trigger.ConsumeItem.ToString());
                });
            
            Brain.Configure(State.HaveNoFoodItem.ToString())
                .SubstateOf(State.Hungry.ToString())
                .PermitIf(Trigger.ItemNotFound.ToString(), State.Idle.ToString())
                .OnEntry(t =>
                {
                    Brain.Fire(Trigger.ItemNotFound.ToString());
                });
        }

        private void ConfigureFollow()
        {
            Brain.Configure(State.Follow.ToString())
                .PermitIf(UpdateTrigger, State.Idle.ToString(), (args) => !(bool)args.instance.GetFollowTarget())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Follow");
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    m_assignment.Clear();
                    m_containers.Clear();
                    m_stateChangeTimer = 0;
                });
        }

        private void ConfigureFlee()
        {
            Brain.Configure(State.Flee.ToString())
                .PermitIf(UpdateTrigger, State.Idle.ToString(), (args) => TimeSinceHurt >= 20f)
                .Permit(Trigger.Follow.ToString(), State.Follow.ToString())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Got hurt, flee!");
                    Instance.Alert();
                })
                .OnExit(t =>
                {
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    //m_attacker = null;
                });
        }

        private void ConfigureAssigned()
        {
            Brain.Configure(State.Assigned.ToString())
                .InitialTransition(State.MoveToAssignment.ToString())
                .PermitIf(Trigger.TakeDamage.ToString(), State.Flee.ToString(), () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(Trigger.Hungry.ToString(), State.Hungry.ToString(), () => (Instance as MonsterAI).Tameable().IsHungry())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, $"Check the {m_assignment.Peek().TypeOfAssignment.Name}, I'm on it Boss");
                    m_assignedTimer = 0;
                })
                .OnExit(t =>
                {
                    if (m_carrying != null)
                    {
                        (Character as Humanoid).DropItem((Character as Humanoid).GetInventory(), m_carrying, 1);
                        m_carrying = null;
                    }
                });

            Brain.Configure(State.HaveAssignmentItem.ToString())
                .SubstateOf(State.Assigned.ToString())
                .Permit(Trigger.ItemFound.ToString(), State.MoveToAssignment.ToString())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, $"Trying to Pickup {searchForItemsBehaviour.FoundItem.m_shared.m_name}");
                    var pickedUpInstance = (Character as Humanoid).PickupPrefab(searchForItemsBehaviour.FoundItem.m_dropPrefab);
                    (Character as Humanoid).GetInventory().Print();
                    (Character as Humanoid).EquipItem(pickedUpInstance);
                    m_carrying = pickedUpInstance;
                    Brain.Fire(Trigger.ItemFound.ToString());
                });

            Brain.Configure(State.HaveNoAssignmentItem.ToString())
                .SubstateOf(State.Assigned.ToString())
                .PermitIf(Trigger.ItemNotFound.ToString(), State.DoneWithAssignment.ToString())
                .OnEntry(t =>
                {
                    Brain.Fire(Trigger.ItemNotFound.ToString());
                });

        }

        private void ConfigureMoveToAssignment()
        {
            State nextState = State.Idle;
            Brain.Configure(State.MoveToAssignment.ToString())
                .SubstateOf(State.Assigned.ToString())
                .PermitDynamicIf(UpdateTrigger, (args) => true ? nextState.ToString() : State.Idle.ToString(), (arg) => MoveToAssignment(m_stateChangeTimer, arg.dt))
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Moving to assignment");
                    if (t.Source == State.HaveAssignmentItem.ToString())
                    {
                        nextState = State.UnloadToAssignment;
                    }
                    else
                    {
                        nextState = State.CheckingAssignment;
                    }
                });
        }

        private void ConfigureCheckAsignment()
        {
            Brain.Configure(State.CheckingAssignment.ToString())
                .SubstateOf(State.Assigned.ToString())
                .Permit(LookForItemTrigger.Trigger, State.SearchForItems.ToString())
                .Permit(Trigger.AssignmentFinished.ToString(), State.DoneWithAssignment.ToString())
                .OnEntry(t =>
                {
                    StopMoving();
                    UpdateAiStatus(NView, "Checking assignment for task");
                    var needFuel = m_assignment.Peek().NeedFuel;
                    var needOre = m_assignment.Peek().NeedOre;
                    var fetchItems = new List<ItemDrop.ItemData>();
                    SlaveGreylings.Dbgl($"Ore:{needOre.Join(j => j.m_shared.m_name)}, Fuel:{needFuel?.m_shared.m_name}");
                    if (needFuel != null)
                    {
                        fetchItems.Add(needFuel);
                        UpdateAiStatus(NView, $"Adding {needFuel.m_shared.m_name} to search list");
                    }
                    if (needOre.Any())
                    {
                        fetchItems.AddRange(needOre);
                        UpdateAiStatus(NView, $"Adding {needOre.Join(o => o.m_shared.m_name)} to search list");
                    }
                    m_stateChangeTimer = 0;
                    if (!fetchItems.Any())
                    {
                        Brain.Fire(Trigger.AssignmentFinished.ToString());
                    }
                    else
                    {
                        Brain.Fire(LookForItemTrigger, fetchItems, State.HaveAssignmentItem.ToString(), State.HaveNoAssignmentItem.ToString());
                    }
                });
        }

        private void ConfigureUnloadToAssignment()
        {
            Brain.Configure(State.UnloadToAssignment.ToString())
                .SubstateOf(State.Assigned.ToString())
                .Permit(Trigger.AssignmentFinished.ToString(), State.DoneWithAssignment.ToString())
                .OnEntry(t =>
                {
                    StopMoving();
                    var needFuel = m_assignment.Peek().NeedFuel;
                    var needOre = m_assignment.Peek().NeedOre;
                    bool isCarryingFuel = m_carrying.m_shared.m_name == needFuel?.m_shared?.m_name;
                    bool isCarryingMatchingOre = needOre?.Any(c => m_carrying.m_shared.m_name == c?.m_shared?.m_name) ?? false;

                    if (isCarryingFuel)
                    {
                        UpdateAiStatus(NView, $"Unload to {m_assignment.Peek().TypeOfAssignment.Name} -> Fuel");
                        m_assignment.Peek().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
                        (Character as Humanoid).GetInventory().RemoveOneItem(m_carrying);
                    }
                    else if (isCarryingMatchingOre)
                    {
                        UpdateAiStatus(NView, $"Unload to {m_assignment.Peek().TypeOfAssignment.Name} -> Ore");

                        m_assignment.Peek().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddOre", new object[] { Common.GetPrefabName(m_carrying.m_dropPrefab.name) });
                        (Character as Humanoid).GetInventory().RemoveOneItem(m_carrying);
                    }
                    else
                    {
                        UpdateAiStatus(NView, $"Dropping {m_carrying.m_shared.m_name} on the ground");
                        (Character as Humanoid).DropItem((Character as Humanoid).GetInventory(), m_carrying, 1);
                    }
                    (Character as Humanoid).UnequipItem(m_carrying, false);

                    Brain.Fire(Trigger.AssignmentFinished.ToString());
                });
        }

        private void ConfigureDoneWithAssignment()
        {
            Brain.Configure(State.DoneWithAssignment.ToString())
                .SubstateOf(State.Assigned.ToString())
                .Permit(Trigger.LeaveAssignment.ToString(), State.Idle.ToString())
                .OnEntry(t =>
                {
                    if (m_carrying != null)
                    {
                        UpdateAiStatus(NView, $"Dropping {m_carrying.m_shared.m_name} on the ground");
                        (Character as Humanoid).DropItem((Character as Humanoid).GetInventory(), m_carrying, 1);
                        m_carrying = null;
                    }
                    m_stateChangeTimer = 0;
                    UpdateAiStatus(NView, $"Done doin worksignment!");
                    Brain.Fire(Trigger.LeaveAssignment.ToString());
                });
        }

        public override void UpdateAI(BaseAI instance, float dt)
        {
            base.UpdateAI(instance, dt);
            m_triggerTimer += dt;
            if (m_triggerTimer < 0.1f) return;
            m_triggerTimer = 0f;
            var monsterAi = instance as MonsterAI;
            Vector3 greylingPosition = this.Character.transform.position;

            SlaveGreylings.Dbgl($"Greyling:{greylingPosition}, monsterAi:{monsterAi.transform.position}");


            Brain.Fire(Trigger.TakeDamage.ToString());
            Brain.Fire(Trigger.Follow.ToString());
            Brain.Fire(Trigger.Hungry.ToString());
            Brain.Fire(UpdateTrigger, (monsterAi, dt));


            if (Brain.IsInState(State.Flee.ToString()))
            {
                Brain.Fire(Trigger.CalmDown.ToString());
                //var fleeFrom = m_attacker == null ? ___m_character.transform.position : m_attacker.transform.position;
                Invoke<MonsterAI>(instance, "Flee", dt, Character.transform.position);
                return;
            }

            if (Brain.IsInState(State.Follow.ToString()))
            {
                Invoke<MonsterAI>(instance, "Follow", monsterAi.GetFollowTarget(), dt);
                return;
            }

            if (Brain.IsInState(searchForItemsBehaviour.InitState))
            {
                searchForItemsBehaviour.Update(this, dt);
                return;
            }

            // Here starts the fun.

            //Assigned timeout-function 
            m_assignedTimer += dt;
            if (m_assignedTimer > GreylingsConfig.TimeLimitOnAssignment.Value) m_assigned = false;

            //Assignment timeout-function

            m_assigned = Common.AssignmentTimeoutCheck(ref m_assignment, dt);

            //stateChangeTimer Updated
            m_stateChangeTimer += dt;
            if (m_stateChangeTimer < 1) return;

            return;

            //    var humanoid = this.Character as Humanoid;
            //    Assignment assignment = m_assignment.Peek();


            //    bool isLookingAtAssignment = (bool)Invoke<MonsterAI>(instance, "IsLookingAt", assignment.Position, 20f);
            //    if (isCarryingItem && assignment.IsClose(greylingPosition) && !isLookingAtAssignment)
            //    {
            //        UpdateAiStatus(NView, $"Looking at Assignment: {assignment.TypeOfAssignment.Name} ");
            //        humanoid.SetMoveDir(Vector3.zero);
            //        Invoke<MonsterAI>(instance, "LookAt", assignment.Position);
            //        return;
            //    }

            //    if (isCarryingItem && assignment.IsCloseEnough(greylingPosition))
            //    {
            //        humanoid.SetMoveDir(Vector3.zero);
            //        var needFuel = assignment.NeedFuel;
            //        var needOre = assignment.NeedOre;
            //        bool isCarryingFuel = m_carrying.m_shared.m_name == needFuel?.m_shared?.m_name;
            //        bool isCarryingMatchingOre = needOre?.Any(c => m_carrying.m_shared.m_name == c?.m_shared?.m_name) ?? false;

            //        if (isCarryingFuel)
            //        {
            //            UpdateAiStatus(NView, $"Unload to {assignment.TypeOfAssignment.Name} -> Fuel");
            //            assignment.AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
            //            humanoid.GetInventory().RemoveOneItem(m_carrying);
            //        }
            //        else if (isCarryingMatchingOre)
            //        {
            //            UpdateAiStatus(NView, $"Unload to {assignment.TypeOfAssignment.Name} -> Ore");

            //            assignment.AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddOre", new object[] { Common.GetPrefabName(m_carrying.m_dropPrefab.name) });
            //            humanoid.GetInventory().RemoveOneItem(m_carrying);
            //        }
            //        else
            //        {
            //            UpdateAiStatus(NView, $"Dropping {m_carrying.m_shared.m_name} on the ground");
            //            humanoid.DropItem(humanoid.GetInventory(), m_carrying, 1);
            //        }

            //        humanoid.UnequipItem(m_carrying, false);
            //        m_carrying = null;
            //        m_fetchitems.Clear();
            //        m_stateChangeTimer = 0;
            //        return;
            //    }

            //    if (!knowWhattoFetch && assignment.IsCloseEnough(greylingPosition))
            //    {
            //        humanoid.SetMoveDir(Vector3.zero);
            //        UpdateAiStatus(NView, "Checking assignment for task");
            //        var needFuel = assignment.NeedFuel;
            //        var needOre = assignment.NeedOre;
            //        SlaveGreylings.Dbgl($"Ore:{needOre.Join(j => j.m_shared.m_name)}, Fuel:{needFuel?.m_shared.m_name}");
            //        if (needFuel != null)
            //        {
            //            m_fetchitems.Add(needFuel);
            //            UpdateAiStatus(NView, $"Adding {needFuel.m_shared.m_name} to search list");
            //        }
            //        if (needOre.Any())
            //        {
            //            m_fetchitems.AddRange(needOre);
            //            UpdateAiStatus(NView, $"Adding {needOre.Join(o => o.m_shared.m_name)} to search list");
            //        }
            //        if (!m_fetchitems.Any())
            //        {
            //            m_assigned = false;
            //        }
            //        m_stateChangeTimer = 0;
            //        return;
            //    }

            //    bool hasSpottedAnItem = m_spottedItem != null;
            //    bool searchForItemToPickup = knowWhattoFetch && !hasSpottedAnItem && !isCarryingItem && !m_searchcontainer;
            //    if (searchForItemToPickup)
            //    {
            //        UpdateAiStatus(NView, "Search the ground for item to pickup");
            //        ItemDrop spottedItem = Common.GetNearbyItem(greylingPosition, m_fetchitems, GreylingsConfig.ItemSearchRadius.Value);
            //        if (spottedItem != null)
            //        {
            //            m_spottedItem = spottedItem;
            //            m_stateChangeTimer = 0;
            //            return;
            //        }

            //        UpdateAiStatus(NView, "Trying to remeber content of known Chests");
            //        foreach (Container chest in m_containers)
            //        {
            //            foreach (var fetchItem in m_fetchitems)
            //            {
            //                ItemDrop.ItemData item = chest?.GetInventory()?.GetItem(fetchItem.m_shared.m_name);
            //                if (item == null) continue;
            //                else
            //                {
            //                    UpdateAiStatus(NView, "Item found in old chest");
            //                    m_containers.Remove(chest);
            //                    m_containers.Push(chest);
            //                    m_searchcontainer = true;
            //                    m_stateChangeTimer = 0;
            //                    return;
            //                }
            //            }
            //        }

            //        UpdateAiStatus(NView, "Search for nerby Chests");
            //        Container nearbyChest = Common.FindRandomNearbyContainer(greylingPosition, m_containers, m_acceptedContainerNames);
            //        if (nearbyChest != null)
            //        {
            //            UpdateAiStatus(NView, "Chest found");
            //            m_containers.Push(nearbyChest);
            //            m_searchcontainer = true;
            //            m_stateChangeTimer = 0;
            //            return;
            //        }
            //    }

            //    if (m_searchcontainer)
            //    {
            //        bool containerIsInvalid = m_containers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
            //        if (containerIsInvalid)
            //        {
            //            m_containers.Pop();
            //            m_searchcontainer = false;
            //            return;
            //        }
            //        bool isCloseToContainer = Vector3.Distance(greylingPosition, m_containers.Peek().transform.position) < 1.5;
            //        if (!isCloseToContainer)
            //        {
            //            UpdateAiStatus(NView, "Heading to Container");
            //            Invoke<MonsterAI>(instance, "MoveAndAvoid", dt, m_containers.Peek().transform.position, 0.5f, false);
            //            return;
            //        }
            //        else
            //        {
            //            humanoid.SetMoveDir(Vector3.zero);
            //            UpdateAiStatus(NView, $"Chest inventory:{m_containers.Peek()?.GetInventory().GetAllItems().Join(i => i.m_shared.m_name)} from Chest ");
            //            var wantedItemsInChest = m_containers.Peek()?.GetInventory()?.GetAllItems()?.Where(i => m_fetchitems.Contains(i));
            //            foreach (var fetchItem in m_fetchitems)
            //            {
            //                ItemDrop.ItemData item = m_containers.Peek()?.GetInventory()?.GetItem(fetchItem.m_shared.m_name);
            //                if (item == null) continue;
            //                else
            //                {
            //                    UpdateAiStatus(NView, $"Trying to Pickup {item} from Chest ");
            //                    var pickedUpInstance = humanoid.PickupPrefab(item.m_dropPrefab);
            //                    humanoid.GetInventory().Print();
            //                    humanoid.EquipItem(pickedUpInstance);
            //                    m_containers.Peek().GetInventory().RemoveItem(item, 1);
            //                    Invoke<Container>(m_containers.Peek(), "Save");
            //                    Invoke<Inventory>(m_containers.Peek(), "Changed");
            //                    m_carrying = pickedUpInstance;
            //                    m_spottedItem = null;
            //                    m_fetchitems.Clear();
            //                    m_searchcontainer = false;
            //                    m_stateChangeTimer = 0;
            //                    return;
            //                }
            //            }

            //            m_searchcontainer = false;
            //            m_stateChangeTimer = 0;
            //            return;
            //        }
            //    }

            //    if (hasSpottedAnItem)
            //    {
            //        bool isNotCloseToPickupItem = Vector3.Distance(greylingPosition, m_spottedItem.transform.position) > 1;
            //        if (isNotCloseToPickupItem)
            //        {
            //            UpdateAiStatus(NView, "Heading to pickup item");
            //            Invoke<MonsterAI>(instance, "MoveAndAvoid", dt, m_spottedItem.transform.position, 0.5f, false);
            //            return;
            //        }
            //        else // Pickup item from ground
            //        {
            //            humanoid.SetMoveDir(Vector3.zero);
            //            UpdateAiStatus(NView, $"Trying to Pickup {m_spottedItem.gameObject.name}");
            //            var pickedUpInstance = humanoid.PickupPrefab(m_spottedItem.m_itemData.m_dropPrefab);

            //            humanoid.GetInventory().Print();

            //            humanoid.EquipItem(pickedUpInstance);
            //            if (m_spottedItem.m_itemData.m_stack == 1)
            //            {
            //                if (NView.GetZDO() == null)
            //                {
            //                    SlaveGreylings.Destroy(m_spottedItem.gameObject);
            //                }
            //                else
            //                {
            //                    ZNetScene.instance.Destroy(m_spottedItem.gameObject);
            //                }
            //            }
            //            else
            //            {
            //                m_spottedItem.m_itemData.m_stack--;
            //                Traverse.Create(m_spottedItem).Method("Save").GetValue();
            //            }
            //            m_carrying = pickedUpInstance;
            //            m_spottedItem = null;
            //            m_fetchitems.Clear();
            //            m_stateChangeTimer = 0;
            //            return;
            //        }
            //    }

            //    UpdateAiStatus(NView, $"Done with assignment");
            //    if (m_carrying != null)
            //    {
            //        humanoid.UnequipItem(m_carrying, false);
            //        m_carrying = null;
            //        UpdateAiStatus(NView, $"Dropping unused item");
            //    }
            //    m_fetchitems.Clear();
            //    m_spottedItem = null;
            //    m_containers.Clear();
            //    m_searchcontainer = false;
            //    m_assigned = false;
            //    m_stateChangeTimer = 0;
            //    return;
            //}

            //UpdateAiStatus(NView, "Random movement (No new assignments found)");
            //Invoke<MonsterAI>(instance, "IdleMovement", dt);

        }

        public bool AddNewAssignment(Vector3 center, MaxStack<Assignment> KnownAssignments)
        {
            Assignment newassignment = Common.FindRandomNearbyAssignment(center, KnownAssignments);
            if (newassignment != null)
            {
                KnownAssignments.Push(newassignment);
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public bool MoveToAssignment(float StateChangeTimer, float dt)
        {
            bool assignmentIsInvalid = m_assignment.Peek()?.AssignmentObject?.GetComponent<ZNetView>()?.IsValid() == false;
            if (assignmentIsInvalid)
            {
                m_assignment.Pop();
                return true; 
            }
            MoveAndAvoidFire(m_assignment.Peek().Position, dt, 0.5f);
            if (StateChangeTimer > 30 || m_assignment.Peek().IsClose(Instance.transform.position))
            {
                return true;
            }
            return false;
        }
    }
}
