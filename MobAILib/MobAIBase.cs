using Stateless;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;

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
        }

        public ZDOID ZDOId { get; set; }

        public bool HasInstance()
        {
            return m_instance != null;
        }

        public StateMachine<string, string> Brain;

        public string learningTask;
        public int learningRate = 0;
        public List<string> m_trainedAssignments = new List<string>();
        private Func<MobAIBase, Type> m_fightingBehaviourSelector;

        public string CurrentAIState { get; protected set; }

        public MobAIBase()
        { }

        public MobAIBase(BaseAI instance, string initState, MobAIBaseConfig config)
        {
            m_instance = instance;
            ZDOId = NView.GetZDO().m_uid;
            Config = config;
            Brain = new StateMachine<string, string>(() => CurrentAIState, s => CurrentAIState = s);
            Brain.OnUnhandledTrigger((state, trigger) => { });
            CurrentAIState = initState;
            if (NView.IsValid())
            {
                NView.Unregister(Constants.Z_MobCommand);
                NView.Register<ZDOID, string>(Constants.Z_MobCommand, RPC_MobCommand);
            }
            m_trainedAssignments.AddRange(NView.GetZDO().GetString(Constants.Z_trainedAssignments).Split(new char[] { ' ', ',' }).Where(a => !string.IsNullOrEmpty(a)));

        }

        #region Config
        private MobAIBaseConfig Config { get; set; }
        public int Awareness { get { return Config.Awareness; } }
        public int Agressiveness { get { return Config.Agressiveness; } }
        public int Mobility { get { return Config.Mobility; } }
        public int Intelligence { get { return Config.Intelligence; } }
        public bool CanWorkAssignment(string assignmentName)
        {
            return Config.WorkableAssignments?.Contains(assignmentName) ?? false;
        }
        #endregion

        public Character Character
        {
            get
            {
                return Instance.GetType().GetField("m_character", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Instance) as Character;
            }
        }

        public Tameable Tameable
        {
            get
            {
                return Instance.GetType().GetField("m_tamable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Instance) as Tameable;
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
                return NView.GetZDO().GetString(Constants.Z_UniqueId);
            }
        }

        public float TimeSinceHurt
        {
            get
            {
                return (float)Instance.GetType().GetField("m_timeSinceHurt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Instance);
            }
            set
            {
                Instance.GetType().GetField("m_timeSinceHurt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Instance, value);
            }
        }

        public Vector3 HomePosition
        {
            get
            {
                if (NView?.IsValid() ?? false)
                {
                    return NView.GetZDO().GetVec3(Constants.Z_SavedHomePosition, Character.transform.position);
                }
                return Character.transform.position;
            }
            set
            {
                if (NView?.IsValid() ?? false)
                {
                    NView.GetZDO().Set(Constants.Z_SavedHomePosition, value);
                }
            }
        }

        public Vector3 StartPosition
        {
            get
            {
                if (NView?.IsValid() ?? false)
                {
                    return NView.GetZDO().GetVec3(Constants.Z_SavedStartPosition, Character.transform.position);
                }
                return Character.transform.position;
            }
            set
            {
                if (NView?.IsValid() ?? false)
                {
                    NView.GetZDO().Set(Constants.Z_SavedStartPosition, value);
                }
            }
        }

        public bool IsHurt
        {
            get
            {
                return Character.GetHealthPercentage() <= 0.99f;
            }
        }

        public Character TargetCreature
        {
            get
            {
                return (Character)(Instance as MonsterAI).GetType().GetField("m_targetCreature", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Instance);
            }
            set
            {
                (Instance as MonsterAI).GetType().GetField("m_targetCreature", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Instance, value);
            }
        }

        private Type DefaultFightingBehaviour(MobAIBase m) { return typeof(FightBehaviour); }

        public Func<MobAIBase, Type> FightingBehaviourSelector 
        { 
            get => m_fightingBehaviourSelector ?? DefaultFightingBehaviour; 
            set => m_fightingBehaviourSelector = value; 
        }

        public Character Attacker { get; set; }

        public bool Alerted { get; set; }

        public abstract void Follow(Player player);

        public void GiveCommand(string command, params object[] commandData)
        {
            NView.InvokeRPC(ZNetView.Everybody, Constants.Z_MobCommand, commandData , command);
        }

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
            Instance.StopMoving();
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

        /// <summary>
        /// Move towards destination avoiding fire along the way
        /// </summary>
        /// <param name="destination">Where to</param>
        /// <param name="dt">delta time</param>
        /// <param name="distance">distance from destination to accept as "reached"</param>
        /// <param name="running">run or walk</param>
        /// <returns>True if character is closer than distance from destination, false otherwise</returns>
        public bool MoveAndAvoidFire(Vector3 destination, float dt, float distance, bool running = false)
        {
            if (AvoidFire(dt)) return false;

            var remainingDistance = Utils.DistanceXZ(Character.transform.position, destination);
            if (remainingDistance < distance) return true;

            running = remainingDistance > 5;
            var nearbyMobs = MobManager.AliveMobs.Values
                .Where(c => c.HasInstance())
                .Where(c => Vector3.Distance(c.Instance.transform.position, Instance.transform.position) < 1.0f)
                .Where(m => m.UniqueID != this.UniqueID);
            var findPath = (bool)Invoke<MonsterAI>(Instance, "FindPath", destination) && remainingDistance < 50;
            if (!nearbyMobs.Any() && findPath)
            {
                Invoke<MonsterAI>(Instance, "MoveTo", dt, destination, 0f, running);
            }
            else
            {
                Invoke<MonsterAI>(Instance, "MoveAndAvoid", dt, destination, 0f, running);
            }
            return false;
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

        public bool PrintAIStateToDebug { get; set; } = CommonConfig.PrintAIStatusMessageToDebug.Value;


        public string UpdateAiStatus(string newStatus)
        {
            return UpdateAiStatus(newStatus, null);
        }

        public string UpdateAiStatus(string newStatus, string arg = null)
        {
            if (Config.AIStateCustomStrings?.ContainsKey(newStatus) ?? false)
            {
                newStatus = string.Format(Config.AIStateCustomStrings[newStatus], arg ?? string.Empty);
            }
            newStatus = Localization.instance.Localize(newStatus);
            string currentAiStatus = NView?.GetZDO()?.GetString(Constants.Z_AiStatus);
            if (currentAiStatus != newStatus)
            {
                NView.GetZDO().Set(Constants.Z_AiStatus, newStatus);
                if (PrintAIStateToDebug)
                {
                    string name = Character.GetHoverName();
                    Debug.Log($"{name}: {newStatus}");
                }
            }
            return newStatus;
        }

        public int HungerLevel
        {
            get
            {
                if (int.TryParse(NView.GetZDO()?.GetString(Constants.Z_HungerLevel), out int result))
                {
                    return result;
                }
                return 0;
            }
            set
            {
                var zdo = NView.GetZDO();
                if (zdo?.IsOwner() ?? false)
                {
                    zdo.Set(Constants.Z_HungerLevel, value);
                }
            }
        }

        public MaxStack<Container> Containers { get; set; }

        public string[] AcceptedContainerNames { get; set; }

    }
}
