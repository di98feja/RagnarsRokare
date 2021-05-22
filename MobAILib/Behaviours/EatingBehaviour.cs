using Stateless;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class EatingBehaviour : IBehaviour
    {
        private const string Prefix = "RR_EAT";

        private class State
        {
            public const string Hungry = Prefix + "Hungry";
            public const string SearchForFood = Prefix + "SearchForFood";
            public const string HaveFoodItem = Prefix + "HaveFoodItem";
            public const string HaveNoFoodItem = Prefix + "HaveNoFoodItem";
        }

        private class Trigger
        {
            public const string ConsumeItem = Prefix + "ConsumeItem";
            public const string ItemFound = Prefix + "ItemFound";
            public const string Update = Prefix + "Update";
            public const string ItemNotFound = Prefix + "ItemNotFound";
            public const string SearchForItems = Prefix + "SearchForItems";
        }

        public Vector3 LastKnownFoodPosition 
        {
            get
            {
                if (m_aiBase?.NView?.IsValid() ?? false)
                {
                    return m_aiBase.NView.GetZDO().GetVec3(Constants.Z_SavedFoodPosition, m_aiBase.Character.transform.position);
                }
                return m_aiBase.Character.transform.position;
            }
            set
            {
                if (m_aiBase?.NView?.IsValid() ?? false)
                {
                    m_aiBase.NView.GetZDO().Set(Constants.Z_SavedFoodPosition, value);
                }
            }
        }

        private float m_hungryTimer;
        private float m_foodsearchtimer;
        private MobAIBase m_aiBase;

        private StateMachine<string, string>.TriggerWithParameters<float> UpdateTrigger;
        private StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;

        // Settings
        public string SuccessState;
        public string FailState;
        public float HealPercentageOnConsume;
        public string SearchForItemsState;
        public string StartState { get { return State.Hungry; } }
        public float HungryTimeout { get; set; } = 500;
        public float HurtHungryTimeout { get; set; } = 10;

        public bool IsHungry(bool isHurt)
        {
            return m_hungryTimer > (isHurt ? HurtHungryTimeout : HungryTimeout);
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            m_foodsearchtimer = 0f;
            if (LastKnownFoodPosition == Vector3.zero)
            {
                LastKnownFoodPosition = aiBase.Character.transform.position;
            }

            UpdateTrigger = brain.SetTriggerParameters<float>(Trigger.Update);
            LookForItemTrigger = brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>(Trigger.ItemFound);

            brain.Configure(State.Hungry)
                .SubstateOf(parentState)
                .PermitIf(UpdateTrigger, State.SearchForFood, (dt) => (m_foodsearchtimer += dt) > 10)
                .OnEntry(t =>
                {
                    aiBase.StopMoving();
                    aiBase.UpdateAiStatus(State.Hungry);
                })
                .OnExit(t =>
                {
                    Debug.LogWarning("Exiting EatingBehaviour");
                });

            brain.Configure(State.SearchForFood)
                .SubstateOf(State.Hungry)
                .PermitDynamic(LookForItemTrigger.Trigger, () => SearchForItemsState)
                .OnEntry(t =>
                {
                    m_foodsearchtimer = 0f;
                    brain.Fire(LookForItemTrigger, (aiBase.Instance as MonsterAI).m_consumeItems.Select(i => i.m_itemData), State.HaveFoodItem, State.HaveNoFoodItem);
                });

            brain.Configure(State.HaveFoodItem)
                .SubstateOf(State.Hungry)
                .PermitDynamic(Trigger.ConsumeItem, () => SuccessState)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.HaveFoodItem);
                    (aiBase.Instance as MonsterAI).m_onConsumedItem((aiBase.Instance as MonsterAI).m_consumeItems.FirstOrDefault());
                    (aiBase.Instance.GetComponent<Character>() as Humanoid).m_consumeItemEffects.Create(aiBase.Instance.transform.position, Quaternion.identity);
                    var animator = aiBase.Instance.GetType().GetField("m_animator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(aiBase.Instance) as ZSyncAnimation;
                    animator.SetTrigger("consume");
                    float consumeHeal = aiBase.Character.GetMaxHealth() * HealPercentageOnConsume;
                    Common.Dbgl($"consumeHeal:{consumeHeal}");

                    if (consumeHeal > 0f)
                    {
                        aiBase.Instance.GetComponent<Character>().Heal(consumeHeal);
                    }
                    m_hungryTimer = 0f;
                    LastKnownFoodPosition = aiBase.Character.transform.position;
                    brain.Fire(Trigger.ConsumeItem);
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.HaveNoFoodItem)
                .SubstateOf(State.Hungry)
                .PermitIf(Trigger.ItemNotFound, State.Hungry)
                .OnEntry(t =>
                {
                    brain.Fire(Trigger.ItemNotFound);
                });

        }

        public void Update(MobAIBase instance, float dt)
        {
            m_hungryTimer += dt;
            if (instance.Brain.State == State.Hungry)
            {
                Common.Invoke<BaseAI>(instance.Instance, "RandomMovement", dt, LastKnownFoodPosition);
                instance.Brain.Fire(UpdateTrigger, dt);
            }
        }
    }
}
