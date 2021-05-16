using Stateless;
using System;
using System.Linq;
using System.Reflection;
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
            public const string SelectWeapon = Prefix + "SelectWeapon";
            public const string TrackingEnemy = Prefix + "TrackingEnemy";
            public const string EngagingEnemy = Prefix + "EngaugingEnemy";
            public const string CirclingEnemy = Prefix + "CirclingEnemy";
            public const string AvoidFire = Prefix + "AvoidFire";
            public const string DoneFighting = Prefix + "DoneFigfhting";
        }

        private class Trigger
        {
            public const string Failed = Prefix + "Failed";
            public const string Timeout = Prefix + "Timeout";
            public const string WeaponSelected = Prefix + "WeaponSelected";
            public const string FoundTarget = Prefix + "FoundTarget";
            public const string NoTarget = Prefix + "NoTarget";
            public const string TargetLost = Prefix + "TargetLost";
            public const string Attack = Prefix + "Attack";
            public const string Flee = Prefix + "Flee";
            public const string Reposition = Prefix + "Reposition";
            public const string Done = Prefix + "Done";
        }

        // Input

        // Output

        // Settings
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public string StartState { get { return State.Main; } }
        
        public float m_mobilitylevel;
        public float m_agressionLevel;
        public float m_Awareness;

        // Management
        private float m_viewRange;
        private Vector3 m_startPosition;
        private float m_circleTargetDistance;
        private float m_searchTargetMovement;
        private MobAIBase m_aiBase;
        private ItemDrop.ItemData m_weapon;

        // Timers
        private float m_circleTimer;
        private float m_searchTimer;
        

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            brain.Configure(State.Main)
                .InitialTransition(State.IdentifyEnemy)
                .PermitDynamic(Trigger.Flee, () => FailState)
                .Permit(Trigger.TargetLost, State.IdentifyEnemy)
                .SubstateOf(parentState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus("Entered fighting behaviour");
                    m_startPosition = aiBase.Instance.transform.position;
                    m_viewRange = m_Awareness * 5f;
                    m_circleTargetDistance = m_mobilitylevel * 2 - m_agressionLevel;
                    m_searchTargetMovement = m_mobilitylevel;
                })
                .OnExit(t =>
                {
                    aiBase.StopMoving();
                });

            brain.Configure(State.IdentifyEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundTarget, State.SelectWeapon)
                .Permit(Trigger.NoTarget, State.DoneFighting)
                .OnEntry(t =>
                {
                    if (aiBase.Instance.CanSenseTarget(aiBase.Attacker))
                    {
                        aiBase.TargetCreature = aiBase.Attacker;
                        aiBase.Brain.Fire(Trigger.FoundTarget);
                        return;
                    }
                    m_searchTimer = m_agressionLevel * 2;
                });

            brain.Configure(State.SelectWeapon)
                .SubstateOf(State.Main)
                .Permit(Trigger.WeaponSelected, State.TrackingEnemy)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    m_weapon = (ItemDrop.ItemData)Common.Invoke<MonsterAI>(aiBase.Instance, "SelectBestAttack", (aiBase.Character as Humanoid), 1.0f);
                    if (m_weapon == null)
                    {
                        brain.Fire(Trigger.Failed);

                    }
                    else
                    {
                        brain.Fire(Trigger.WeaponSelected);
                    }
                });


            brain.Configure(State.TrackingEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.Attack, State.EngagingEnemy)
                .Permit(Trigger.NoTarget, State.IdentifyEnemy)
                .OnEntry(t =>
                {

                });

            brain.Configure(State.EngagingEnemy)
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
                    m_circleTimer = 30f / m_agressionLevel;
                    aiBase.Character.Heal(aiBase.Character.GetMaxHealth()/50);
                });


            brain.Configure(State.DoneFighting)
                .SubstateOf(State.Main)
                .PermitDynamic(Trigger.Done, () => SuccessState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus("Done fighting.");
                    aiBase.Character.Heal(aiBase.Character.GetMaxHealth() / 10);
                })
                .OnExit(t =>
                {
                    aiBase.Attacker = null;
                    aiBase.TargetCreature = null;
                    aiBase.TimeSinceHurt = 20;
                });
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Character.GetHealthPercentage() < 1 - m_agressionLevel * 0.08)
            {
                aiBase.Brain.Fire(Trigger.Flee);
                return;
            }

            if (aiBase.Brain.IsInState(State.IdentifyEnemy))
            {
                m_searchTimer -= dt;
                Common.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, m_startPosition, m_circleTargetDistance, true);
                if (Vector3.Distance(m_startPosition, aiBase.Character.transform.position) > m_viewRange - 5)
                {
                    return;
                }
                aiBase.TargetCreature = BaseAI.FindClosestEnemy(aiBase.Character, m_startPosition, m_viewRange);
                if (aiBase.TargetCreature != null && Vector3.Distance(m_startPosition, aiBase.TargetCreature.transform.position) < m_viewRange)
                {
                    Common.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                    aiBase.Brain.Fire(Trigger.FoundTarget);
                    //Debug.Log("IdentifyEnemy-FoundTarget");
                    return;
                }
                if (m_searchTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.NoTarget);
                    //Debug.Log("IdentifyEnemy-NoTarget");
                    aiBase.StopMoving();
                }
                return;
            }

            if (aiBase.TargetCreature == null)
            {
                aiBase.Attacker = null;
                aiBase.Brain.Fire(Trigger.TargetLost);
                //Debug.Log("TargetLost");
                return;
            }

            if (aiBase.Brain.IsInState(State.TrackingEnemy))
            {
                m_searchTimer -= dt;
                if (aiBase.TargetCreature != aiBase.Attacker && aiBase.Instance.CanSenseTarget(aiBase.Attacker))
                {
                    aiBase.TargetCreature = aiBase.Attacker;
                    //Debug.Log("TrackingEnemy-Switch target to Attacker");
                }
                Common.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                if (Vector3.Distance(m_startPosition, aiBase.Character.transform.position) > m_viewRange && (aiBase.TargetCreature != aiBase.Attacker || m_agressionLevel < 5))
                {
                    aiBase.Brain.Fire(Trigger.NoTarget);
                    //Debug.Log("TrackingEnemy-NoTarget(lost track)");
                    aiBase.TargetCreature = null;
                    aiBase.Attacker = null;
                    aiBase.StopMoving();
                    return;
                }
                if (aiBase.MoveAndAvoidFire(aiBase.TargetCreature.transform.position, dt, Math.Max(m_weapon.m_shared.m_aiAttackRange - 0.5f, 1.0f), true))
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Attack);
                    //Debug.Log("TrackingEnemy-Attack");
                    return;
                }
                if (m_searchTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.NoTarget);
                    //Debug.Log("TrackingEnemy-NoTarget(timeout)");
                    aiBase.TargetCreature = null;
                    aiBase.Attacker = null;
                    aiBase.StopMoving();
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.EngagingEnemy))
            {
                m_circleTimer -= dt;
                bool isLookingAtTarget = (bool)Common.Invoke<MonsterAI>(aiBase.Instance, "IsLookingAt", aiBase.TargetCreature.transform.position, 10f);
                bool isCloseToTarget = Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) < m_weapon.m_shared.m_aiAttackRange;
                if (!isCloseToTarget)
                {
                    aiBase.Brain.Fire(Trigger.Attack);
                    //Debug.Log("EngagingEnemy-Attack");
                    return;
                }
                if (!isLookingAtTarget)
                {
                    Common.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                    return;
                }
                if (m_circleTimer <= 0)
                {
                    //Debug.Log("EngagingEnemy-Reposition");
                    aiBase.Brain.Fire(Trigger.Reposition);
                    return;
                }
                Common.Invoke<MonsterAI>(aiBase.Instance, "DoAttack", aiBase.TargetCreature, false);
                //Debug.Log("EngagingEnemy-DoAttack");
                return;
            }

            if (aiBase.Brain.IsInState(State.CirclingEnemy))
            {
                m_circleTimer -= dt;
                Common.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, aiBase.TargetCreature.transform.position, m_circleTargetDistance, true);
                if (m_circleTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.Attack);
                    //Debug.Log("CirclingEnemy-Attack");
                    return;
                }
            }

            if (aiBase.Brain.IsInState(State.DoneFighting))
            {
                aiBase.MoveAndAvoidFire(m_startPosition, dt, 0.5f, false);
                if (Vector3.Distance(m_startPosition, aiBase.Character.transform.position) < 1f)
                {
                    aiBase.Brain.Fire(Trigger.Done);
                    //Debug.Log("DoneFighting-Done");
                }
                return;
            }
        }
    }
}
