using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stateless;

namespace MabAI_Tests
{
    [TestClass]
    public class Tests
    {

        enum State
        {
            First,
            Second,
            Third
        }

        enum Trigger
        {
            Tick,
            Reset
        }

        [TestMethod]
        public void TestPermitIf()
        {
            var fsm = new StateMachine<State, Trigger>(State.First);
            fsm.Configure(State.First)
                .Permit(Trigger.Tick, State.Second);
            fsm.Configure(State.Second)
                .Permit(Trigger.Reset, State.First)
                .Permit(Trigger.Tick, State.Third);
            fsm.Configure(State.Third)
                .SubstateOf(State.First)
                .SubstateOf(State.Second)
                .Permit(Trigger.Tick, State.First);

            var currentState = fsm.State;
            fsm.Fire(Trigger.Tick);
            currentState = fsm.State;
            fsm.Fire(Trigger.Tick);
            currentState = fsm.State;
            fsm.Fire(Trigger.Reset);
            Assert.AreEqual(State.First, fsm.State);
        }
    }
}
