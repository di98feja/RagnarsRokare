using Stateless;
using System.Collections.Generic;

namespace RagnarsRokare.MobAI
{
    public interface IBehaviour
    {
        IEnumerable<string> RegisterStates();
        IEnumerable<string> RegisterTriggers();
        void Configure(StateMachine<string, string> brain, string SuccessState, string FailedState, string parentState);
        void Update(MobAIBase instance, float dt);
    }
}
