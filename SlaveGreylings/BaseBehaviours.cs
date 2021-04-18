using Stateless;
using Stateless.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlaveGreylings
{
    class BaseBehaviours : MobAI.IBehaviour
    {
        private string m_parentState;

        public Action<Stateless.StateMachine<string,string>.Transition> OnAvoidFireEntry;

        public void Configure(StateMachine<string, string> brain)
        {
        }

        public IEnumerable<string> RegisterStates()
        {
            return new List<string>()
            {

            };
        }

        public IEnumerable<string> RegisterTriggers()
        {
            throw new NotImplementedException();
        }

        public void Update()
        {
            throw new NotImplementedException();
        }
    }
}
