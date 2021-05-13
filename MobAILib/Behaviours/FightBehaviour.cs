using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    class FightBehaviour : IBehaviour
    {
        private const string Prefix = "RR_FIGHT";
        private class State
        {
            public const string Main = Prefix + "Main";
            public const string IdentifyEnemy = Prefix + "IdentifyEnemy";
            public const string TrackingEnemy = Prefix + "TrackingEnemy";
            public const string EngaugingEnemy = Prefix + "EngaugingEnemy";
            public const string CirclingEnemy = Prefix + "CirclingEnemy";
            public const string AvoidFire = Prefix + "AvoidFire"; 
            public const string DoneFigfhting = Prefix + "DoneFigfhting";
        }

        private class Trigger
        {
            public const string Failed = Prefix + "Failed";
            public const string Timeout = Prefix + "Timeout";
            public const string FoundTarget = Prefix + "FoundTarget";
            public const string NoTarget = Prefix + "NoTarget";
            public const string Attack = Prefix + "Attack";
            public const string Flee = Prefix + "Flee";
            public const string Reposition = Prefix + "Reposition";
        }

        // Input

        // Output

        // Settings
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public string InitState { get { return State.Main; } }
        private bool m_canHearTarget = false;
        private bool m_canSeeTarget = false;
        private ItemDrop.ItemData m_weapon;
        private float m_circleTargetDistance = 10;
        private float m_agressionLevel = 10;
        private float m_circleTimer;
        private float m_searchTimer;
        private MobAIBase m_aiBase;
        private object m_startPosition;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            
            brain.Configure(State.Main)
                .InitialTransition(State.IdentifyEnemy)
                .Permit(Trigger.Flee, FixerAI.State.Flee)
                .SubstateOf(parentState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus("Entered fighting behaviour");
                    m_startPosition = aiBase.Instance.transform.position;
                });
            
            brain.Configure(State.IdentifyEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundTarget, State.TrackingEnemy)
                .Permit(Trigger.NoTarget, State.DoneFigfhting)
                .OnEntry(t =>
                {
                    if (aiBase.TargetCreature != null)
                    {
                        aiBase.Brain.Fire(Trigger.FoundTarget);
                        return;
                    }
                    m_searchTimer = m_agressionLevel*2;
                });
            
            brain.Configure(State.TrackingEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.Attack, State.EngaugingEnemy)
                .Permit(Trigger.NoTarget, State.IdentifyEnemy)
                .OnEntry(t =>
                {

                });
            
            brain.Configure(State.EngaugingEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.Attack, State.TrackingEnemy)
                .Permit(Trigger.NoTarget, State.IdentifyEnemy)
                .Permit(Trigger.Reposition, State.CirclingEnemy)
                .OnEntry(t =>
                {
                    m_circleTimer = m_agressionLevel;
                });
            
            brain.Configure(State.CirclingEnemy)
                .Permit(Trigger.Attack, State.TrackingEnemy)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    m_circleTimer = 100/m_agressionLevel+1;
                });
            

            brain.Configure(State.DoneFigfhting)
                .InitialTransition(FixerAI.State.Idle)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus("Done fighting.");
                });
        }


        public void Update(MobAIBase aiBase, float dt)
        {
            float helth = aiBase.Character.GetMaxHealth()/aiBase.Character.GetHealth();
            if (helth < 1 / m_agressionLevel)
            {
                aiBase.Brain.Fire(Trigger.Flee);
                return;
            }

            if (aiBase.Brain.IsInState(State.IdentifyEnemy))
            {
                m_searchTimer -= dt;
                Debug.Log("1"); 
                Common.Invoke<MonsterAI>(aiBase.Character.GetComponent<MonsterAI>(), "UpdateTarget", (aiBase.Character as Humanoid), dt, m_canHearTarget, m_canSeeTarget);
                Debug.Log("2");
                if (m_canHearTarget || m_canSeeTarget)
                {
                    Debug.Log("3");
                    m_weapon = (ItemDrop.ItemData)Common.Invoke<MonsterAI>(aiBase.Character.GetComponent<MonsterAI>(), "SelectBestAttack", (aiBase.Character as Humanoid), dt);
                    Debug.Log("4");
                    aiBase.Brain.Fire(Trigger.NoTarget);
                }
                else
                {
                    Debug.Log("5");
                    Common.Invoke<MonsterAI>(aiBase.Character.GetComponent<MonsterAI>(), "RandomMovementArroundPoint", dt, m_startPosition, m_circleTargetDistance, true);
                    Debug.Log("6");
                }
                if (m_searchTimer <= 0)
                {
                    Debug.Log("7");
                    aiBase.Brain.Fire(Trigger.NoTarget);
                }
                Debug.Log("8");
                return;
            }

            if (aiBase.Brain.IsInState(State.TrackingEnemy))
            {
                m_searchTimer -= dt;
                aiBase.MoveAndAvoidFire(aiBase.TargetCreature.transform.position, dt, m_weapon.m_shared.m_aiAttackRange, true);
                if (Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) < m_weapon.m_shared.m_aiAttackRange - 0.5f)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Attack);
                }
                if (m_searchTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.NoTarget);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.EngaugingEnemy))
            {
                m_circleTimer -= dt;
                bool isLookingAtAssignment = (bool)Common.Invoke<MonsterAI>(aiBase.Character.GetComponent<MonsterAI>(), "IsLookingAt", aiBase.TargetCreature.transform.position, 10f);
                bool isCloseToTarget = Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) < m_weapon.m_shared.m_aiAttackRange;
                if (!isCloseToTarget)
                {
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
                if (!isLookingAtAssignment)
                {
                    Common.Invoke<MonsterAI>(aiBase.Character.GetComponent<MonsterAI>(), "LookAt", aiBase.TargetCreature.transform.position);
                    return;
                }
                if (m_circleTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.Reposition);
                    return;
                }
                Common.Invoke<MonsterAI>(aiBase.Character.GetComponent<MonsterAI>(), "DoAttack", aiBase.TargetCreature, false);
                return;
            }

            if (aiBase.Brain.IsInState(State.CirclingEnemy))
            {
                m_circleTimer -= dt;
                Common.Invoke<MonsterAI>(aiBase.Character.GetComponent<MonsterAI>(), "RandomMovementArroundPoint", dt, aiBase.TargetCreature.transform.position, m_circleTargetDistance, true);
                if (m_circleTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
            }

        }
    }
}
