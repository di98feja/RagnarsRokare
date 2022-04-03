using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RagnarsRokare.MobAI
{
    public static class BehaviourFactory
    {
        private static Dictionary<string, Type> BehaviourTypes = new Dictionary<string, Type>();
        static BehaviourFactory()
        {
            var it = typeof(IDynamicBehaviour);
            var executingAsm = Assembly.GetExecutingAssembly();
            var mobAILibAsm = Assembly.GetAssembly(typeof(MobAILib));
            var behaviours = executingAsm.GetLoadableTypes().Where(it.IsAssignableFrom).Where(t => !(t.Equals(it))).ToList();
            if (executingAsm != mobAILibAsm)
            {
                behaviours.AddRange(behaviours.Where(t => !(t.Equals(it))));
            }
            BehaviourTypes = behaviours.ToDictionary(t => t.Name, t => t);
        }

        public static IDynamicBehaviour Create(string behaviourName)
        {
            return Activator.CreateInstance(BehaviourTypes[behaviourName]) as IDynamicBehaviour;
        }

        public static IDynamicBehaviour Create<T>()
        {
            return Activator.CreateInstance<T>() as IDynamicBehaviour;
        }

        public static IDynamicBehaviour Create(string behaviourName, MobAIBase mobAI, StateMachine<string,string> brain, string parentState)
        {
            var behaviour = Activator.CreateInstance(BehaviourTypes[behaviourName]) as IDynamicBehaviour;
            behaviour.Configure(mobAI, brain, parentState);
            return behaviour;
        }

        public static IDynamicBehaviour Create<T>(MobAIBase mobAI, StateMachine<string, string> brain, string parentState)
        {
            var behaviour = Activator.CreateInstance<T>() as IDynamicBehaviour;
            behaviour.Configure(mobAI, brain, parentState);
            return behaviour;
        }
    }
}
