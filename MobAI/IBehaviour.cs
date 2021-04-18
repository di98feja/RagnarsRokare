using Stateless;
using System.Collections.Generic;

namespace MobAI
{
    public interface IBehaviour
    {
        IEnumerable<string> RegisterStates();
        IEnumerable<string> RegisterTriggers();
        void Configure(StateMachine<string, string> brain);
        void Update();
    }
}
