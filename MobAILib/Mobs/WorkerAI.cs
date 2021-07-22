using HarmonyLib;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class WorkerAI : MobAIBase, IMobAIType
    {
        public MaxStack<Assignment> m_assignment;
        public MaxStack<Container> m_containers;
        public ItemDrop.ItemData m_carrying;

        // Timers
        private float m_closeEnoughTimer;
        private float m_searchForNewAssignmentTimer;
        private float m_shoutedAtTimer;
        private float m_fleeTimer;
        public float m_assignedTimer;
        private float m_triggerTimer;

        // Management
        private Vector3 m_startPosition;

        // Settings
        public float FleeTimeout { get; private set; } = 10f;

        private class State
        {
            public const string Root = "Root";
            public const string Idle = "Idle";
            public const string Flee = "Flee";
            public const string Fight = "Fight";
            public const string Follow = "Follow";
            public const string Assigned = "Assigned";
            public const string Hungry = "Hungry";
            public const string SearchForItems = "SearchForItems";
            public const string HaveAssignmentItem = "HaveAssignmentItem";
            public const string HaveNoAssignmentItem = "HaveNoAssignmentItem";
            public const string MoveToAssignment = "MoveToAssignment";
            public const string CheckingAssignment = "CheckingAssignment";
            public const string DoneWithAssignment = "DoneWithAssignment";
            public const string UnloadToAssignment = "UnloadToAssignment";
            public const string MoveAwayFrom = "MoveAwayFrom";
        }

        private class Trigger
        {
            public const string TakeDamage = "TakeDamage";
            public const string Follow = "Follow";
            public const string UnFollow = "UnFollow";
            public const string CalmDown = "CalmDown";
            public const string Hungry = "Hungry";
            public const string ConsumeItem = "ConsumeItem";
            public const string ItemFound = "ItemFound";
            public const string Update = "Update";
            public const string ItemNotFound = "ItemNotFound";
            public const string SearchForItems = "SearchForItems";
            public const string IsCloseToAssignment = "IsCloseToAssignment";
            public const string AssignmentTimedOut = "AssignmentTimedOut";
            public const string AssignmentFinished = "AssignmentFinished";
            public const string LeaveAssignment = "LeaveAssignment";
            public const string ShoutedAt = "ShoutedAt";
            public const string Fight = "Fight";
            public const string EnterEatBehaviour = "EnterEatBehaviour";
        }

        private readonly StateMachine<string, string>.TriggerWithParameters<(MonsterAI instance, float dt)> UpdateTrigger;
        private readonly StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;
        private readonly SearchForItemsBehaviour searchForItemsBehaviour;
        private readonly FightBehaviour fightBehaviour;
        private readonly EatingBehaviour eatingBehaviour;

        public WorkerAIConfig m_config { get; set; }

        public float CloseEnoughTimeout { get; private set; } = 30;

        public WorkerAI() : base()
        { }

        public WorkerAI(MonsterAI instance, object config) : this(instance, config as MobAIBaseConfig)
        { }

        public WorkerAI(MonsterAI instance, MobAIBaseConfig config) : base(instance, State.Idle, config)
        {
            PrintAIStateToDebug = false;

            m_config = config as WorkerAIConfig;
            m_assignment = new MaxStack<Assignment>(Intelligence);
            m_containers = new MaxStack<Container>(Intelligence);
            m_carrying = null;
            m_assignedTimer = 0f;
            if (m_startPosition == Vector3.zero)
            {
                m_startPosition = instance.transform.position;
            }

            RegisterRPCMethods();

            UpdateTrigger = Brain.SetTriggerParameters<(MonsterAI instance, float dt)>(Trigger.Update);
            LookForItemTrigger = Brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>(Trigger.ItemFound);

            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Configure(this, Brain, State.SearchForItems);
            fightBehaviour = new FightBehaviour();
            fightBehaviour.Configure(this, Brain, State.Fight);
            eatingBehaviour = new EatingBehaviour();
            eatingBehaviour.Configure(this, Brain, State.Hungry);
            eatingBehaviour.HungryTimeout = m_config.FeedDuration;
            eatingBehaviour.SearchForItemsState = State.SearchForItems;
            eatingBehaviour.SuccessState = State.Idle;
            eatingBehaviour.FailState = State.Idle;
            eatingBehaviour.HealPercentageOnConsume = 0.2f;

            ConfigureRoot();
            ConfigureFlee();
            ConfigureFollow();
            ConfigureIdle();
            ConfigureAssigned();
            ConfigureMoveToAssignment();
            ConfigureCheckAsignment();
            ConfigureSearchForItems();
            ConfigureDoneWithAssignment();
            ConfigureUnloadToAssignment();
            ConfigureShoutedAt();
            ConfigureFight();
            ConfigureHungry();
        }

        private void RegisterRPCMethods()
        {
            NView.Register<string, string>(Constants.Z_updateTrainedAssignments, (long source, string uniqueID, string trainedAssignments) =>
            {
                if (NView.IsOwner()) return;
                if (UniqueID == uniqueID)
                {
                    m_trainedAssignments.Clear();
                    m_trainedAssignments.AddRange(trainedAssignments.Split());
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "A worker learned a new skill.");
                }
            });
        }

        private void ConfigureRoot()
        {
            Brain.Configure(State.Root)
                .InitialTransition(State.Idle)
                .PermitIf(Trigger.TakeDamage, State.Fight, () => !Brain.IsInState(State.Flee) && !Brain.IsInState(State.Fight) && (TimeSinceHurt < 20.0f || Common.Alarmed(Instance, Awareness)))
                .PermitIf(Trigger.Follow, State.Follow, () => !Brain.IsInState(State.Follow) && (bool)(Instance as MonsterAI).GetFollowTarget());
        }

        private void ConfigureHungry()
        {
            Brain.Configure(State.Hungry)
                .SubstateOf(State.Root)
                .Permit(Trigger.EnterEatBehaviour, eatingBehaviour.StartState)
                .OnEntry(t =>
                {
                    Brain.Fire(Trigger.EnterEatBehaviour);
                });
        }

        private void ConfigureSearchForItems()
        {
            Brain.Configure(State.SearchForItems)
                .SubstateOf(State.Root)
                .Permit(Trigger.SearchForItems, searchForItemsBehaviour.StartState)
                .Permit(Trigger.ShoutedAt, State.MoveAwayFrom)
                .OnEntry(t =>
                {
                    searchForItemsBehaviour.KnownContainers = m_containers;
                    searchForItemsBehaviour.Items = t.Parameters[0] as IEnumerable<ItemDrop.ItemData>;
                    searchForItemsBehaviour.AcceptedContainerNames = m_config.IncludedContainers;
                    searchForItemsBehaviour.SuccessState = t.Parameters[1] as string;
                    searchForItemsBehaviour.FailState = t.Parameters[2] as string;
                    Brain.Fire(Trigger.SearchForItems);
                });
        }

        private void ConfigureIdle()
        {
            Brain.Configure(State.Idle)
                .SubstateOf(State.Root)
                .PermitIf(Trigger.Hungry, eatingBehaviour.StartState, () => eatingBehaviour.IsHungry(IsHurt))
                .Permit(Trigger.ShoutedAt, State.MoveAwayFrom)
                .PermitIf(UpdateTrigger, State.Assigned, (arg) =>
                {
                    if (Brain.IsInState(State.Assigned)) return false;
                    if ((m_searchForNewAssignmentTimer += arg.dt) < 2) return false;
                    m_searchForNewAssignmentTimer = 0f;
                    Common.Dbgl("Searching for new assignment");
                    return AddNewAssignment(arg.instance, ref m_assignment);
                })
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.Idle);
                    //m_startPosition = Instance.transform.position;
                });
        }

        private void ConfigureFollow()
        {
            Brain.Configure(State.Follow)
                .PermitIf(UpdateTrigger, State.Idle, (args) => !(bool)args.instance.GetFollowTarget())
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.Follow);
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    m_assignment.Clear();
                    m_containers.Clear();
                });
        }

        private void ConfigureFight()
        {
            Brain.Configure(State.Fight)
                .SubstateOf(State.Root)
                .Permit(Trigger.Fight, fightBehaviour.StartState)
                .OnEntry(t =>
                {
                    fightBehaviour.SuccessState = State.Idle;
                    fightBehaviour.FailState = State.Flee;
                    fightBehaviour.m_mobilityLevel =  Mobility;
                    fightBehaviour.m_agressionLevel = Agressiveness;
                    fightBehaviour.m_awarenessLevel = Awareness;
                    Brain.Fire(Trigger.Fight);
                })
                .OnExit(t =>
                {
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                });
        }
        private void ConfigureFlee()
        {
            Brain.Configure(State.Flee)
                .SubstateOf(State.Root)
                .PermitIf(UpdateTrigger, State.Idle, (args) => (m_fleeTimer += args.dt) > FleeTimeout && !Common.Alarmed(args.instance, Mathf.Max(1, Awareness-1)))
                .OnEntry(t =>
                {
                    m_fleeTimer = 0f;
                    UpdateAiStatus(State.Flee);
                    Instance.Alert();
                })
                .OnExit(t =>
                {
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    Attacker = null;
                    StopMoving();
                });
        }

        private void ConfigureShoutedAt()
        {
            Brain.Configure(State.MoveAwayFrom)
                .SubstateOf(State.Idle)
                .PermitIf(UpdateTrigger, State.Idle, (args) => (m_shoutedAtTimer += args.dt) >= 1f)
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.MoveAwayFrom);
                    Instance.Alert();
                })
                .OnExit(t =>
                {
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    Attacker = null;
                    StopMoving();
                });
        }

        private void ConfigureAssigned()
        {
            Brain.Configure(State.Assigned)
                .SubstateOf(State.Idle)
                .InitialTransition(State.MoveToAssignment)
                .PermitIf(Trigger.TakeDamage, State.Fight, () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow, State.Follow, () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .Permit(Trigger.AssignmentTimedOut, State.DoneWithAssignment)
                .Permit(Trigger.ShoutedAt, State.MoveAwayFrom)
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.Assigned);
                    m_assignedTimer = 0;
                    m_startPosition = Instance.transform.position;
                })
                .OnExit(t =>
                {
                    if (m_carrying != null)
                    {
                        (Character as Humanoid).DropItem((Character as Humanoid).GetInventory(), m_carrying, 1);
                        m_carrying = null;
                    }
                });

            Brain.Configure(State.HaveAssignmentItem)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.ItemFound, State.MoveToAssignment)
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.HaveAssignmentItem, searchForItemsBehaviour.FoundItem.m_shared.m_name);
                    var pickedUpInstance = (Character as Humanoid).PickupPrefab(searchForItemsBehaviour.FoundItem.m_dropPrefab);
                    (Character as Humanoid).EquipItem(pickedUpInstance);
                    m_carrying = pickedUpInstance;
                    Brain.Fire(Trigger.ItemFound);
                });

            Brain.Configure(State.HaveNoAssignmentItem)
                .SubstateOf(State.Assigned)
                .PermitIf(Trigger.ItemNotFound, State.DoneWithAssignment)
                .OnEntry(t =>
                {
                    Brain.Fire(Trigger.ItemNotFound);
                });
        }

        private void ConfigureMoveToAssignment()
        {
            string nextState = State.Idle;
            Brain.Configure(State.MoveToAssignment)
                .SubstateOf(State.Assigned)
                .PermitDynamicIf(UpdateTrigger, (args) => true ? nextState : State.Idle, (arg) => MoveToAssignment(arg.dt))
                .Permit(Trigger.LeaveAssignment, State.Idle)
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.MoveToAssignment);
                    m_closeEnoughTimer = 0;
                    if (t.Source == State.HaveAssignmentItem)
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
            Brain.Configure(State.CheckingAssignment)
                .SubstateOf(State.Assigned)
                .Permit(LookForItemTrigger.Trigger, State.SearchForItems)
                .Permit(Trigger.AssignmentFinished, State.DoneWithAssignment)
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.CheckingAssignment, m_assignment.Peek().TypeOfAssignment.Name);
                    StopMoving();
                    var needFuel = m_assignment.Peek().NeedFuel;
                    var needOre = m_assignment.Peek().NeedOre;
                    var fetchItems = new List<ItemDrop.ItemData>();
                    Common.Dbgl($"{Character.GetHoverName()}:Ore:{needOre.Join(j => j.m_shared.m_name)}, Fuel:{needFuel?.m_shared.m_name}");
                    if (needFuel != null)
                    {
                        fetchItems.Add(needFuel);
                    }
                    if (needOre.Any())
                    {
                        fetchItems.AddRange(needOre);
                    }
                    if (!fetchItems.Any())
                    {
                        Brain.Fire(Trigger.AssignmentFinished);
                    }
                    else
                    {
                        Brain.Fire(LookForItemTrigger, fetchItems, State.HaveAssignmentItem, State.HaveNoAssignmentItem);
                    }
                });
        }

        private void ConfigureUnloadToAssignment()
        {
            Brain.Configure(State.UnloadToAssignment)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.AssignmentFinished, State.CheckingAssignment)
                .OnEntry(t =>
                {
                    StopMoving();
                    var needFuel = m_assignment.Peek().NeedFuel;
                    var needOre = m_assignment.Peek().NeedOre;
                    bool isCarryingFuel = m_carrying.m_shared.m_name == needFuel?.m_shared?.m_name;
                    bool isCarryingMatchingOre = needOre?.Any(c => m_carrying.m_shared.m_name == c?.m_shared?.m_name) ?? false;

                    UpdateAiStatus(State.UnloadToAssignment, m_assignment.Peek().TypeOfAssignment.Name);
                    if (isCarryingFuel)
                    {
                        m_assignment.Peek().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
                        (Character as Humanoid).GetInventory().RemoveOneItem(m_carrying);
                    }
                    else if (isCarryingMatchingOre)
                    {
                        m_assignment.Peek().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddOre", new object[] { Common.GetPrefabName(m_carrying.m_dropPrefab.name) });
                        (Character as Humanoid).GetInventory().RemoveOneItem(m_carrying);
                    }
                    else
                    {
                        (Character as Humanoid).DropItem((Character as Humanoid).GetInventory(), m_carrying, 1);
                    }
                    (Character as Humanoid).UnequipItem(m_carrying, false);
                    m_carrying = null;

                    Brain.Fire(Trigger.AssignmentFinished);
                });
        }

        private void ConfigureDoneWithAssignment()
        {
            Brain.Configure(State.DoneWithAssignment)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.LeaveAssignment, State.Idle)
                .OnEntry(t =>
                {
                    if (m_carrying != null)
                    {
                        Common.Dbgl($"{Character.GetHoverName()}:Dropping {m_carrying.m_shared.m_name} on the ground");
                        (Character as Humanoid).DropItem((Character as Humanoid).GetInventory(), m_carrying, 1);
                        m_carrying = null;
                    }
                    UpdateAiStatus(State.DoneWithAssignment);
                    m_containers.Peek()?.SetInUse(inUse: false);
                    Brain.Fire(Trigger.LeaveAssignment);
                });
        }

        private string m_lastState = "";

        public override void UpdateAI(float dt)
        {
            if (Brain.State != m_lastState)
            {
                Common.Dbgl($"{Character.GetHoverName()}:State:{Brain.State}");
                m_lastState = Brain.State;
            }

            base.UpdateAI(dt);
            m_triggerTimer += dt;
            if (m_triggerTimer < 0.1f) return;
            m_triggerTimer = 0f;
            var monsterAi = Instance as MonsterAI;

            eatingBehaviour.Update(this, dt);

            //Runtime triggers
            Brain.Fire(Trigger.TakeDamage);
            Brain.Fire(Trigger.Follow);
            Brain.Fire(Trigger.Hungry);
            Brain.Fire(UpdateTrigger, (monsterAi, dt));

            //Assigned timeout-function 
            m_assignedTimer += dt;
            if (m_assignedTimer > m_config.TimeLimitOnAssignment)
            {
                Brain.Fire(Trigger.AssignmentTimedOut);
            }
            //Assignment timeout-function
            if (!Common.AssignmentTimeoutCheck(ref m_assignment, dt, m_config.TimeBeforeAssignmentCanBeRepeated))
            {
                Brain.Fire(Trigger.AssignmentTimedOut);
            }

            if (Brain.IsInState(State.Flee) || Brain.IsInState(State.MoveAwayFrom))
            {
                var fleeFrom = Attacker == null ? Character.transform.position : Attacker.transform.position;
                Invoke<MonsterAI>(Instance, "Flee", dt, fleeFrom);
                return;
            }

            if (Brain.IsInState(State.Follow))
            {
                Invoke<MonsterAI>(Instance, "Follow", monsterAi.GetFollowTarget(), dt);
                return;
            }

            if (Brain.IsInState(State.SearchForItems))
            {
                searchForItemsBehaviour.Update(this, dt);
                return;
            }

            if (Brain.IsInState(State.Fight))
            {
                fightBehaviour.Update(this, dt);
                return;
            }

            if (Brain.State == State.Idle)
            {
                Common.Invoke<BaseAI>(Instance, "RandomMovement", dt, m_startPosition);
                return;
            }
        }

        public bool AddNewAssignment(BaseAI instance, ref MaxStack<Assignment> KnownAssignments)
        {
            Assignment newassignment = Common.FindRandomNearbyAssignment(instance, m_trainedAssignments, KnownAssignments, Awareness * 5);
            if (newassignment != null)
            {
                Common.Dbgl($"{Character.GetHoverName()}:Found new assignment:{newassignment.TypeOfAssignment.Name}");
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
            if (!m_assignment.Any())
            {
                Common.Dbgl($"{Character.GetHoverName()}:No assignments to move to");
                Brain.Fire(Trigger.LeaveAssignment);
                return true;
            }
            if (!(bool)m_assignment.Peek()?.AssignmentObject)
            {
                Common.Dbgl("AssignmentObject is null");
                m_assignment.Pop();
                Brain.Fire(Trigger.LeaveAssignment);
                return true;
            }
            bool assignmentIsInvalid = m_assignment.Peek()?.AssignmentObject?.GetComponent<ZNetView>()?.IsValid() == false;
            if (assignmentIsInvalid)
            {
                Common.Dbgl("AssignmentObject is invalid");
                m_assignment.Pop();
                Brain.Fire(Trigger.LeaveAssignment);
                return true;
            }
            float distance = (m_closeEnoughTimer += dt) > CloseEnoughTimeout ? m_assignment.Peek().TypeOfAssignment.InteractDist : m_assignment.Peek().TypeOfAssignment.InteractDist + 1;
            return MoveAndAvoidFire(m_assignment.Peek().Position, dt, distance);
        }

        public MobAIInfo GetMobAIInfo()
        {
            return new MobAIInfo
            {
                Name = "Worker",
                AIType = this.GetType(),
                ConfigType = typeof(WorkerAIConfig)
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
                (Instance as MonsterAI).ResetPatrolPoint();
                (Instance as MonsterAI).SetFollowTarget(player.gameObject);
            }
        }

        public override void GotShoutedAtBy(MobAIBase mob)
        {
            Attacker = mob.Character;
            m_shoutedAtTimer = 0.0f;
            Brain.Fire(Trigger.ShoutedAt);
        }
    }
}
