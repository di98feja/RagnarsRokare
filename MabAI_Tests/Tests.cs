using Microsoft.VisualStudio.TestTools.UnitTesting;
using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        [TestMethod]
        public void TestParentOnExit()
        {
            bool parentOnExit = false;
            bool childOnExit = false;
            var fsm = new StateMachine<string, string>("child");
            fsm.Configure("grandParent");
            fsm.Configure("parent")
                .SubstateOf("grandParent")
                .OnExit(t => parentOnExit = true);
            fsm.Configure("child")
                .SubstateOf("parent")
                .Permit("leave", "grandParent")
                .OnExit(t => childOnExit = true);


            fsm.Fire("leave");
            Assert.IsTrue(parentOnExit);
            Assert.IsTrue(childOnExit);
        }

        [TestMethod]
        public void StorageContainer_Serialize_Roundtrip()
        {
            var sc = new StorageContainer("MyUniqueId", new Vector3(1f, 2f, 3f));
            var sc2 = StorageContainer.DeSerialize(sc.Serialize());
            Assert.AreEqual(sc.UniqueId, sc2.UniqueId);
            //Assert.AreEqual(sc.Position.x, sc2.Position.x);
        }

        [TestMethod]
        public void SerializeDict_Roundtrip()
        {
            var itemsDict = new Dictionary<string, IEnumerable<(StorageContainer container, int count)>>();
            itemsDict.Add("item1", new(StorageContainer, int)[] { (new StorageContainer("c1", new Vector3(1.6789f,2.7890f,3.8901f)),5) });
            itemsDict.Add("item2", new (StorageContainer, int)[] { (new StorageContainer("c1", new Vector3(1f, 2f, 3f)), 5), (new StorageContainer("c2", new Vector3(2f, 3f, 4f)), 10) });
            var serializedDict = string.Join(string.Empty, itemsDict.Select(d => $"[{d.Key}:{string.Join("", d.Value.Select(c => $"[{c.container.Serialize()};{c.count}]"))}]"));

            var itemsDict2 = new Dictionary<string, IEnumerable<(StorageContainer container, int count)>>();
            foreach (var item in serializedDict.SplitBySqBrackets())
            {
                var itemData = item.Split(':');
                string key = itemData.First();
                var containerList = new List<(StorageContainer, int)>();
                foreach (var c in item.SplitBySqBrackets())
                {
                    var sc = StorageContainer.DeSerialize(c.Split(';').First());
                    var num = int.Parse(c.Split(';').Last());
                    containerList.Add((sc, num));
                }
                itemsDict2.Add(key, containerList);
            }

            Assert.AreEqual(itemsDict.Count, itemsDict2.Count);
            Assert.AreEqual(itemsDict["item1"].Count(), itemsDict2["item1"].Count());
            Assert.AreEqual(itemsDict["item1"].First().container.Position, itemsDict2["item1"].First().container.Position);
            Assert.AreEqual(itemsDict["item1"].First().container.UniqueId, itemsDict2["item1"].First().container.UniqueId);
            Assert.AreEqual(itemsDict["item1"].First().count, itemsDict2["item1"].First().count);

            Assert.AreEqual(itemsDict["item2"].Count(), itemsDict2["item2"].Count());
            Assert.AreEqual(itemsDict["item2"].First().container.Position, itemsDict2["item2"].First().container.Position);
            Assert.AreEqual(itemsDict["item2"].First().container.UniqueId, itemsDict2["item2"].First().container.UniqueId);
            Assert.AreEqual(itemsDict["item2"].First().count, itemsDict2["item2"].First().count);
            Assert.AreEqual(itemsDict["item2"].Last().container.Position, itemsDict2["item2"].Last().container.Position);
            Assert.AreEqual(itemsDict["item2"].Last().container.UniqueId, itemsDict2["item2"].Last().container.UniqueId);
            Assert.AreEqual(itemsDict["item2"].Last().count, itemsDict2["item2"].Last().count);
        }
    }
}
