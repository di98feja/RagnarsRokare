using RagnarsRokare.MobAI;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SlaveGreylings
{
    public class BruteAI : MobAIBase, IControllableMob
    {
        public MaxStack<Piece> m_assignment = new MaxStack<Piece>(100);
        public MaxStack<Container> m_containers;
        public string[] m_acceptedContainerNames;

        // Timers
        private float m_searchForNewAssignmentTimer;
        private float m_foodsearchtimer;
        private float m_triggerTimer;
        private float m_assignedTimer;
        private float m_closeEnoughTimer;
        private float m_repairTimer;

        public float CloseEnoughTimeout { get; private set; } = 30;
        public float RepairTimeout { get; private set; } = 5;

        private class State
        {
            public const string Idle = "Idle";
            public const string Follow = "Follow";
            public const string Fight = "Fight";
            public const string Hungry = "Hungry";
            public const string Assigned = "Assigned";
            public const string SearchForFood = "SearchForFood";
            public const string SearchForItems = "SearchForItems";
            public const string HaveFoodItem = "HaveFoodItem";
            public const string HaveNoFoodItem = "HaveNoFoodItem";
            public const string MoveToAssignment = "MoveToAssignment";
            public const string CheckRepairState = "CheckRepairState";
            public const string RepairAssignment = "RepairAssignment";
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
        }

        readonly StateMachine<string, string>.TriggerWithParameters<(MonsterAI instance, float dt)> UpdateTrigger;
        readonly StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;
        readonly SearchForItemsBehaviour searchForItemsBehaviour;
        readonly FightBehaviour fightBehaviour;

        public BruteAI() : base()
        { }

        public BruteAI(MonsterAI instance) : base(instance, State.Idle)
        {
            m_containers = new MaxStack<Container>(GreylingsConfig.MaxContainersInMemory.Value);
            m_acceptedContainerNames = BruteConfig.IncludedContainersList.Value.Split();
            UpdateTrigger = Brain.SetTriggerParameters<(MonsterAI instance, float dt)>(Trigger.Update);
            LookForItemTrigger = Brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>(Trigger.ItemFound);

            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Configure(this, Brain, State.SearchForItems.ToString());
            fightBehaviour = new FightBehaviour();
            fightBehaviour.Configure(this, Brain, State.Fight.ToString());


            ConfigureIdle();
            ConfigureFollow();
            ConfigureIsHungry();
            ConfigureSearchForItems();
            ConfigureAssigned();
            ConfigureFight();
        }

        private void ConfigureIdle()
        {
            Brain.Configure(State.Idle.ToString())
                .PermitIf(Trigger.TakeDamage, State.Fight, () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow, State.Follow, () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(Trigger.Hungry, State.Hungry, () => (Instance as MonsterAI).Tameable().IsHungry())
                .PermitIf(UpdateTrigger, State.Assigned, (arg) =>
                {
                    if ((m_searchForNewAssignmentTimer += arg.dt) < 2) return false;
                    Debug.LogWarning("Search for piece to fix");
                    m_searchForNewAssignmentTimer = 0f;
                    return AddNewAssignment(arg.instance.transform.position, m_assignment);
                })
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Nothing to do, bored");
                });
        }

        private void ConfigureFollow()
        {
            Brain.Configure(State.Follow)
                .PermitIf(UpdateTrigger, State.Idle, (args) => !(bool)args.instance.GetFollowTarget())
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Follow");
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    m_assignment.Clear();
                });
        }

        private void ConfigureIsHungry()
        {
            Brain.Configure(State.Hungry)
                .PermitIf(Trigger.TakeDamage, State.Fight, () => Attacker != null)
                .PermitIf(Trigger.Follow, State.Follow, () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(UpdateTrigger, State.SearchForFood, (arg) => (m_foodsearchtimer += arg.dt) > 10)
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, "Is hungry, no work a do");
                    m_foodsearchtimer = 0f;
                });

            Brain.Configure(State.SearchForFood)
                .SubstateOf(State.Hungry)
                .Permit(LookForItemTrigger.Trigger, State.SearchForItems)
                .OnEntry(t =>
                {
                    Brain.Fire(LookForItemTrigger, (Instance as MonsterAI).m_consumeItems.Select(i => i.m_itemData), State.HaveFoodItem, State.HaveNoFoodItem);
                });

            Brain.Configure(State.HaveFoodItem)
                .SubstateOf(State.Hungry)
                .Permit(Trigger.ConsumeItem, State.Idle)
                .OnEntry(t =>
                {
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
                    Brain.Fire(Trigger.ConsumeItem);
                });

            Brain.Configure(State.HaveNoFoodItem)
                .SubstateOf(State.Hungry)
                .PermitIf(Trigger.ItemNotFound, State.Hungry)
                .OnEntry(t =>
                {
                    Brain.Fire(Trigger.ItemNotFound);
                });
        }

        private void ConfigureSearchForItems()
        {
            Brain.Configure(State.SearchForItems.ToString())
                .PermitIf(Trigger.TakeDamage.ToString(), State.Fight, () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .Permit(Trigger.SearchForItems, searchForItemsBehaviour.InitState)
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

        private void ConfigureFight()
        {
            Brain.Configure(State.Fight.ToString())
                .PermitIf(Trigger.Follow.ToString(), State.Follow.ToString(), () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .Permit(Trigger.TakeDamage, fightBehaviour.InitState)
                .OnEntry(t =>
                {
                    Debug.Log("FightBehaviour Initiated");
                    Brain.Fire(Trigger.TakeDamage.ToString());
                });
        }



        private void ConfigureAssigned()
        {
            Brain.Configure(State.Assigned)
                .InitialTransition(State.MoveToAssignment)
                .PermitIf(Trigger.TakeDamage, State.Fight, () => TimeSinceHurt < 20)
                .PermitIf(Trigger.Follow, State.Follow, () => (bool)(Instance as MonsterAI).GetFollowTarget())
                .PermitIf(Trigger.Hungry, State.Hungry, () => (Instance as MonsterAI).Tameable().IsHungry())
                .Permit(Trigger.AssignmentTimedOut, State.Idle)
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, $"uuhhhmm..  checkin' dis over 'ere");
                    m_assignedTimer = 0;
                });
        
            Brain.Configure(State.MoveToAssignment)
                .SubstateOf(State.Assigned)
                .PermitIf(UpdateTrigger, State.CheckRepairState, (arg) => MoveToAssignment(arg.dt))
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, $"Moving to assignment {m_assignment.Peek().m_name}");
                    m_closeEnoughTimer = 0;
                });

            Brain.Configure(State.CheckRepairState)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.RepairDone, State.Idle)
                .Permit(Trigger.RepairNeeded, State.RepairAssignment)
                .OnEntry(t =>
                {
                    var wnt = m_assignment.Peek().GetComponent<WearNTear>();
                    float health = wnt?.GetHealthPercentage() ?? 1.0f;
                    if (health < 0.9f)
                    {
                        UpdateAiStatus(NView, $"Hum, no goood");
                        Brain.Fire(Trigger.RepairNeeded);
                    }
                    else
                    {
                        UpdateAiStatus(NView, $"Naah dis {m_assignment.Peek().m_name} goood");
                        Brain.Fire(Trigger.RepairDone);
                    }
                });

            Brain.Configure(State.RepairAssignment)
                .SubstateOf(State.Assigned)
                .PermitIf(UpdateTrigger, State.Idle, (arg) => (m_repairTimer += arg.dt) > RepairTimeout)
                .OnEntry(t =>
                {
                    UpdateAiStatus(NView, $"Fixin Dis {m_assignment.Peek().m_name}");
                    m_repairTimer = 0.0f;
                })
                .OnExit(t =>
                {
                    var pieceToRepair = m_assignment.Peek();
                    UpdateAiStatus(NView, $"Dis {m_assignment.Peek().m_name} is goood as new!");
                    WearNTear component = pieceToRepair.GetComponent<WearNTear>();
                    if ((bool)component && component.Repair())
                    {
                        var zAnim = typeof(Character).GetField("m_zanim", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Character) as ZSyncAnimation;
                        ItemDrop.ItemData currentWeapon = (Character as Humanoid).GetCurrentWeapon();
                        var jaws = (Character as Humanoid).GetInventory().GetAllItems().FirstOrDefault();
                        Debug.LogWarning($"{jaws?.m_dropPrefab?.name} attack:{jaws?.m_shared?.m_attack?.m_attackAnimation}, currentWeapon:{currentWeapon?.m_shared?.m_attack?.m_attackAnimation}");
                        zAnim.SetTrigger(currentWeapon.m_shared.m_attack.m_attackAnimation);
                        pieceToRepair.m_placeEffect.Create(pieceToRepair.transform.position, pieceToRepair.transform.rotation);
                    }
                });
        }

        public bool MoveToAssignment(float dt)
        {
            bool assignmentIsInvalid = m_assignment.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
            if (assignmentIsInvalid)
            {
                m_assignment.Pop();
                return true;
            }
            float distance = (m_closeEnoughTimer += dt) > CloseEnoughTimeout ? 1.0f : 2.0f;
            return MoveAndAvoidFire(m_assignment.Peek().FindClosestPoint(Instance.transform.position), dt, distance);
        }

        private bool AddNewAssignment(Vector3 position, MaxStack<Piece> m_assignment)
        {
            SlaveGreylings.Dbgl($"Enter {nameof(AddNewAssignment)}");
            var pieceList = new List<Piece>();
            var start = DateTime.Now;
            Piece.GetAllPiecesInRadius(position, BruteConfig.AssignmentSearchRadius.Value, pieceList);
            var piece = pieceList
                .Where(p => p.m_category == Piece.PieceCategory.Building || p.m_category == Piece.PieceCategory.Crafting)
                .Where(p => !m_assignment.Contains(p))
                .OrderBy(p => Vector3.Distance(p.GetCenter(), position))
                .FirstOrDefault();
            SlaveGreylings.Dbgl($"Selecting piece took {(DateTime.Now - start).TotalMilliseconds}ms");
            if (piece != null)
            {
                m_assignment.Push(piece);
                return true;
            }
            return false;
        }

        public override void UpdateAI(float dt)
        {
            base.UpdateAI(dt);
            m_triggerTimer += dt;
            if (m_triggerTimer < 0.1f) return;

            m_triggerTimer = 0f;
            var monsterAi = Instance as MonsterAI;

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

            if (Brain.IsInState(State.Follow))
            {
                Invoke<MonsterAI>(Instance, "Follow", monsterAi.GetFollowTarget(), dt);
                return;
            }

            if (Brain.IsInState(searchForItemsBehaviour.InitState))
            {
                searchForItemsBehaviour.Update(this, dt);
                return;
            }

            if (Brain.IsInState(fightBehaviour.InitState))
            {
                fightBehaviour.Update(this, dt);
                return;
            }

        }

        public override void Follow(Player player)
        {
            NView.InvokeRPC(ZNetView.Everybody, Constants.Z_MobCommand, player.GetZDOID(), "Follow");
        }

        public MobInfo GetMobInfo()
        {
            return new MobInfo
            {
                PreTameFeedDuration = BruteConfig.FeedDuration.Value,
                PostTameFeedDuration = 1000,
                TamingTime = 1000,
                Name = "Greydwarf_Elite",
                PreTameConsumables = new List<ItemDrop> { ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Dandelion").Single() },
                PostTameConsumables = new List<ItemDrop> { ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Dandelion").Single() },
                AIType = this.GetType()
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
    }
}
