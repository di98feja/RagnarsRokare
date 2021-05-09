using Stateless;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public abstract class MobAIBase
    {
        private BaseAI m_instance = null;
        public BaseAI Instance 
        {
            get
            {
                if (m_instance == null) throw new ArgumentException("Instance is missing");
                return m_instance;
            }
            set
            {
                m_instance = value;
            }
        }

        public bool HasInstance()
        {
            return m_instance != null;
        }

        public StateMachine<string, string> Brain;

        public string learningTask;
        public int learningRate = 0;
        public List<string> m_trainedAssignments = new List<string>();

        public string CurrentState { get; protected set; }

        public MobAIBase()
        { }

        public MobAIBase(BaseAI instance, string initState)
        {
            
            m_instance = instance;
            Brain = new StateMachine<string,string>(() => CurrentState, s => CurrentState = s);
            Brain.OnUnhandledTrigger((state, trigger) => { });
            CurrentState = initState;
            if (NView.IsValid())
            {
                NView.Register<ZDOID, string>(Constants.Z_MobCommand, RPC_MobCommand);
            }
        }

        public Character Character
        {
            get
            {
                return Instance.GetType().GetField("m_character", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Instance) as Character;
            }
        }

        public ZNetView NView
        {
            get
            {
                return Instance.GetType().GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Instance) as ZNetView;
            }
        }

        public string UniqueID
        {
            get
            {
                return NView.GetZDO().GetString(Constants.Z_CharacterId);
            }
        }

        public float TimeSinceHurt
        {
            get
            {
                return (float)Instance.GetType().GetField("m_timeSinceHurt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Instance);
            }
        }

        public Character Attacker { get; set; }

        public abstract void Follow(Player player);

        protected abstract void RPC_MobCommand(long sender, ZDOID playerId, string command);

        public abstract void GotShoutedAtBy(MobAIBase mob);

        public virtual void UpdateAI(float dt)
        {
        }

        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        public void StopMoving()
        {
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

        protected Player GetPlayer(ZDOID characterID)
        {
            GameObject gameObject = ZNetScene.instance.FindInstance(characterID);
            if ((bool)gameObject)
            {
                return gameObject.GetComponent<Player>();
            }
            return null;
        }

        public static bool PrintAIStateToDebug { get; set; } = CommonConfig.PrintAIStatusMessageToDebug.Value;

        public static string UpdateAiStatus(ZNetView nview, string newStatus)
        {
            newStatus = Localization.instance.Localize(newStatus);
            string currentAiStatus = nview?.GetZDO()?.GetString(Constants.Z_AiStatus);
            if (currentAiStatus != newStatus)
            {
                nview.GetZDO().Set(Constants.Z_AiStatus, newStatus);
                if (PrintAIStateToDebug)
                {
                    string name = nview?.GetZDO()?.GetString(Constants.Z_GivenName);
                    Debug.Log($"{name}: {newStatus}");
                }
            }
            return newStatus;
        }
    }
}
