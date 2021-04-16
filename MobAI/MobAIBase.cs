using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public abstract class MobAIBase
    {
        public BaseAI Instance { get; private set; }

        public Character Character
        {
            get
            {
                if (Instance == null) throw new ArgumentException("Instance is missing");
                return Traverse.Create(Instance).Field("m_character").GetValue(Instance) as Character;
            }
        }

        public ZNetView NView
        {
            get
            {
                if (Instance == null) throw new ArgumentException("Instance is missing");
                return Traverse.Create(Instance).Field("m_nview").GetValue(Instance) as ZNetView;
            }
        }

        public float TimeSinceHurt
        {
            get
            {
                if (Instance == null) throw new ArgumentException("Instance is missing");
                return (float)Traverse.Create(Instance).Field("m_timeSinceHurt").GetValue(Instance);
            }
        }

        public virtual void UpdateAI(BaseAI instance, float dt)
        {
            Instance = instance;


        }

        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        protected bool AvoidFire(float dt)
        {
            EffectArea effectArea2 = EffectArea.IsPointInsideArea(Instance.transform.position, EffectArea.Type.Burning, 2f);
            if ((bool)effectArea2)
            {
                Invoke<BaseAI>(Instance, "RandomMovementArroundPoint", dt, effectArea2.transform.position, effectArea2.GetRadius() + 3f, true);
                return true;
            }
            return false;
        }

    }
}
