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
                .PermitDynamic(Trigger.Flee, () => FailState)
                .Permit(Trigger.TargetLost, State.IdentifyEnemy)
                .SubstateOf(parentState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus("Entered fighting behaviour");
                    m_startPosition = aiBase.Instance.transform.position;
                });

            brain.Configure(State.IdentifyEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundTarget, State.SelectWeapon)
                .Permit(Trigger.NoTarget, State.DoneFighting)
                .OnEntry(t =>
                {
                    if (aiBase.TargetCreature != null)
                    {
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
                    m_circleTimer = 100 / m_agressionLevel + 1;
                });


            brain.Configure(State.DoneFighting)
                .SubstateOf(State.Main)
                .PermitDynamic(Trigger.Done, () => SuccessState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus("Done fighting.");
                });
        }


        public void Update(MobAIBase aiBase, float dt)
        {
            float health = aiBase.Character.GetHealth() / aiBase.Character.GetMaxHealth();
            if (health < 1 / m_agressionLevel)
            {
                aiBase.Brain.Fire(Trigger.Flee);
                return;
            }

            if (aiBase.TargetCreature == null)
            {
                aiBase.Brain.Fire(Trigger.TargetLost);
            }

            if (aiBase.Brain.IsInState(State.IdentifyEnemy))
            {
                m_searchTimer -= dt;
                aiBase.TargetCreature = BaseAI.FindClosestEnemy(aiBase.Character, aiBase.Character.transform.position, m_circleTargetDistance);
                if (aiBase.TargetCreature != null)
                {
                    aiBase.Brain.Fire(Trigger.FoundTarget);
                }
                else
                {
                    Common.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, m_startPosition, m_circleTargetDistance, true);
                }
                if (m_searchTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.NoTarget);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.TrackingEnemy))
            {
                Debug.LogWarning($"Enemy:{aiBase.TargetCreature}, Weapon:{m_weapon}, AttackRange:{m_weapon.m_shared.m_aiAttackRange}, Dist:{Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position)}");
                m_searchTimer -= dt;
                if (aiBase.MoveAndAvoidFire(aiBase.TargetCreature.transform.position, dt, Math.Max(m_weapon.m_shared.m_aiAttackRange - 0.5f, 1.0f), true))
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
                if (m_searchTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.NoTarget);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.EngagingEnemy))
            {
                m_circleTimer -= dt;
                bool isLookingAtTarget = (bool)Common.Invoke<MonsterAI>(aiBase.Instance, "IsLookingAt", aiBase.TargetCreature.transform.position, 10f);
                bool isCloseToTarget = Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) < m_weapon.m_shared.m_aiAttackRange;
                Debug.LogWarning($"isLookingAtTarget:{isLookingAtTarget}, isCloseToTarget:{isCloseToTarget}, circleTime:{m_circleTimer}");
                if (!isCloseToTarget)
                {
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
                if (!isLookingAtTarget)
                {
                    Common.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                    return;
                }
                if (m_circleTimer <= 0)
                {

                    aiBase.Brain.Fire(Trigger.Reposition);
                    return;
                }
                Debug.LogWarning("Smiting the enemy!");
                Common.Invoke<MonsterAI>(aiBase.Instance, "DoAttack", aiBase.TargetCreature, false);
                return;
            }

            if (aiBase.Brain.IsInState(State.CirclingEnemy))
            {
                m_circleTimer -= dt;
                Common.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, aiBase.TargetCreature.transform.position, m_circleTargetDistance, true);
                if (m_circleTimer <= 0)
                {
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
            }

        }
    }
}
