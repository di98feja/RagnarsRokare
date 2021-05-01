using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlaveGreylings
{
    class FightBehaviour : IBehaviour
    {
        private const string Prefix = "RR_FIGHT";

        private const string Main_state = Prefix + "Main";
        private const string IdentifyEnemy_state = Prefix + "IdentifyEnemy";
        private const string AvoidFire_state = Prefix + "AvoidFire";

        private const string Failed_trigger = Prefix + "Failed";
        private const string Timeout_trigger = Prefix + "Timeout";


        // Input

        // Output

        // Settings
        public string InitState { get { return Main_state; } }

        private MobAIBase m_aiBase;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;

            brain.Configure(Main_state)
                .InitialTransition(IdentifyEnemy_state)
                .SubstateOf(parentState)
                .OnEntry(t =>
                {
                    Debug.Log("Entered FightBehaviour");
                });

            brain.Configure(IdentifyEnemy_state)
                .SubstateOf(Main_state)
                .OnEntry(t =>
                {
                    
                });

        }


        public void Update(MobAIBase aiBase, float dt)
        {

        }
    }
}
