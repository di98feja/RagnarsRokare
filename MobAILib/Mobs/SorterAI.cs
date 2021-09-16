using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace RagnarsRokare.MobAI
{
    public class SorterAI : MobAIBase, IMobAIType
    {
        public MaxStack<Container> m_containers;

        // Timers
        private float m_searchForNewAssignmentTimer;
        private float m_triggerTimer;
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
            public const string Sorting = "Sorting";
            public const string SearchForItems = "SearchForItems";
            public const string Root = "Root";
            public const string Hungry = "Hungry";
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
            public const string Failed = "Failed";
            public const string Fight = "Fight";
            public const string EnterEatBehaviour = "EnterEatBehaviour";
        }

        readonly StateMachine<string, string>.TriggerWithParameters<float> UpdateTrigger;
        readonly StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;
        readonly SearchForItemsBehaviour searchForItemsBehaviour;
        readonly ItemSortingBehaviour itemSortingBehaviour;
        readonly IFightBehaviour fightBehaviour;
        readonly EatingBehaviour eatingBehaviour;

        SorterAIConfig m_config;

        public SorterAI() : base()
        { }

        public SorterAI(MonsterAI instance, object config) : this(instance, config as MobAIBaseConfig)
        { }

        public SorterAI(MonsterAI instance, MobAIBaseConfig config) : base(instance, State.Idle, config)
        {
            m_config = config as SorterAIConfig;
            m_containers = new MaxStack<Container>(Intelligence);

            //if (instance.m_consumeHeal == 0.0f)
            //{
            //    instance.m_consumeHeal = Character.GetMaxHealth() * 0.25f;
            //}

            if (m_startPosition == Vector3.zero)
            {
                m_startPosition = instance.transform.position;
            }

            RegisterRPCMethods();

            UpdateTrigger = Brain.SetTriggerParameters<float>(Trigger.Update);
            LookForItemTrigger = Brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>(Trigger.ItemFound);

            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Configure(this, Brain, State.SearchForItems);
            itemSortingBehaviour = new ItemSortingBehaviour();
            itemSortingBehaviour.Configure(this, Brain, State.Sorting);
            itemSortingBehaviour.MaxSearchTime = m_config.MaxSearchTime;
            itemSortingBehaviour.AcceptedContainerNames = m_config.IncludedContainers;
            itemSortingBehaviour.SuccessState = State.Idle;
            itemSortingBehaviour.FailState = State.Idle;
            fightBehaviour = Activator.CreateInstance(FightingBehaviourSelector.Invoke(this)) as IFightBehaviour;
            fightBehaviour.Configure(this, Brain, State.Fight);
            eatingBehaviour = new EatingBehaviour();
            eatingBehaviour.Configure(this, Brain, State.Hungry);
            eatingBehaviour.HungryTimeout = m_config.PostTameFeedDuration;
            eatingBehaviour.SearchForItemsState = State.SearchForItems;
            eatingBehaviour.SuccessState = State.Idle;
            eatingBehaviour.FailState = State.Idle;
            eatingBehaviour.HealPercentageOnConsume = 0.2f;

            string serializedDumpChest = NView.GetZDO().GetString(Constants.Z_SavedDumpChest);
            itemSortingBehaviour.DumpContainer = StorageContainer.DeSerialize(serializedDumpChest);
            Common.Dbgl($"{Character.GetHoverName()}:Loaded dumpchest {serializedDumpChest}", true, "Sorter");

            ConfigureRoot();
            ConfigureIdle();
            ConfigureFollow();
            ConfigureSearchForItems();
            ConfigureSorting();
            ConfigureFlee();
            ConfigureFight();
            ConfigureHungry();
            var graph = new Stateless.Graph.StateGraph(Brain.GetInfo());
            //Debug.Log(graph.ToGraph(new Stateless.Graph.UmlDotGraphStyle()));

            PrintAIStateToDebug = true;
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
                .PermitIf(Trigger.TakeDamage, State.Fight, () => !Brain.IsInState(State.Flee) && !Brain.IsInState(State.Fight) && (TimeSinceHurt < 20.0f || Common.Alarmed(Instance, 1)))
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
                .PermitIf(UpdateTrigger, State.Sorting, (dt) =>
                {
                    if (Brain.IsInState(State.Sorting)) return false;
                    if ((m_stuckInIdleTimer += dt) > 300f)
                    {
                        Common.Dbgl($"{Character.GetHoverName()}:m_startPosition = HomePosition", true, "Sorter");
                        m_startPosition = HomePosition;
                        m_stuckInIdleTimer = 0f;
                    }
                    if ((m_searchForNewAssignmentTimer += dt) < 10f) return false;
                    m_searchForNewAssignmentTimer = 0f;
                    Common.Dbgl($"{Character.GetHoverName()}:Execute State.Sorting ", true, "Sorter");
                    return true;
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
                    fightBehaviour.MobilityLevel = Mobility;
                    fightBehaviour.AgressionLevel = Agressiveness;
                    fightBehaviour.AwarenessLevel = Awareness;

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
                .PermitIf(UpdateTrigger, State.Idle, (dt) => (m_fleeTimer += dt ) > FleeTimeout && !Common.Alarmed(Instance, 1))
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
                .PermitIf(UpdateTrigger, State.Idle, (dt) => !(bool)(Instance as MonsterAI).GetFollowTarget())
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
                    Common.Dbgl($"{Character.GetHoverName()}:ConfigureSearchContainers Initiated", true, "Sorter");
                    searchForItemsBehaviour.KnownContainers = m_containers;
                    searchForItemsBehaviour.Items = t.Parameters[0] as IEnumerable<ItemDrop.ItemData>;
                    searchForItemsBehaviour.AcceptedContainerNames = m_config.IncludedContainers;
                    searchForItemsBehaviour.SuccessState = t.Parameters[1] as string;
                    searchForItemsBehaviour.FailState = t.Parameters[2] as string;
                    Brain.Fire(Trigger.SearchForItems.ToString());
                });
        }

        private void ConfigureSorting()
        {
            Brain.Configure(State.Sorting)
                .SubstateOf(State.Idle)
                .InitialTransition(itemSortingBehaviour.StartState)
                .OnEntry(t =>
                {
                    Common.Dbgl($"{Character.GetHoverName()}:ItemSortBehaviour Initiated", true, "Sorter");
                });
        }


        private string m_lastState = "";

        public override void UpdateAI(float dt)
        {
            if (Brain.State != m_lastState)
            {
                Common.Dbgl($"{Character.GetHoverName()}:State:{Brain.State}", true, "Sorter");
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
            Brain.Fire(UpdateTrigger, dt);

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

            if (Brain.IsInState(State.Sorting))
            {
                itemSortingBehaviour.Update(this, dt);
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
                Name = "Sorter",
                AIType = this.GetType(),
                ConfigType = typeof(SorterAIConfig)
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
            else if (!(player == null) && command == "AssignDumpContainer")
            {
                var hoverObject = player.GetHoverObject();
                var container = hoverObject?.GetComponent<Container>() ?? hoverObject?.GetComponentInChildren<Container>() ?? hoverObject?.GetComponentInParent<Container>();
                if (container == null)
                {
                    itemSortingBehaviour.DumpContainer = null;
                    NView.GetZDO().Set(Constants.Z_SavedDumpChest, string.Empty);
                }
                else
                {
                    itemSortingBehaviour.DumpContainer = new StorageContainer(container, 0f);
                    NView.GetZDO().Set(Constants.Z_SavedDumpChest, itemSortingBehaviour.DumpContainer.Serialize());
                    Common.Dbgl($"{Character.GetHoverName()}:Set DumpContainer: {itemSortingBehaviour.DumpContainer.Serialize()}", true, "Sorter");
                }
            }
        }

        public override void GotShoutedAtBy(MobAIBase mob)
        {
            Instance.m_alertedEffects.Create(Instance.transform.position, Quaternion.identity);
        }
    }
}
