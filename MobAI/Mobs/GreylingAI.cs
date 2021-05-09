using HarmonyLib;
using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class GreylingAI : MobAIBase, IControllableMob
    {
        public MaxStack<Assignment> m_assignment;
        public MaxStack<Container> m_containers;
        public ItemDrop.ItemData m_carrying;
        public float m_assignedTimer;
        public float m_foodsearchtimer;
        public string[] m_acceptedContainerNames;
        

        public enum State
        {
            Idle,
            Flee,
            Follow,
            Assigned,
            Hungry,
            SearchForItems,
            SearchForFood,
            HaveFoodItem,
            HaveNoFoodItem,
            HaveAssignmentItem,
            HaveNoAssignmentItem,
            MoveToAssignment,
            CheckingAssignment,
            DoneWithAssignment,
            UnloadToAssignment,
            MoveAwayFrom
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
            AssignmentTimedOut,
            AssignmentFinished,
            LeaveAssignment,
            ShoutedAt
        }

        readonly StateMachine<string, string>.TriggerWithParameters<(MonsterAI instance, float dt)> UpdateTrigger;
        readonly StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;
        private float m_triggerTimer;
        SearchForItemsBehaviour searchForItemsBehaviour;
        private float m_closeEnoughTimer;
        private float m_searchForNewAssignmentTimer;
        private float m_shoutedAtTimer;
        public float CloseEnoughTimeout { get; private set; } = 30;

        public GreylingAI() : base()
        { }

        public GreylingAI(MonsterAI instance) : base(instance, State.Idle.ToString())
        {
            m_assignment = new MaxStack<Assignment>(20);
            m_containers = new MaxStack<Container>(GreylingsConfig.MaxContainersInMemory.Value);
            m_carrying = null;
            m_assignedTimer = 0f;
            m_foodsearchtimer = 0f;
            m_acceptedContainerNames = GreylingsConfig.IncludedContainersList.Value.Split();
            UpdateTrigger = Brain.SetTriggerParameters<(MonsterAI instance, float dt)>(Trigger.Update.ToString());
            LookForItemTrigger = Brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>(Trigger.ItemFound.ToString());

            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Configure(this, Brain, State.SearchForItems.ToString());
            NView.Register<string, string>(Constants.Z_updateTrainedAssignments, (long source, string uniqueID, string trainedAssignments) =>
            {
                if (NView.IsOwner()) return;
                if (UniqueID == uniqueID)
                {
                    m_trainedAssignments.Clear();
                    m_trainedAssignments.AddRange(trainedAssignments.Split());
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "A greyling learned a new skill.");
                }
            });
            m_trainedAssignments.AddRange(NView.GetZDO().GetString(Constants.Z_trainedAssignments).Split());
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
            ConfigureShoutedAt();
        }

        private void ConfigureSearchContainers()
        {
            Brain.Configure(State.SearchForItems.ToString())
                .PermitIf(Trigger.TakeDamage.ToString(), State.Flee.ToString(), () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .Permit(Trigger.SearchForItems.ToString(), searchForItemsBehaviour.InitState)
                .Permit(Trigger.ShoutedAt.ToString(), State.MoveAwayFrom.ToString())
                .OnEntry(t =>
                {
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
                .PermitIf(Trigger.TakeDamage.ToString(), State.Flee.ToString(), () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(Trigger.Hungry.ToString(), State.Hungry.ToString(), () => (Instance as MonsterAI).Tameable().IsHungry())
                .Permit(Trigger.ShoutedAt.ToString(), State.MoveAwayFrom.ToString())
                .PermitIf(UpdateTrigger, State.Assigned.ToString(), (arg) =>
                {
                    if ((m_searchForNewAssignmentTimer += arg.dt) < 2) return false;
                    m_searchForNewAssignmentTimer = 0f;
                    return AddNewAssignment(arg.instance, m_assignment);
                })
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Nothing to do, bored");
                });
        }

        private void ConfigureIsHungry()
        {
            Brain.Configure(State.Hungry.ToString())
                .PermitIf(Trigger.TakeDamage.ToString(), State.Flee.ToString(), () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(UpdateTrigger, State.SearchForFood.ToString(), (arg) => (m_foodsearchtimer += arg.dt) > 10)
                .Permit(Trigger.ShoutedAt.ToString(), State.MoveAwayFrom.ToString())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Is hungry, no work a do");
                    m_foodsearchtimer = 0f;
                });

            Brain.Configure(State.SearchForFood.ToString())
                .SubstateOf(State.Hungry.ToString())
                .Permit(LookForItemTrigger.Trigger, State.SearchForItems.ToString())
                .OnEntry(t =>
                {
                    Brain.Fire(LookForItemTrigger, (Instance as MonsterAI).m_consumeItems.Select(i => i.m_itemData), State.HaveFoodItem.ToString(), State.HaveNoFoodItem.ToString());
                });

            Brain.Configure(State.HaveFoodItem.ToString())
                .SubstateOf(State.Hungry.ToString())
                .Permit(Trigger.ConsumeItem.ToString(), State.Idle.ToString())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "*burps*");
                    (Instance as MonsterAI).m_onConsumedItem((Instance as MonsterAI).m_consumeItems.FirstOrDefault());
                    (Instance.GetComponent<Character>() as Humanoid).m_consumeItemEffects.Create(Instance.transform.position, Quaternion.identity);
                    var animator = Instance.GetType().GetField("m_animator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Instance) as ZSyncAnimation;
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
                .PermitIf(Trigger.ItemNotFound.ToString(), State.Hungry.ToString())
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
                });
        }

        private void ConfigureFlee()
        {
            Brain.Configure(State.Flee.ToString())
                .PermitIf(UpdateTrigger, State.Idle.ToString(), (args) => TimeSinceHurt >= 20f)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Got hurt, flee!");
                    Instance.Alert();
                })
                .OnExit(t =>
                {
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    Attacker = null;
                    Character.SetMoveDir(Vector3.zero);
                });
        }

        private void ConfigureShoutedAt()
        {
            Brain.Configure(State.MoveAwayFrom.ToString())
                .PermitIf(UpdateTrigger, State.Idle.ToString(), (args) => (m_shoutedAtTimer += args.dt) >= 1f)
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Ahhh Scary!");
                    Instance.Alert();
                })
                .OnExit(t =>
                {
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    Attacker = null;
                    Character.SetMoveDir(Vector3.zero);
                });
        }

        private void ConfigureAssigned()
        {
            Brain.Configure(State.Assigned.ToString())
                .InitialTransition(State.MoveToAssignment.ToString())
                .PermitIf(Trigger.TakeDamage.ToString(), State.Flee.ToString(), () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(Trigger.Hungry.ToString(), State.Hungry.ToString(), () => (Instance as MonsterAI).Tameable().IsHungry())
                .Permit(Trigger.AssignmentTimedOut.ToString(), State.DoneWithAssignment.ToString())
                .Permit(Trigger.ShoutedAt.ToString(), State.MoveAwayFrom.ToString())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, $"I'm on it Boss");
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
                .PermitDynamicIf(UpdateTrigger, (args) => true ? nextState.ToString() : State.Idle.ToString(), (arg) => MoveToAssignment(arg.dt))
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, $"Moving to assignment {m_assignment.Peek().TypeOfAssignment.Name}");
                    m_closeEnoughTimer = 0;
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
                    Common.Dbgl($"Ore:{needOre.Join(j => j.m_shared.m_name)}, Fuel:{needFuel?.m_shared.m_name}");
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
                .Permit(Trigger.AssignmentFinished.ToString(), State.CheckingAssignment.ToString())
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
                    m_carrying = null;

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
                    UpdateAiStatus(NView, $"Done doin worksignment!");
                    m_containers.Peek()?.SetInUse(inUse: false);
                    Brain.Fire(Trigger.LeaveAssignment.ToString());
                });
        }

        public override void UpdateAI(float dt)
        {
            base.UpdateAI(dt);
            m_triggerTimer += dt;
            if (m_triggerTimer < 0.1f) return;
            m_triggerTimer = 0f;
            var monsterAi = Instance as MonsterAI;
            Vector3 greylingPosition = this.Character.transform.position;

            //Runtime triggers
            Brain.Fire(Trigger.TakeDamage.ToString());
            Brain.Fire(Trigger.Follow.ToString());
            Brain.Fire(Trigger.Hungry.ToString());
            Brain.Fire(UpdateTrigger, (monsterAi, dt));

            //Assigned timeout-function 
            m_assignedTimer += dt;
            if (m_assignedTimer > GreylingsConfig.TimeLimitOnAssignment.Value)
            {
                Brain.Fire(Trigger.AssignmentTimedOut.ToString());
            }
            //Assignment timeout-function
            if (!Common.AssignmentTimeoutCheck(ref m_assignment, dt))
            {
                Brain.Fire(Trigger.AssignmentTimedOut.ToString());
            }

            if (Brain.IsInState(State.Flee.ToString()) || Brain.IsInState(State.MoveAwayFrom.ToString()))
            {
                var fleeFrom = Attacker == null ? Character.transform.position : Attacker.transform.position;
                Invoke<MonsterAI>(Instance, "Flee", dt, fleeFrom);
                return;
            }

            if (Brain.IsInState(State.Follow.ToString()))
            {
                Invoke<MonsterAI>(Instance, "Follow", monsterAi.GetFollowTarget(), dt);
                return;
            }

            if (Brain.IsInState(searchForItemsBehaviour.InitState))
            {
                searchForItemsBehaviour.Update(this, dt);
                return;
            }
        }

        public bool AddNewAssignment(BaseAI instance, MaxStack<Assignment> KnownAssignments)
        {
            Assignment newassignment = Common.FindRandomNearbyAssignment(instance, m_trainedAssignments, KnownAssignments);
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

        public bool MoveToAssignment(float dt)
        {
            bool assignmentIsInvalid = m_assignment.Peek()?.AssignmentObject?.GetComponent<ZNetView>()?.IsValid() == false;
            if (assignmentIsInvalid)
            {
                m_assignment.Pop();
                return true;
            }
            float distance = (m_closeEnoughTimer += dt) > CloseEnoughTimeout ? m_assignment.Peek().TypeOfAssignment.InteractDist : m_assignment.Peek().TypeOfAssignment.InteractDist + 1;
            return MoveAndAvoidFire(m_assignment.Peek().Position, dt, distance);
        }

        public MobInfo GetMobInfo()
        {
            return new MobInfo
            {
                Name = "Greyling",
                //Name = "RRR_Hostile_T1",
                AIType = this.GetType(),
                PreTameConsumables = GreylingsConfig.TamingItemList.Value.Split(',').Select(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault()),
                PostTameConsumables = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Coal").ToList(),
                PreTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                PostTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                TamingTime = GreylingsConfig.TamingTime.Value
            };
        }

        public override void Follow(Player player)
        {
            NView.InvokeRPC(ZNetView.Everybody, Constants.Z_MobCommand, player.GetZDOID(), "Follow");
        }

        protected override void RPC_MobCommand(long sender, ZDOID playerId, string command)
        {
            Player player = GetPlayer(playerId);
            if (!(player == null) && command == "Follow")
            {
                {
                    (Instance as MonsterAI).ResetPatrolPoint();
                    (Instance as MonsterAI).SetFollowTarget(player.gameObject);
                }
            }
        }

        public override void GotShoutedAtBy(MobAIBase mob)
        {
            Attacker = mob.Character;
            m_shoutedAtTimer = 0.0f;
            Brain.Fire(Trigger.ShoutedAt.ToString());
        }


    }
}
