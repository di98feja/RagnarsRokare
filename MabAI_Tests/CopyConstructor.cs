using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stateless;
using System;
using System.Collections.Generic;

namespace MabAI_Tests
{
    [TestClass]
    public class CopyConstructor
    {
        class A
        {
            public A()
            {
                f1 = 1;
                f2 = "apa";
                f3 = new List<string> { "Den", "Ryska", "Räven" };
            }
            public A(int f1, string f2, IEnumerable<string> f3)
            {
                this.f1 = f1;
                this.f2 = f2;
                this.f3 = f3;
            }
            private int f1;
            private string f2;
            private IEnumerable<string> f3;
        }

        class B
        {
            public bool f1 = false;
            public float[] f2 = { 1.0f, 2.0f, 3.0f };
            public A f3 = new A();
        }

        class C : B
        {
            public C(B orig)
            {
                this.Copy(orig);
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            var myB = new B();
            myB.f1 = true;
            myB.f2 = new float[] { 1.1f, 2.2f, 3.3f, 4.4f };
            myB.f3 = new A(123, "Banan", new List<string> { "Rev", "En", "Annan", "Räv" });
            var myC = new C(myB);

        }


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
