using HarmonyLib;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class DynamicWorkerBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_DYNWORK";

        private StateDef State { get; set; }

        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string Idle { get { return $"{prefix}Idle"; } }
            public string Assigned { get { return $"{prefix}Assigned"; } }
            public string HaveAssignmentItem { get { return $"{prefix}HaveAssignmentItem"; } }
            public string HaveNoAssignmentItem { get { return $"{prefix}HaveNoAssignmentItem"; } }
            public string MoveToAssignment { get { return $"{prefix}MoveToAssignment"; } }
            public string CheckingAssignment { get { return $"{prefix}CheckingAssignment"; } }
            public string DoneWithAssignment { get { return $"{prefix}DoneWithAssignment"; } }
            public string UnloadToAssignment { get { return $"{prefix}UnloadToAssignment"; } }

            public StateDef(string prefix)
            {
                this.prefix = prefix;
            }
        }
        private TriggerDef Trigger { get; set; }
        private sealed class TriggerDef
        {
            private readonly string prefix;

            public string Abort { get { return $"{prefix}Abort"; } }
            public string StartAssignment { get { return $"{prefix}StartAssignment"; } }
            public string ItemFound { get { return $"{prefix}ItemFound"; } }
            public string Update { get { return $"{prefix}Update"; } }
            public string ItemNotFound { get { return $"{prefix}ItemNotFound"; } }
            public string SearchForItems { get { return $"{prefix}SearchForItems"; } }
            public string IsCloseToAssignment { get { return $"{prefix}IsCloseToAssignment"; } }
            public string AssignmentTimedOut { get { return $"{prefix}AssignmentTimedOut"; } }
            public string AssignmentFinished { get { return $"{prefix}AssignmentFinished"; } }
            public string LeaveAssignment { get { return $"{prefix}LeaveAssignment"; } }
            public string ReachedMoveTarget { get { return $"{prefix}ReachedMoveTarget"; } }

            public TriggerDef(string prefix)
            {
                this.prefix = prefix;

            }
        }

        // Settings
        public float CloseEnoughTimeout { get; private set; } = 30;
        public string[] AcceptedContainerNames { get; set; } = new string[] { };
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float TimeLimitOnAssignment { get; set; } = 120;
        public bool RequireLineOfSightToDiscoverAssignment { get; set; } = false;

        // Member vars
        private LinkedList<Assignment> m_assignment;
        private ItemDrop.ItemData m_carrying;
        private SearchForItemsBehaviour searchForItemsBehaviour;
        private Vector3 m_startPosition;
        private MobAIBase m_aiBase;
        // Timers
        private float m_closeEnoughTimer;
        private float m_searchForNewAssignmentTimer;
        private float m_assignedTimer;
        private float m_stuckInIdleTimer;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);

            m_aiBase = aiBase;
            m_assignment = new LinkedList<Assignment>();
            m_carrying = null;
            m_assignedTimer = 0f;
            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Postfix = Prefix;
            searchForItemsBehaviour.IncludePickables = false;
            searchForItemsBehaviour.SuccessState = State.HaveAssignmentItem;
            searchForItemsBehaviour.FailState = State.HaveNoAssignmentItem;
            searchForItemsBehaviour.CenterPoint = aiBase.NView.transform.position;
            searchForItemsBehaviour.KnownContainers = aiBase.KnownContainers;
            searchForItemsBehaviour.Configure(aiBase, aiBase.Brain, State.Main);

            aiBase.Brain.Configure(State.Main)
                .SubstateOf(parentState)
                .InitialTransition(State.Idle)
                .Permit(Trigger.Abort, FailState)
                .Permit(Trigger.AssignmentTimedOut, State.DoneWithAssignment)
                .OnEntry(() =>
                {
                    Common.Dbgl("Entering DynamicWorkerBehaviour", true);
                });

            aiBase.Brain.Configure(State.Idle)
                .SubstateOf(State.Main)
                .Permit(Trigger.StartAssignment, State.Assigned)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.Idle);
                });

            aiBase.Brain.Configure(State.Assigned)
                .SubstateOf(State.Main)
                .InitialTransition(State.MoveToAssignment)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.Assigned);
                    m_assignedTimer = 0;
                    m_startPosition = aiBase.Instance.transform.position;
                })
                .OnExit(t =>
                {
                    if (m_carrying != null)
                    {
                        (aiBase.Character as Humanoid).DropItem((aiBase.Character as Humanoid).GetInventory(), m_carrying, 1);
                        m_carrying = null;
                    }
                });

            string nextState = State.Idle;
            aiBase.Brain.Configure(State.MoveToAssignment)
                .SubstateOf(State.Assigned)
                .PermitDynamic(Trigger.ReachedMoveTarget, () => nextState)
                .Permit(Trigger.LeaveAssignment, State.Idle)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.MoveToAssignment);
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


            aiBase.Brain.Configure(State.HaveAssignmentItem)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.ItemFound, State.MoveToAssignment)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.HaveAssignmentItem, searchForItemsBehaviour.FoundItem.m_shared.m_name);
                    var pickedUpInstance = (aiBase.Character as Humanoid).PickupPrefab(searchForItemsBehaviour.FoundItem.m_dropPrefab);
                    (aiBase.Character as Humanoid).EquipItem(pickedUpInstance);
                    m_carrying = pickedUpInstance;
                    aiBase.Brain.Fire(Trigger.ItemFound);
                });

            aiBase.Brain.Configure(State.HaveNoAssignmentItem)
                .SubstateOf(State.Assigned)
                .PermitIf(Trigger.ItemNotFound, State.DoneWithAssignment)
                .OnEntry(t =>
                {
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                });

            aiBase.Brain.Configure(State.CheckingAssignment)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.SearchForItems, searchForItemsBehaviour.StartState)
                .Permit(Trigger.AssignmentFinished, State.DoneWithAssignment)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.CheckingAssignment, m_assignment.First().TypeOfAssignment.Name);
                    aiBase.StopMoving();
                    var needFuel = m_assignment.First().NeedFuel;
                    var needOre = m_assignment.First().NeedOre;
                    var fetchItems = new List<ItemDrop.ItemData>();
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:Ore:{needOre.Join(j => j.m_shared.m_name)}, Fuel:{needFuel?.m_shared.m_name}", true, "Worker");
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
                        aiBase.Brain.Fire(Trigger.AssignmentFinished);
                    }
                    else
                    {
                        searchForItemsBehaviour.CenterPoint = aiBase.Instance.transform.position;
                        searchForItemsBehaviour.Items = fetchItems;
                        aiBase.Brain.Fire(Trigger.SearchForItems);
                    }
                });
            aiBase.Brain.Configure(State.UnloadToAssignment)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.AssignmentFinished, State.CheckingAssignment)
                .OnEntry(t =>
                {
                    aiBase.StopMoving();
                    var needFuel = m_assignment.First().NeedFuel;
                    var needOre = m_assignment.First().NeedOre;
                    bool isCarryingFuel = m_carrying.m_shared.m_name == needFuel?.m_shared?.m_name;
                    bool isCarryingMatchingOre = needOre?.Any(c => m_carrying.m_shared.m_name == c?.m_shared?.m_name) ?? false;

                    aiBase.UpdateAiStatus(State.UnloadToAssignment, m_assignment.First().TypeOfAssignment.Name);
                    if (isCarryingFuel)
                    {
                        Debug.Log("AddFuelRPC");
                        m_assignment.First().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddFuel", new object[] { });
                        (aiBase.Character as Humanoid).GetInventory().RemoveOneItem(m_carrying);
                    }
                    else if (isCarryingMatchingOre)
                    {
                        Debug.Log("AddOreRPC");
                        m_assignment.First().AssignmentObject.GetComponent<ZNetView>().InvokeRPC("AddOre", new object[] { Utils.GetPrefabName(m_carrying.m_dropPrefab.name) });
                        (aiBase.Character as Humanoid).GetInventory().RemoveOneItem(m_carrying);
                    }
                    else
                    {
                        (aiBase.Character as Humanoid).DropItem((aiBase.Character as Humanoid).GetInventory(), m_carrying, 1);
                    }
                    (aiBase.Character as Humanoid).UnequipItem(m_carrying, false);
                    m_carrying = null;

                    aiBase.Brain.Fire(Trigger.AssignmentFinished);
                });

            aiBase.Brain.Configure(State.DoneWithAssignment)
                .SubstateOf(State.Assigned)
                .Permit(Trigger.LeaveAssignment, State.Idle)
                .OnEntry(t =>
                {
                    if (m_carrying != null)
                    {
                        Common.Dbgl($"{aiBase.Character.GetHoverName()}:Dropping {m_carrying.m_shared.m_name} on the ground", true, "Worker");
                        (aiBase.Character as Humanoid).DropItem((aiBase.Character as Humanoid).GetInventory(), m_carrying, 1);
                        m_carrying = null;
                    }
                    aiBase.UpdateAiStatus(State.DoneWithAssignment);
                    //m_containers.Peek()?.SetInUse(inUse: false);
                    if (m_assignment.Any())
                    {
                        m_assignment.First().AssignmentTimeout = Time.time + m_assignment.First().TypeOfAssignment.TimeBeforeAssignmentCanBeRepeated;
                    }
                    aiBase.Brain.Fire(Trigger.LeaveAssignment);
                });

        }

        private string m_lastState = "";

        public void Update(MobAIBase instance, float dt)
        {
            if (instance.Brain.State != m_lastState)
            {
                Common.Dbgl($"{instance.Character.GetHoverName()}:State:{instance.Brain.State}", true, "Worker");
                m_lastState = instance.Brain.State;
            }

            //Assigned timeout-function 
            m_assignedTimer += dt;
            if (m_assignedTimer > TimeLimitOnAssignment)
            {
                instance.Brain.Fire(Trigger.AssignmentTimedOut);
                return;
            }

            if (instance.Brain.IsInState(State.Idle))
            {
                if ((m_stuckInIdleTimer += dt) > 300f)
                {
                    Common.Dbgl($"{instance.Character.GetHoverName()}:m_startPosition = HomePosition", true);
                    m_stuckInIdleTimer = 0f;
                }
                if (instance.Brain.IsInState(State.Assigned)) return;
                if ((m_searchForNewAssignmentTimer += dt) < 2) return;
                m_searchForNewAssignmentTimer = 0f;
                Common.Dbgl("Searching for new assignment", true, "Worker");
                if (StartNewAssignment(instance, ref m_assignment))
                {
                    instance.Brain.Fire(Trigger.StartAssignment);
                }
                return;
            }

            if (instance.Brain.IsInState(State.MoveToAssignment))
            {
                if (MoveToAssignment(instance, dt))
                {
                    instance.Brain.Fire(Trigger.ReachedMoveTarget);
                }
                return;
            }

            if (instance.Brain.IsInState(searchForItemsBehaviour.StartState))
            {
                searchForItemsBehaviour.Update(instance, dt);
                return;
            }
        }

        public bool StartNewAssignment(MobAIBase aiBase, ref LinkedList<Assignment> KnownAssignments)
        {
            Debug.Log($"KnownAssignments:{string.Join(",", KnownAssignments.Select(a => a.TypeOfAssignment.Name))}");
            Assignment newassignment = Common.FindRandomNearbyAssignment(aiBase.Instance, null, KnownAssignments, aiBase.Awareness * 5, null, RequireLineOfSightToDiscoverAssignment);
            Debug.Log($"Num assignments after:{KnownAssignments.Count()}");
            if (newassignment != null)
            {
                Common.Dbgl($"{aiBase.Character.GetHoverName()}:Found new assignment:{newassignment.TypeOfAssignment.Name}", true, "Worker");
                KnownAssignments.AddFirst(newassignment);
                Common.Dbgl($"Num assignments:{KnownAssignments.Count}, First assignment:{KnownAssignments.First()?.TypeOfAssignment.Name}", true, "Worker");
                return true;
            }
            else if (KnownAssignments.Any())
            {
                var assignment = KnownAssignments.OrderBy(a => a.AssignmentTimeout).Where(a => a.AssignmentObject != null).FirstOrDefault();
                if (assignment == null)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:No valid assignments found, going back to Idle", true, "Worker");
                    return false;
                }
                KnownAssignments.Remove(assignment);
                KnownAssignments.AddFirst(assignment);
                Common.Dbgl($"{aiBase.Character.GetHoverName()}:No new assignment found, checking old one:{KnownAssignments.First().TypeOfAssignment.Name}", true, "Worker");
                return true;
            }
            return false;
        }

        public bool MoveToAssignment(MobAIBase instance, float dt)
        {
            if (!m_assignment.Any())
            {
                Common.Dbgl($"{instance.Character.GetHoverName()}:No assignments to move to", true, "Worker");
                instance.Brain.Fire(Trigger.LeaveAssignment);
                return true;
            }
            if (!(bool)m_assignment.First().AssignmentObject)
            {
                Common.Dbgl("AssignmentObject is null", true, "Worker");
                m_assignment.RemoveFirst();
                instance.Brain.Fire(Trigger.LeaveAssignment);
                return false;
            }
            bool assignmentIsInvalid = m_assignment.First().AssignmentObject?.GetComponent<ZNetView>()?.IsValid() == false;
            if (assignmentIsInvalid)
            {
                Common.Dbgl("AssignmentObject is invalid", true, "Worker");
                m_assignment.RemoveFirst();
                instance.Brain.Fire(Trigger.LeaveAssignment);
                return false;
            }
            float distance = (m_closeEnoughTimer += dt) > CloseEnoughTimeout ? m_assignment.First().TypeOfAssignment.InteractDist : m_assignment.First().TypeOfAssignment.InteractDist + 1;
            return instance.MoveAndAvoidFire(m_assignment.First().Position, dt, distance, false, true);
        }

        public void Abort()
        {
            if (m_carrying != null)
            {
                (m_aiBase.Character as Humanoid).DropItem((m_aiBase.Character as Humanoid).GetInventory(), m_carrying, 1);
                m_carrying = null;
            }
        }
    }
}
