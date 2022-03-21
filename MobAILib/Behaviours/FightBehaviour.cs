﻿using Stateless;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    class FightBehaviour : IFightBehaviour
    {
        private const string Prefix = "RR_FIGHT";
        private class State
        {
            public const string Main = Prefix + "Main";
            public const string IdentifyEnemy = Prefix + "IdentifyEnemy";
            public const string SelectWeapon = Prefix + "SelectWeapon";
            public const string TrackingEnemy = Prefix + "TrackingEnemy";
            public const string EngagingEnemy = Prefix + "EngagingEnemy";
            public const string CirclingEnemy = Prefix + "CirclingEnemy";
            public const string AvoidFire = Prefix + "AvoidFire";
            public const string DoneFighting = Prefix + "DoneFighting";
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
        
        public float MobilityLevel { get; set; }
        public float AgressionLevel { get; set; }
        public float AwarenessLevel { get; set; }

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

        public bool IsBelowHealthThreshold(MobAIBase aiBase)
        {
            return aiBase.Character.GetHealthPercentage() < 1 - AgressionLevel * 0.08;
        }

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
                    m_aiBase.UpdateAiStatus(State.Main);
                    m_startPosition = aiBase.Instance.transform.position;
                    m_circleTargetDistance = System.Math.Max(MobilityLevel - AgressionLevel, 1);
                    m_searchTargetMovement = System.Math.Max(MobilityLevel, AgressionLevel);
                    m_viewRange = m_searchTargetMovement + AwarenessLevel;
                    
                })
                .OnExit(t =>
                {
                    Debug.Log($"{aiBase.Character.m_name}: FightBehaviour, OnExit()");
                    aiBase.StopMoving();
                    aiBase.TargetCreature = null;
                    Utils.Invoke<MonsterAI>(aiBase.Instance, "SetAlerted", false);
                });

            brain.Configure(State.IdentifyEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundTarget, State.SelectWeapon)
                .Permit(Trigger.NoTarget, State.DoneFighting)
                .OnEntry(t =>
                {
                    //Debug.Log("IdentifyEnemy-Enter");
                    m_searchTimer = AgressionLevel * 2;
                    if (aiBase.Attacker != null && aiBase.Instance.CanSenseTarget(aiBase.Attacker))
                    {
                        aiBase.TargetCreature = aiBase.Attacker;
                        aiBase.Brain.Fire(Trigger.FoundTarget);
                        aiBase.UpdateAiStatus(State.IdentifyEnemy, aiBase.TargetCreature.GetHoverName());
                        return;
                    }
                });

            brain.Configure(State.SelectWeapon)
                .SubstateOf(State.Main)
                .Permit(Trigger.WeaponSelected, State.TrackingEnemy)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    //Debug.Log("SelectWeapon-Enter");
                    m_weapon = (ItemDrop.ItemData)Utils.Invoke<MonsterAI>(aiBase.Instance, "SelectBestAttack", (aiBase.Character as Humanoid), 1.0f);
                    if (m_weapon == null)
                    {
                        //Debug.Log("SelectWeapon-Fail");
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
                    //Debug.Log("TrackingEnemy-Enter");
                    m_searchTimer = AgressionLevel * 2;
                });

            brain.Configure(State.EngagingEnemy)
                .SubstateOf(State.Main)
                .Permit(Trigger.Attack, State.TrackingEnemy)
                .Permit(Trigger.NoTarget, State.IdentifyEnemy)
                .Permit(Trigger.Reposition, State.CirclingEnemy)
                .OnEntry(t =>
                {
                    m_circleTimer = AgressionLevel;
                });

            brain.Configure(State.CirclingEnemy)
                .Permit(Trigger.Attack, State.TrackingEnemy)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    m_circleTimer = 10f / AgressionLevel;
                    aiBase.Character.Heal(aiBase.Character.GetMaxHealth()/50);
                });


            brain.Configure(State.DoneFighting)
                .SubstateOf(State.Main)
                .PermitDynamic(Trigger.Done, () => SuccessState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.DoneFighting);
                    aiBase.Character.Heal(aiBase.Character.GetMaxHealth() / 10);
                })
                .OnExit(t =>
                {
                    aiBase.Attacker = null;
                    aiBase.TimeSinceHurt = 20;
                });
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (IsBelowHealthThreshold(aiBase))
            {
                aiBase.Brain.Fire(Trigger.Flee);
                return;
            }

            if (aiBase.Brain.IsInState(State.IdentifyEnemy))
            {
                m_searchTimer -= dt;
                Utils.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, m_startPosition, m_searchTargetMovement, true);
                if (Vector3.Distance(m_startPosition, aiBase.Character.transform.position) > m_searchTargetMovement + 1)
                {
                    return;
                }
                aiBase.TargetCreature = BaseAI.FindClosestEnemy(aiBase.Character, m_startPosition, m_viewRange);
                if (aiBase.TargetCreature != null && Vector3.Distance(m_startPosition, aiBase.TargetCreature.transform.position) < m_viewRange)
                {
                    Utils.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                    //Debug.Log("IdentifyEnemy-FoundTarget");
                    aiBase.Brain.Fire(Trigger.FoundTarget);
                    return;
                }
                if (m_searchTimer <= 0)
                {
                    //Debug.Log("IdentifyEnemy-NoTarget");
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.NoTarget);
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
                if (aiBase.Attacker != null && aiBase.TargetCreature != aiBase.Attacker && aiBase.Instance.CanSenseTarget(aiBase.Attacker))
                {
                    aiBase.TargetCreature = aiBase.Attacker;
                    ////Debug.Log("TrackingEnemy-Switch target to Attacker");
                }
                Utils.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                if (Vector3.Distance(m_startPosition, aiBase.Character.transform.position) > m_viewRange && (aiBase.TargetCreature != aiBase.Attacker || AgressionLevel < 5))
                {
                    //Debug.Log("TrackingEnemy-NoTarget(lost track)");
                    aiBase.TargetCreature = null;
                    aiBase.Attacker = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.NoTarget);
                    return;
                }
                //Debug.Log("TrackingEnemy-MoveToTarget");
                if (aiBase.MoveAndAvoidFire(aiBase.TargetCreature.transform.position, dt, Math.Max(m_weapon.m_shared.m_aiAttackRange - 0.5f, 1.0f), true))
                {
                    aiBase.StopMoving();
                    //Debug.Log("TrackingEnemy-Attack");
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
                if (m_searchTimer <= 0)
                {
                    //Debug.Log("TrackingEnemy-NoTarget(timeout)");
                    aiBase.TargetCreature = null;
                    aiBase.Attacker = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.NoTarget);
                }
                //Debug.Log("TrackingEnemy-End");
                return;
            }

            if (aiBase.Brain.IsInState(State.EngagingEnemy))
            {
                m_circleTimer -= dt;
                bool isLookingAtTarget = (bool)Utils.Invoke<MonsterAI>(aiBase.Instance, "IsLookingAt", aiBase.TargetCreature.transform.position, 10f);
                bool isCloseToTarget = Vector3.Distance(aiBase.Instance.transform.position, aiBase.TargetCreature.transform.position) < m_weapon.m_shared.m_aiAttackRange;
                if (!isCloseToTarget)
                {
                    //Debug.Log("EngagingEnemy-Attack");
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
                if (!isLookingAtTarget)
                {
                    Utils.Invoke<MonsterAI>(aiBase.Instance, "LookAt", aiBase.TargetCreature.transform.position);
                    return;
                }
                if (m_circleTimer <= 0)
                {
                    //Debug.Log("EngagingEnemy-Reposition");
                    aiBase.Brain.Fire(Trigger.Reposition);
                    return;
                }
                Utils.Invoke<MonsterAI>(aiBase.Instance, "DoAttack", aiBase.TargetCreature, false);
                //Debug.Log("EngagingEnemy-DoAttack");
                return;
            }

            if (aiBase.Brain.IsInState(State.CirclingEnemy))
            {
                m_circleTimer -= dt;
                Utils.Invoke<MonsterAI>(aiBase.Instance, "RandomMovementArroundPoint", dt, aiBase.TargetCreature.transform.position, m_circleTargetDistance, true);
                if (m_circleTimer <= 0)
                {
                    //Debug.Log("CirclingEnemy-Attack");
                    aiBase.Brain.Fire(Trigger.Attack);
                    return;
                }
            }

            if (aiBase.Brain.IsInState(State.DoneFighting))
            {
                aiBase.MoveAndAvoidFire(m_startPosition, dt, 0.5f, false);
                if (Vector3.Distance(m_startPosition, aiBase.Character.transform.position) < 1f)
                {
                    //Debug.Log("DoneFighting-Done");
                    aiBase.Brain.Fire(Trigger.Done);
                }
                return;
            }
        }
    }
}
