using Stateless;
using System.Collections.Generic;

namespace RagnarsRokare.MobAI
{
    public interface IBehaviour
    {
        void Configure(StateMachine<string, string> brain, string SuccessState, string FailedState, string parentState);
        void Update(MobAIBase instance, float dt);
    }
}
