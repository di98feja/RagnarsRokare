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
        }
        
        
        private bool m_canHearTarget = false;
        private bool m_canSeeTarget = false;

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
                .OnEntry(t =>
                {

                });

            brain.Configure(State.EngaugingEnemy)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {

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
                if(m_canHearTarget || m_canSeeTarget) aiBase.Brain.Fire(Trigger.FoundTarget);
            }

            if (aiBase.Brain.IsInState(State.TrackingEnemy))
            {
                //aiBase.MoveAndAvoidFire(Common.Invoke<Character>(aiBase, "m_targetCreature").transform.position, dt, 0.5f);
            }
        }

    }
}
