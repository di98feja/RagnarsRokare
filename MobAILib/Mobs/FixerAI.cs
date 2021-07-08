using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace RagnarsRokare.MobAI
{
    public class FixerAI : MobAIBase, IMobAIType
    {
        public MaxStack<Piece> m_assignment = new MaxStack<Piece>(100);
        public MaxStack<Container> m_containers;

        // Timers
        private float m_searchForNewAssignmentTimer;
        private float m_triggerTimer;
        private float m_assignedTimer;
        private float m_closeEnoughTimer;
        private float m_repairTimer;
        private float m_roarTimer;
        private float m_lastSuccessfulFindAssignment;
        private float m_lastFailedFindAssignment;
        private float m_stuckInIdleTimer;
        private float m_fleeTimer;

        // Management
        public Vector3 m_startPosition;

        // Settings
        public float CloseEnoughTimeout { get; private set; } = 10;
        public float RepairTimeout { get; private set; } = 5;
        public float RoarTimeout { get; private set; } = 10;
        public float RepairMinDist { get; private set; } = 2.0f;
        public float AdjustAssignmentStackSizeTime { get; private set; } = 60;
        public float FleeTimeout { get; private set; } = 10f;

        public class State
        {
            public const string Idle = "Idle";
            public const string Follow = "Follow";
            public const string Fight = "Fight";
            public const string Flee = "Flee";
            public const string Assigned = "Assigned";
            public const string SearchForItems = "SearchForItems";
            public const string MoveToAssignment = "MoveToAssignment";
            public const string CheckRepairState = "CheckRepairState";
            public const string RepairAssignment = "RepairAssignment";
            public const string Root = "Root";
            public const string Hungry = "Hungry";
            public const string TurnToFaceAssignment = "TurnToFaceAssignment";
        }

        private class Trigger
        {
            public const string Update = "Update";
            public const string TakeDamage = "TakeDamage";
            public const string Follow = "Follow";
            public const string Hungry = "Hungry";
            public const string ItemFound = "ItemFound";
            public const string ConsumeItem = "ConsumeItem";
            public const string ItemNotFound = "ItemNotFound";
            public const string SearchForItems = "SearchForItems";
            public const string AssignmentTimedOut = "AssignmentTimedOut";
            public const string RepairNeeded = "RepairNeeded";
            public const string RepairDone = "RepairDone";
            public const string Failed = "Failed";
            public const string Fight = "Fight";
            public const string EnterEatBehaviour = "EnterEatBehaviour";
        }

        readonly StateMachine<string, string>.TriggerWithParameters<(MonsterAI instance, float dt)> UpdateTrigger;
        readonly StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;
        readonly SearchForItemsBehaviour searchForItemsBehaviour;
        readonly FightBehaviour fightBehaviour;
        readonly EatingBehaviour eatingBehaviour;

        FixerAIConfig m_config;

        public FixerAI() : base()
        { }

        public FixerAI(MonsterAI instance, object config) : this(instance, config as MobAIBaseConfig)
        { }

        public FixerAI(MonsterAI instance, MobAIBaseConfig config) : base(instance, State.Idle, config)
        {
            PrintAIStateToDebug = false;

            m_config = config as FixerAIConfig;
            m_containers = new MaxStack<Container>(Intelligence);

            if (instance.m_consumeHeal == 0.0f)
            {
                instance.m_consumeHeal = Character.GetMaxHealth() * 0.25f;
            }

            if (m_startPosition == Vector3.zero)
            {
                m_startPosition = instance.transform.position;
            }

            var loadedAssignments = NView.GetZDO().GetString(Constants.Z_SavedAssignmentList);
            if (!string.IsNullOrEmpty(loadedAssignments))
            {
                var assignmentList = loadedAssignments.Split(',');
                var allPieces = typeof(Piece).GetField("m_allPieces", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as IEnumerable<Piece>;
                var pieceDict = allPieces.Where(p => Common.GetNView(p)?.IsValid() ?? false).ToDictionary(p => Common.GetOrCreateUniqueId(Common.GetNView(p)));
                Common.Dbgl($"Loading {assignmentList.Count()} assignments");
                foreach (var p in assignmentList)
                {
                    if (pieceDict.ContainsKey(p))
                    {
                        m_assignment.Push(pieceDict[p]);
                    }
                }
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
            eatingBehaviour.HungryTimeout = m_config.PostTameFeedDuration;
            eatingBehaviour.SearchForItemsState = State.SearchForItems;
            eatingBehaviour.SuccessState = State.Idle;
            eatingBehaviour.FailState = State.Idle;
            eatingBehaviour.HealPercentageOnConsume = 0.2f;

            ConfigureRoot();
            ConfigureIdle();
            ConfigureFollow();
            ConfigureSearchForItems();
            ConfigureAssigned();
            ConfigureFlee();
            ConfigureFight();
            ConfigureHungry();
            var graph = new Stateless.Graph.StateGraph(Brain.GetInfo());
            //Debug.Log(graph.ToGraph(new Stateless.Graph.UmlDotGraphStyle()));
        }

        private void RegisterRPCMethods()
        {
            NView.Register(Constants.Z_AddAssignment, (long source, string assignment) =>
            {
                if (NView.IsOwner())
                {
                    Common.Dbgl($"Saving {m_assignment.Count()} assignments");
                    Common.Dbgl($"Removed {m_assignment.Where(p => !Common.GetNView(p).IsValid()).Count()} invalid assignments");
                    var assignmentsToRemove = m_assignment.Where(p => !Common.GetNView(p).IsValid()).ToList();
                    foreach (var piece in assignmentsToRemove)
                    {
                        m_assignment.Remove(piece);
                    }
                    NView.GetZDO().Set(Constants.Z_SavedAssignmentList, string.Join(",", m_assignment.Select(p => p.GetUniqueId())));
                }
                else
                {
                    Common.Dbgl($"Push new assignment");
                    var allPieces = typeof(Piece).GetField("m_allPieces", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as IEnumerable<Piece>;
                    var addedPiece = allPieces.Where(p => p.GetUniqueId() == assignment).FirstOrDefault();
                    if (null != addedPiece && !m_assignment.Contains(addedPiece))
                    {
                        m_assignment.Push(addedPiece);
                    }
                }
            });
            NView.Register<string, string>(Constants.Z_updateTrainedAssignments, (long source, string uniqueID, string trainedAssignments) =>
            {
                if (NView.IsOwner()) return;
                if (UniqueID == uniqueID)
                {
                    m_trainedAssignments.Clear();
                    m_trainedAssignments.AddRange(trainedAssignments.Split());
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
                .SubstateOf(State.Root);
        }

        private void ConfigureIdle()
        {
            Brain.Configure(State.Idle)
                .SubstateOf(State.Root)
                .PermitIf(Trigger.Hungry, eatingBehaviour.StartState, () => eatingBehaviour.IsHungry(IsHurt))
                .PermitIf(UpdateTrigger, State.Assigned, (arg) =>
                {
                    if (Brain.IsInState(State.Hungry)) return false;
                    if ((m_stuckInIdleTimer += arg.dt) > 300f)
                    {
                        Common.Dbgl("m_startPosition = HomePosition");
                        m_startPosition = HomePosition;
                        m_stuckInIdleTimer = 0f;
                    }
                    if ((m_searchForNewAssignmentTimer += arg.dt) < 2f) return false;
                    m_searchForNewAssignmentTimer = 0f;
                    return AddNewAssignment(arg.instance.transform.position);

                })
                .OnEntry(t =>
                {
                    m_stuckInIdleTimer = 0;
                    UpdateAiStatus(State.Idle);
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
                    fightBehaviour.m_mobilityLevel = Mobility;
                    fightBehaviour.m_agressionLevel = Agressiveness;
                    fightBehaviour.m_awarenessLevel = Awareness;

                    Brain.Fire(Trigger.Fight);
                })
                .OnExit(t =>
                {
                    ItemDrop.ItemData currentWeapon = (Character as Humanoid).GetCurrentWeapon();
                    if (null != currentWeapon)
                    {
                        (Character as Humanoid).UnequipItem(currentWeapon);
                    }
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                });
        }
        private void ConfigureFlee()
        {
            Brain.Configure(State.Flee)
                .SubstateOf(State.Root)
                .PermitIf(UpdateTrigger, State.Idle, (args) => (m_fleeTimer += args.dt) > FleeTimeout && !Common.Alarmed(args.instance, Mathf.Max(1, Awareness - 1)))
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

        private void ConfigureFollow()
        {
            Brain.Configure(State.Follow)
                .PermitIf(UpdateTrigger, State.Idle, (args) => !(bool)args.instance.GetFollowTarget())
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.Follow);
                    Attacker = null;
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                })
                .OnExit(t =>
                {
                    HomePosition = m_startPosition = eatingBehaviour.LastKnownFoodPosition = Instance.transform.position;

                });
        }

        private void ConfigureSearchForItems()
        {
            Brain.Configure(State.SearchForItems.ToString())
                .SubstateOf(State.Root)
                .Permit(Trigger.SearchForItems, searchForItemsBehaviour.StartState)
                .OnEntry(t =>
                {
                    Common.Dbgl("ConfigureSearchContainers Initiated");
                    searchForItemsBehaviour.KnownContainers = m_containers;
                    searchForItemsBehaviour.Items = t.Parameters[0] as IEnumerable<ItemDrop.ItemData>;
                    searchForItemsBehaviour.AcceptedContainerNames = m_config.IncludedContainers;
                    searchForItemsBehaviour.SuccessState = t.Parameters[1] as string;
                    searchForItemsBehaviour.FailState = t.Parameters[2] as string;
                    Brain.Fire(Trigger.SearchForItems.ToString());
                });
        }

        private void ConfigureAssigned()
        {
            Brain.Configure(State.Assigned)
                .SubstateOf(State.Idle)
                .InitialTransition(State.MoveToAssignment)
                .Permit(Trigger.AssignmentTimedOut, State.Idle)
                .OnEntry(t =>
                {
                    UpdateAiStatus(State.Assigned);
                    m_assignedTimer = 0;
                });

            Brain.Configure(State.MoveToAssignment)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.Failed, State.Idle)
                .PermitIf(UpdateTrigger, State.CheckRepairState, (arg) => MoveToAssignment(arg.dt))
                .OnEntry(t =>
                {
                    if (Common.GetNView(m_assignment.Peek())?.IsValid() != true)
                    {
                        Brain.Fire(Trigger.Failed);
                        m_assignment.Pop();
                        return;
                    }
                    UpdateAiStatus(State.MoveToAssignment, m_assignment.Peek().m_name);
                    m_closeEnoughTimer = 0;
                })
                .OnExit(t =>
                {
                    StopMoving();
                });

            Brain.Configure(State.TurnToFaceAssignment)
                .SubstateOf(State.Assigned)
                .PermitIf(UpdateTrigger, State.CheckRepairState, (arg) => Common.TurnToFacePosition(this, m_assignment.Peek().transform.position));

            Brain.Configure(State.CheckRepairState)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.Failed, State.Idle)
                .Permit(Trigger.RepairDone, State.Idle)
                .Permit(Trigger.RepairNeeded, State.RepairAssignment)
                .OnEntry(t =>
                {
                    if (Common.GetNView(m_assignment.Peek())?.IsValid() != true)
                    {
                        Brain.Fire(Trigger.Failed);
                        m_assignment.Pop();
                        return;
                    }
                    NView.InvokeRPC(ZNetView.Everybody, Constants.Z_AddAssignment, m_assignment.Peek().GetUniqueId());
                    var wnt = m_assignment.Peek().GetComponent<WearNTear>();
                    float health = wnt?.GetHealthPercentage() ?? 1.0f;
                    if (health < 0.9f)
                    {
                        m_startPosition = Instance.transform.position;
                        Brain.Fire(Trigger.RepairNeeded);
                    }
                    else
                    {
                        UpdateAiStatus(State.CheckRepairState, m_assignment.Peek().m_name);
                        Brain.Fire(Trigger.RepairDone);
                    }
                });
            bool hammerAnimationStarted = false;
            Brain.Configure(State.RepairAssignment)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.Failed, State.Idle)
                .PermitIf(UpdateTrigger, State.Idle, (args) =>
                {
                    m_repairTimer += args.dt;
                    if (m_repairTimer < RepairTimeout - 0.5f) return false;
                    if (!hammerAnimationStarted)
                    {
                        var zAnim = typeof(Character).GetField("m_zanim", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Character) as ZSyncAnimation;
                        ItemDrop.ItemData currentWeapon = (Character as Humanoid).GetCurrentWeapon();
                        if (null == currentWeapon)
                        {
                            currentWeapon = (Character as Humanoid).GetInventory().GetAllItems().FirstOrDefault();
                            (Character as Humanoid).EquipItem(currentWeapon);
                        }
                        zAnim.SetTrigger(currentWeapon.m_shared.m_attack.m_attackAnimation);
                        hammerAnimationStarted = true;
                    }
                    return m_repairTimer >= RepairTimeout;
                })
                .OnEntry(t =>
                {
                    if (Common.GetNView(m_assignment.Peek())?.IsValid() != true)
                    {
                        Brain.Fire(Trigger.Failed);
                        m_assignment.Pop();
                        return;
                    }
                    UpdateAiStatus(State.RepairAssignment, m_assignment.Peek().m_name);
                    m_repairTimer = 0.0f;
                    hammerAnimationStarted = false;
                })
                .OnExit(t =>
                {
                    if (t.Trigger == Trigger.Failed || Common.GetNView<Piece>(m_assignment.Peek())?.IsValid() != true) return;
                    m_stuckInIdleTimer = 0;
                    var pieceToRepair = m_assignment.Peek();
                    WearNTear component = pieceToRepair.GetComponent<WearNTear>();
                    if ((bool)component && component.Repair())
                    {
                        pieceToRepair.m_placeEffect.Create(pieceToRepair.transform.position, pieceToRepair.transform.rotation);
                    }
                });
        }

        public bool MoveToAssignment(float dt)
        {
            bool assignmentIsInvalid = m_assignment.Peek() == null || m_assignment.Peek().GetComponent<ZNetView>()?.IsValid() != true;
            if (assignmentIsInvalid)
            {
                if (m_assignment.Any())
                {
                    m_assignment.Pop();
                }
                return true;
            }
            if ((m_roarTimer += dt) > RoarTimeout)
            {
                var nearbyMobs = MobManager.AliveMobs.Values.Where(c => c.HasInstance()).Where(c => Vector3.Distance(c.Instance.transform.position, Instance.transform.position) < 1.0f).Where(m => m.UniqueID != this.UniqueID);
                if (nearbyMobs.Any())
                {
                    Instance.m_alertedEffects.Create(Instance.transform.position, Quaternion.identity);
                    foreach (var mob in nearbyMobs)
                    {
                        mob.GotShoutedAtBy(this);
                    }
                    m_roarTimer = 0.0f;
                }
            }
            float distance = (m_closeEnoughTimer += dt) > CloseEnoughTimeout ? RepairMinDist : RepairMinDist + 2.0f;
            return MoveAndAvoidFire(m_assignment.Peek().FindClosestPoint(Instance.transform.position), dt, distance);
        }

        private bool AddNewAssignment(Vector3 position)
        {
            Common.Dbgl($"Enter {nameof(AddNewAssignment)}");
            var pieceList = new List<Piece>();
            var start = DateTime.Now;
            Piece.GetAllPiecesInRadius(position, m_config.Awareness*5 , pieceList);
            var piece = pieceList
                .Where(p => p.m_category == Piece.PieceCategory.Building || p.m_category == Piece.PieceCategory.Crafting)
                .Where(p => !m_assignment.Contains(p))
                .Where(p => Common.GetNView(p)?.IsValid() ?? false)
                .Where(p => Common.CanSeeTarget(Instance, p.gameObject))
                .OrderBy(p => Vector3.Distance(p.GetCenter(), position))
                .FirstOrDefault();
            Common.Dbgl($"Selecting piece took {(DateTime.Now - start).TotalMilliseconds}ms");
            if (piece != null && !string.IsNullOrEmpty(Common.GetOrCreateUniqueId(Common.GetNView(piece))))
            {
                m_lastSuccessfulFindAssignment = Time.time;
                if (Time.time - m_lastFailedFindAssignment > AdjustAssignmentStackSizeTime)
                {
                    m_lastFailedFindAssignment = Time.time;
                    int newMaxSize = Math.Min(100, (int)(m_assignment.MaxSize * 1.2f));
                    int oldCount = m_assignment.Count();
                    Common.Dbgl($"Increased Assigned stack from {m_assignment.MaxSize} to {newMaxSize} and copied {oldCount} pieces");

                    m_assignment.MaxSize = newMaxSize;
                }
                m_assignment.Push(piece);
                return true;
            }
            else
            {
                m_lastFailedFindAssignment = Time.time;
                if (Time.time - m_lastSuccessfulFindAssignment > AdjustAssignmentStackSizeTime)
                {
                    m_lastSuccessfulFindAssignment = Time.time;
                    int newMaxSize = Math.Max(1, (int)(m_assignment.Count() * 0.8f));
                    int oldCount = m_assignment.Count();
                    Common.Dbgl($"Decreased Assigned stack from {m_assignment.MaxSize} to {newMaxSize} pushing {oldCount} pieces");
                    m_assignment.MaxSize = newMaxSize;
                }
            }

            return false;
        }

        private string m_lastState = "";
        public override void UpdateAI(float dt)
        {
            if (Brain.State != m_lastState)
            {
                Common.Dbgl($"State:{Brain.State}");
                m_lastState = Brain.State;
            }

            base.UpdateAI(dt);
            m_triggerTimer += dt;
            if (m_triggerTimer < 0.1f) return;

            m_triggerTimer = 0f;
            var monsterAi = Instance as MonsterAI;

            eatingBehaviour.Update(this, dt);

            //Runtime triggers
            Brain.Fire(Trigger.Follow);
            Brain.Fire(Trigger.TakeDamage);
            Brain.Fire(Trigger.Hungry);
            Brain.Fire(UpdateTrigger, (monsterAi, dt));

            //Assigned timeout-function 
            m_assignedTimer += dt;
            if (m_assignedTimer > m_config.TimeLimitOnAssignment)
            {
                Brain.Fire(Trigger.AssignmentTimedOut);
            }

            if (Brain.IsInState(State.Follow))
            {
                Invoke<MonsterAI>(Instance, "Follow", monsterAi.GetFollowTarget(), dt);
                return;
            }

            if (Brain.IsInState(State.Flee))
            {
                var fleeFrom = Attacker == null ? Character.transform.position : Attacker.transform.position;
                Invoke<MonsterAI>(Instance, "Flee", dt, fleeFrom);
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

        public override void Follow(Player player)
        {
            NView.InvokeRPC(ZNetView.Everybody, Constants.Z_MobCommand, player.GetZDOID(), "Follow");
        }

        public MobAIInfo GetMobAIInfo()
        {
            return new MobAIInfo
            {
                Name = "Fixer",
                AIType = this.GetType(),
                ConfigType = typeof(FixerAIConfig)
            };
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
            Instance.m_alertedEffects.Create(Instance.transform.position, Quaternion.identity);
        }
    }
}
