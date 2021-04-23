using Stateless;
using System;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public abstract class MobAIBase
    {
        public BaseAI Instance { get; private set; }

        public StateMachine<string, string> Brain;

        public string CurrentState { get; protected set; }

        public MobAIBase(BaseAI instance, string initState)
        {
            Instance = instance;
            Brain = new StateMachine<string,string>(() => CurrentState, s => CurrentState = s);
            Brain.OnUnhandledTrigger((state, trigger) => { });
            CurrentState = initState;
        }

        public Character Character
        {
            get
            {
                if (Instance == null) throw new ArgumentException("Instance is missing");
                return Instance.GetType().GetField("m_character", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Instance) as Character;
            }
        }

        public ZNetView NView
        {
            get
            {
                if (Instance == null) throw new ArgumentException("Instance is missing");
                return Instance.GetType().GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Instance) as ZNetView;
            }
        }

        public float TimeSinceHurt
        {
            get
            {
                if (Instance == null) throw new ArgumentException("Instance is missing");
                return (float)Instance.GetType().GetField("m_timeSinceHurt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Instance);
            }
        }

        public virtual void UpdateAI(float dt)
        {
        }

        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        public void StopMoving()
        {
            if (Instance == null) throw new ArgumentException("Instance is missing");
            Invoke<BaseAI>(Instance, "StopMoving");
        }

        public bool AvoidFire(float dt)
        {
            EffectArea effectArea2 = EffectArea.IsPointInsideArea(Instance.transform.position, EffectArea.Type.Burning, 2f);
            if ((bool)effectArea2)
            {
                Invoke<BaseAI>(Instance, "RandomMovementArroundPoint", dt, effectArea2.transform.position, effectArea2.GetRadius() + 3f, true);
                return true;
            }
            return false;
        }

        public bool MoveAndAvoidFire(Vector3 destination, float dt, float distance)
        {
            if (AvoidFire(dt)) return false;

            return (bool)Invoke<MonsterAI>(Instance, "MoveAndAvoid", dt, destination, distance, false);
        }

        public static string UpdateAiStatus(ZNetView nview, string newStatus)
        {
            newStatus = Localization.instance.Localize(newStatus);
            string currentAiStatus = nview?.GetZDO()?.GetString(Constants.Z_AiStatus);
            if (currentAiStatus != newStatus)
            {
                string name = nview?.GetZDO()?.GetString(Constants.Z_GivenName);
                Debug.Log($"{name}: {newStatus}");
                nview.GetZDO().Set(Constants.Z_AiStatus, newStatus);
            }
            return newStatus;
        }


    }
}
