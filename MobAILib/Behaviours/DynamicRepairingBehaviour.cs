using Stateless;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class DynamicRepairingBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_DYNREPAIR";

        private StateDef State { get; set; }

        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string Idle { get { return $"{prefix}Idle"; } }
            public string FindAssignment { get { return $"{prefix}FindAssignment"; } }
            public string MoveToAssignment { get { return $"{prefix}MoveToAssignment"; } }
            public string TurnToFaceAssignment { get { return $"{prefix}TurnToFaceAssignment"; } }
            public string RepairAssignment { get { return $"{prefix}RepairAssignment"; } }
            public string DoneWithAssignment { get { return $"{prefix}DoneWithAssignment"; } }

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
            public string IsCloseToAssignment { get { return $"{prefix}IsCloseToAssignment"; } }
            public string AssignmentTimedOut { get { return $"{prefix}AssignmentTimedOut"; } }
            public string AssignmentFinished { get { return $"{prefix}AssignmentFinished"; } }
            public string AssignmentFailed{ get { return $"{prefix}AssignmentFailed"; } }
            
            public TriggerDef(string prefix)
            {
                this.prefix = prefix;

            }
        }

        // Settings
        public float CloseEnoughTimeout { get; private set; } = 30;
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float TimeLimitOnAssignment { get; set; } = 120;
        public bool RequireLineOfSightToDiscoverAssignment { get; set; } = false;
        public float RepairStepTime { get; set; } = 1.0f;
        public float RepairStepImprovement { get; set; } = 0.02f;

        // Member vars
        private Piece m_assignment;
        private Vector3 m_startPosition;
        private MobAIBase m_aiBase;
        // Timers
        private float m_closeEnoughTimer;
        private float m_assignedTimer;
        private float m_repairStepTimer;
        private ItemDrop.ItemData m_equippedHammer;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);

            m_aiBase = aiBase;
            m_assignedTimer = 0f;

            brain.Configure(State.Main)
                .SubstateOf(parentState)
                .InitialTransition(State.FindAssignment)
                .Permit(Trigger.Abort, FailState)
                .Permit(Trigger.AssignmentTimedOut, State.DoneWithAssignment)
                .OnEntry(() =>
                {
                    Inventory inventory = (aiBase.Character as Humanoid).GetInventory();
                    m_equippedHammer = inventory.GetItem("$item_hammer");
                    if (m_equippedHammer == null)
                    {
                        Common.Dbgl($"Have no hammer, cannot repair", true, "");
                        brain.Fire(Trigger.Abort);
                        return;
                    }
                    (aiBase.Character as Humanoid).EquipItem(m_equippedHammer);

                    Common.Dbgl("Entering DynamicRepairingBehaviour", true);
                })
                .OnExit(() =>
                {
                    Abort();
                });

            brain.Configure(State.FindAssignment)
                .SubstateOf(State.Main)
                .Permit(Trigger.StartAssignment, State.MoveToAssignment)
                .OnEntry(t =>
                {
                    var repairPiece = GetNewAssignment(aiBase.Character.transform.position);
                    if (null == repairPiece)
                    {
                        Common.Dbgl($"{aiBase.Character.GetHoverName()}: Could not find any piece to repair near postion", true, "");
                        brain.Fire(Trigger.Abort);
                        return;
                    }
                    else
                    {
                        m_assignedTimer = 0;
                        m_startPosition = aiBase.Instance.transform.position;
                        m_assignment = repairPiece;
                        brain.Fire(Trigger.StartAssignment);
                    }
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.MoveToAssignment)
                .SubstateOf(State.Main)
                .Permit(Trigger.IsCloseToAssignment, State.RepairAssignment)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.MoveToAssignment);
                    m_closeEnoughTimer = 0;
                })
                .OnExit(() =>
                {
                    aiBase.StopMoving();
                });

            brain.Configure(State.RepairAssignment)
                .SubstateOf(State.Main)
                .Permit(Trigger.AssignmentFailed, State.Main)
                .Permit(Trigger.AssignmentFinished, State.Main)
                .OnEntry(t =>
                {
                    if (Common.GetNView(m_assignment)?.IsValid() != true)
                    {
                        m_assignment = null;
                        brain.Fire(Trigger.AssignmentFailed);
                        return;
                    }
                    aiBase.UpdateAiStatus(State.RepairAssignment, m_assignment.m_name);
                    m_repairStepTimer = 0.0f;
                })
                .OnExit(t =>
                {
                    if (t.Trigger == Trigger.AssignmentFailed || Common.GetNView<Piece>(m_assignment)?.IsValid() != true) return;
                    var pieceToRepair = m_assignment;
                    WearNTear component = pieceToRepair.GetComponent<WearNTear>();
                    if ((bool)component && component.Repair())
                    {
                        pieceToRepair.m_placeEffect.Create(pieceToRepair.transform.position, pieceToRepair.transform.rotation);
                    }
                    m_assignment = null;
                });
        }

        private string m_lastState = "";

        public void Update(MobAIBase instance, float dt)
        {
            if (instance.Brain.State != m_lastState)
            {
                Common.Dbgl($"{instance.Character.GetHoverName()}:State:{instance.Brain.State}", true, "");
                m_lastState = instance.Brain.State;
            }

            //Assigned timeout-function 
            m_assignedTimer += dt;
            if (m_assignedTimer > TimeLimitOnAssignment)
            {
                instance.Brain.Fire(Trigger.AssignmentTimedOut);
                return;
            }

            if (instance.Brain.IsInState(State.MoveToAssignment))
            {
                if (MoveToAssignment(instance, dt))
                {
                    instance.Brain.Fire(Trigger.IsCloseToAssignment);
                }
                return;
            }

            if (instance.Brain.IsInState(State.RepairAssignment))
            {
                m_repairStepTimer += dt;
                if (m_repairStepTimer > RepairStepTime)
                {
                    m_repairStepTimer = 0;
                    var zAnim = typeof(Character).GetField("m_zanim", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance.Character) as ZSyncAnimation;
                    zAnim.SetTrigger(m_equippedHammer.m_shared.m_attack.m_attackAnimation);
                    var wnt = m_assignment.GetComponent<WearNTear>();
                    wnt.ApplyDamage(wnt.m_health*-RepairStepImprovement);
                    if (wnt.GetHealthPercentage() >= 1.0)
                    {
                        instance.Brain.Fire(Trigger.AssignmentFinished);
                        return;
                    }
                }
                return;
            }
        }

        private Piece GetNewAssignment(Vector3 position)
        {
            Common.Dbgl($"{m_aiBase.Character.GetHoverName()}:Enter {nameof(GetNewAssignment)}", true);
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, m_aiBase.Awareness * 5, pieceList);
            var availablePieces = pieceList
                .Where(p => p.m_category == Piece.PieceCategory.Building || p.m_category == Piece.PieceCategory.Crafting)
                .Where(p => Common.GetNView(p)?.IsValid() ?? false)
                .Where(p => NeedRepair(p))
                .OrderBy(p => Vector3.Distance(p.GetCenter(), position))
                .ToList();
            Piece piece = null;
            if (RequireLineOfSightToDiscoverAssignment)
            {
                foreach (var p in availablePieces)
                {
                    if (Common.CanSeeTarget(m_aiBase.Instance, p.GetComponentInParent<StaticTarget>().GetAllColliders().ToArray()))
                    {
                        piece = p;
                        break;
                    }
                }
            }
            else
            {
                piece = availablePieces.FirstOrDefault();
            }
            return piece;
        }

        private bool NeedRepair(Piece piece)
        {
            var wnt = piece.GetComponent<WearNTear>();
            float health = wnt?.GetHealthPercentage() ?? 1.0f;
            return health < 0.9f;
        }

        public bool MoveToAssignment(MobAIBase instance, float dt)
        {
            if (!m_assignment)
            {
                Common.Dbgl($"{instance.Character.GetHoverName()}:No assignments to move to", true,"");
                instance.Brain.Fire(Trigger.AssignmentFailed);
                return true;
            }
            if (!(bool)m_assignment)
            {
                Common.Dbgl("RepairPiece is null", true, "");
                m_assignment = null;
                instance.Brain.Fire(Trigger.AssignmentFailed);
                return false;
            }
            bool assignmentIsInvalid = m_assignment.GetComponent<ZNetView>()?.IsValid() == false;
            if (assignmentIsInvalid)
            {
                Common.Dbgl("Repair piece is invalid", true, "");
                m_assignment = null;
                instance.Brain.Fire(Trigger.AssignmentFailed);
                return false;
            }
            float distance = (m_closeEnoughTimer += dt) > CloseEnoughTimeout ? 2.0f : 1.5f;
            return instance.MoveAndAvoidFire(m_assignment.transform.position, dt, distance, false, true);
        }

        public void Abort()
        {
            if (m_equippedHammer != null)
            {
                (m_aiBase.Character as Humanoid).UnequipItem(m_equippedHammer);
                m_equippedHammer = null;
            }
        }
    }
}
