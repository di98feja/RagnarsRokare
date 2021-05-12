using Stateless;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI.Mobs
{
    public class ExampleAIConfig
    {
        public int InteractRange = 1;
    }

    /// <summary>
    /// Each MobAI must inherit the MobAIBase class.
    /// It wraps the Valheim original AI class that is accessible via the Instance property.
    /// </summary>
    public class ExampleAI : MobAIBase, IMobAIType
    {
        /// <summary>
        /// This is the configfile sent in when registering a mob to use this MobAI
        /// </summary>
        private ExampleAIConfig m_config;
        
        /// <summary>
        /// This is an item that the mob spotted on the ground
        /// </summary>
        private ItemDrop m_groundItem;

        /// <summary>
        /// Timer for how often we should update the state machine
        /// </summary>
        private float m_updateTimer;

        /// <summary>
        /// Timer for how long before mob gets bored
        /// </summary>
        private float m_boredTimer;

        /// <summary>
        /// This method is defined by the IMobAIType interface and must be implemented.
        /// It is used by the MobManager to identify the MobAI type.
        /// </summary>
        /// <returns>Info about this MobAI</returns>
        public MobAIInfo GetMobAIInfo()
        {
            return new MobAIInfo
            {
                AIType = typeof(ExampleAI),
                ConfigType = typeof(ExampleAIConfig),
                Name = nameof(ExampleAI)
            };
        }

        class State
        {
            public const string Idle = "Idle";
            public const string FindItem = "FindItem";
            public const string MoveToItem = "MoveToItem";
            public const string PickupItem = "PickupItem";
        }

        class Trigger
        {
            public const string Bored = "Bored";
            public const string Happy = "Happy";
            public const string FoundItem = "FoundItem";
            public const string Failed = "Failed";
            public const string Update = "Update";
        }

        /// <summary>
        /// Custom trigger that takes a parameter
        /// </summary>
        StateMachine<string, string>.TriggerWithParameters<float> UpdateTrigger;

        /// <summary>
        /// Each MobAI must have an empty default constructor that call the base default constructor
        /// so it can be instanciated by the MobManager to read the MobAIInfo during init.
        /// </summary>
        public ExampleAI() : base()
        { }

        /// <summary>
        /// This is the constructor used by the MobManager to create the MobAI when it first gets used in the 
        /// original MonsterAI.Update method. 
        /// The base method must be called with the MonsterAI object and a string describing what AI-state should be 
        /// starting state of this mob.
        /// </summary>
        /// <param name="instance">The original MonsterAI object</param>
        /// <param name="config">The config file sent in when registering the current mob</param>
        public ExampleAI(MonsterAI instance, ExampleAIConfig config) : base(instance, State.Idle)
        {
            Debug.Log("Example Config");
            m_config = config;

            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            // Init custom Update trigger
            UpdateTrigger = Brain.SetTriggerParameters<float>(Trigger.Update);

            // Init Idle state
            Brain.Configure(State.Idle)
                .Permit(Trigger.Bored, State.FindItem)
                .OnEntry(t =>
                {
                    UpdateAiStatus("Just hanging around..");
                });

            // Init FindItem state
            Brain.Configure(State.FindItem)
                // if the FoundItem trigger is fired, transit to MoveToItem state
                .Permit(Trigger.FoundItem, State.MoveToItem)
                // if the Failed trigger is fire, transit to Idle state
                .Permit(Trigger.Failed, State.Idle)
                .OnEntry(t =>
                {
                    // Check for nearby items on the ground
                    m_groundItem = Common.GetNearbyItem(Instance, ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "").Select(i => i.m_itemData), 10);
                    if (m_groundItem != null)
                    {
                        // Item found, trigger FoundItem
                        Brain.Fire(Trigger.FoundItem);
                        return;
                    }
                    else
                    {
                        // No item seen, trigger Failed
                        UpdateAiStatus($"No items to be seen");
                        Brain.Fire(Trigger.Failed);
                    }
                });

            // Init MoveToItem state
            Brain.Configure(State.MoveToItem)
                // Only permit transition to Idle state if MoveAndAvoidFire return true and
                // that only happens if mob is closer then 0.5 from target
                .PermitIf(UpdateTrigger, State.Idle, (dt) => MoveAndAvoidFire(m_groundItem.transform.position, dt, 0.5f))
                // if the Failed trigger is fired, transit to Idle state
                .Permit(Trigger.Failed, State.Idle)
                .OnEntry(t =>
                {
                    UpdateAiStatus($"Moving to {m_groundItem.m_itemData.m_shared.m_name}");
                });
        }

        /// <summary>
        /// This update gets called from the original UpdateAI method in MonsterAI
        /// </summary>
        /// <param name="dt">delta time since last call</param>
        public override void UpdateAI(float dt)
        {
            // Always call base method first
            base.UpdateAI(dt);

            // Limit refresh rate to 20 Hz
            if ((m_updateTimer += dt) < 0.05f) return;
            m_updateTimer = 0.0f;

            if (Brain.State == State.Idle && (m_boredTimer += dt) > Random.Range(3.0f, 6.0f))
            {
                m_boredTimer = 0.0f;
                Brain.Fire(Trigger.Bored);
            }

            // Check if we should run triggers for MoveToItem state
            if (Brain.State == State.MoveToItem)
            {
                // Check that the item is still there
                if (m_groundItem == null)
                {
                    // Item is gone, stop moving and trigger Fail
                    StopMoving();
                    Brain.Fire(Trigger.Failed);
                }
                else
                {
                    // Update MoveToItem state
                    Brain.Fire(UpdateTrigger, dt);
                }
            }
        }

        /// <summary>
        /// Set this mob to follow the given Player
        /// </summary>
        /// <param name="player">Player to follow</param>
        public override void Follow(Player player)
        {
            // Not implemented in this example
        }

        /// <summary>
        /// Another mob shouted at us!
        /// </summary>
        /// <param name="mob">the offender</param>
        public override void GotShoutedAtBy(MobAIBase mob)
        {
            Instance.m_alertedEffects.Create(Instance.transform.position, Quaternion.identity);
        }

        /// <summary>
        /// General command pattern.
        /// Used to send a command via the ZNetView
        /// </summary>
        /// <param name="sender">Sender Id</param>
        /// <param name="playerId">Player that initiated the command</param>
        /// <param name="command">Command Id</param>
        protected override void RPC_MobCommand(long sender, ZDOID playerId, string command)
        {
        }
    }
}
