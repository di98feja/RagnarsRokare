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
        }

        private class Trigger
        {
            public const string Failed = Prefix + "Failed";
            public const string Timeout = Prefix + "Timeout";
            public const string FoundTarget = Prefix + "FoundTarget";
            public const string Attack = Prefix + "Attack";
            public const string Reposition = Prefix + "Reposition";
        }
        

        private bool m_canHearTarget = false;
        private bool m_canSeeTarget = false;
        private ItemDrop.ItemData m_weapon;
        private float m_circleTargetDistance = 10;
        private float m_circleTimer;

        // Input

        // Output

        // Settings
        public string InitState { get { return State.Main; } }

        private MobAIBase m_aiBase;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;

            brain.Configure(State.Main)
                .InitialTransition(State.IdentifyEnemy)
                .SubstateOf(parentState)
                .OnEntry(t =>
                {
                    Debug.Log("Entered FightBehaviour");
                });

            brain.Configure(State.IdentifyEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundTarget, State.TrackingEnemy)
                .OnEntry(t =>
                {
                    
                });

            brain.Configure(State.TrackingEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.Attack, State.EngaugingEnemy)
                .OnEntry(t =>
                {

                });

            brain.Configure(State.EngaugingEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundTarget, State.TrackingEnemy)
                .Permit(Trigger.Reposition, State.CirclingEnemy)
                .OnEntry(t =>
                {
                    m_circleTimer = 0;
                });

            brain.Configure(State.CirclingEnemy)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    
                });
        }


        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.IdentifyEnemy))
            {
                Common.Invoke<MonsterAI>(aiBase, "UpdateTarget", (aiBase.Character as Humanoid), dt, m_canHearTarget, m_canSeeTarget);
                if (m_canHearTarget || m_canSeeTarget)
                {
                    m_weapon = (ItemDrop.ItemData)Common.Invoke<MonsterAI>(aiBase, "SelectBestAttack", (aiBase.Character as Humanoid), dt);
                    aiBase.Brain.Fire(Trigger.FoundTarget);
                }
            }

            if (aiBase.Brain.IsInState(State.TrackingEnemy))
            {
                aiBase.MoveAndAvoidFire(aiBase.TargetCreature.transform.position, dt, m_weapon.m_shared.m_aiAttackRange, true);
                if (Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) < m_weapon.m_shared.m_aiAttackRange - 0.5f)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Attack);
                }
            }

            if (aiBase.Brain.IsInState(State.EngaugingEnemy))
            {
                m_circleTimer += dt;
                bool isLookingAtAssignment = (bool)Common.Invoke<MonsterAI>(aiBase, "IsLookingAt", aiBase.TargetCreature.transform.position, 10f);
                bool isCloseToTarget = Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) < m_weapon.m_shared.m_aiAttackRange;
                if (!isCloseToTarget)
                {
                    aiBase.Brain.Fire(Trigger.FoundTarget);
                    return;
                }
                if (!isLookingAtAssignment)
                {
                    Common.Invoke<MonsterAI>(aiBase, "LookAt", aiBase.TargetCreature.transform.position);
                    return;
                }
                if (m_circleTimer > 10)
                {
                    aiBase.Brain.Fire(Trigger.Reposition);
                    return;
                }
                Common.Invoke<MonsterAI>(aiBase, "DoAttack", aiBase.TargetCreature, false);
            }

            if (aiBase.Brain.IsInState(State.CirclingEnemy))
            {
                Common.Invoke<MonsterAI>(aiBase, "RandomMovementArroundPoint", dt, aiBase.TargetCreature.transform.position, m_circleTargetDistance, true);

            }
            


        }

    }
}
