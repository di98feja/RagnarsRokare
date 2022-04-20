using Stateless;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace RagnarsRokare.MobAI
{
    public class DynamicEatingBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_EAT";

        private StateDef State { get; set; }
        private sealed class StateDef
        {
            private readonly string prefix;

            public string Hungry { get { return $"{prefix}Hungry"; } }
            public string SearchForFood { get { return $"{prefix}SearchForFood"; } }
            public string HaveFoodItem { get { return $"{prefix}HaveFoodItem"; } }
            public string HaveNoFoodItem { get { return $"{prefix}HaveNoFoodItem"; } }

            public StateDef(string prefix)
            {
                this.prefix = prefix;
            }
        }

        private TriggerDef Trigger { get; set; }
        private sealed class TriggerDef
        {
            private readonly string prefix;

            public string ConsumeItem { get { return $"{prefix}ConsumeItem"; } }
            public string ItemFound { get { return $"{prefix}ItemFound"; } }
            public string Update { get { return $"{prefix}Update"; } }
            public string ItemNotFound { get { return $"{prefix}ItemNotFound"; } }
            public string SearchForItems { get { return $"{prefix}SearchForItems"; } }
            public string Abort { get { return $"{prefix}Abort"; } }
            public TriggerDef(string prefix)
            {
                this.prefix = prefix;
            }

        }

        SearchForItemsBehaviour m_searchForItemsBehaviour;

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
        public float HealPercentageOnConsume { get; set; }
        public string StartState { get { return State.Hungry; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float HungryTimeout { get; set; } = 1000;
        public float HurtHungryTimeout { get; set; } = 10;
        public int FailedToFindFood { get; set; } = 0;

        public void Abort()
        {
            m_aiBase.Brain.Fire(Trigger.Abort);
        }

        public bool IsHungry(bool isHurt)
        {
            //Debug.Log($"Time {Time.time}, {m_aiBase.Character.GetHoverName()}, IsHungry:{m_hungryTimer > (isHurt ? HurtHungryTimeout : HungryTimeout)} isHurt:{isHurt}, m_hungryTimer{m_hungryTimer}");
            return m_hungryTimer > (isHurt ? HurtHungryTimeout : HungryTimeout);
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);

            m_aiBase = aiBase;
            m_foodsearchtimer = 0f;
            if (LastKnownFoodPosition == Vector3.zero)
            {
                LastKnownFoodPosition = aiBase.Character.transform.position;
            }
            m_searchForItemsBehaviour = new SearchForItemsBehaviour();
            m_searchForItemsBehaviour.Postfix = Prefix;
            m_searchForItemsBehaviour.IncludePickables = false;
            m_searchForItemsBehaviour.SuccessState = State.HaveFoodItem;
            m_searchForItemsBehaviour.FailState = State.HaveNoFoodItem;
            m_searchForItemsBehaviour.CenterPoint = aiBase.NView.transform.position;
            m_searchForItemsBehaviour.Items = (m_aiBase.Instance as MonsterAI).m_consumeItems.Select(i => i.m_itemData) as IEnumerable<ItemDrop.ItemData>;
            m_searchForItemsBehaviour.Configure(aiBase, brain, State.SearchForFood);

            UpdateTrigger = brain.SetTriggerParameters<float>(Trigger.Update);
            LookForItemTrigger = brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>(Trigger.ItemFound);

            brain.Configure(State.Hungry)
                .SubstateOf(parentState)
                .PermitIf(UpdateTrigger, State.SearchForFood, (dt) => (m_foodsearchtimer += dt) > 10)
                .OnEntry(t =>
                {
                    aiBase.StopMoving();
                    Debug.Log($"Time {Time.time}, {m_aiBase.Character.GetHoverName()}, m_hungryTimer{m_hungryTimer}");
                    aiBase.UpdateAiStatus(State.Hungry);
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.SearchForFood)
                .SubstateOf(State.Hungry)
                .PermitDynamic(LookForItemTrigger.Trigger, () => m_searchForItemsBehaviour.StartState)
                .OnEntry(t =>
                {
                    m_searchForItemsBehaviour.KnownContainers = m_aiBase.KnownContainers;
                    m_foodsearchtimer = 0f;
                    Debug.Log($"{aiBase.Character.GetHoverName()}Searching for consumeItems{(aiBase.Instance as MonsterAI).m_consumeItems.Count}");
                    //Debug.Log($"{aiBase.Character.GetHoverName()}: {string.Join(",", (aiBase.Instance as MonsterAI).m_consumeItems.Select(c => c?.name ?? "null"))}");
                    brain.Fire(LookForItemTrigger, (aiBase.Instance as MonsterAI).m_consumeItems.Select(i => i.m_itemData), State.HaveFoodItem, State.HaveNoFoodItem);
                });

            brain.Configure(State.HaveFoodItem)
                .PermitDynamic(Trigger.ConsumeItem, () => SuccessState)
                .OnEntry(t =>
                {
                    aiBase.UpdateAiStatus(State.HaveFoodItem);
                    (aiBase.Instance as MonsterAI).m_onConsumedItem((aiBase.Instance as MonsterAI).m_consumeItems.FirstOrDefault());
                    (aiBase.Instance.GetComponent<Character>() as Humanoid).m_consumeItemEffects.Create(aiBase.Instance.transform.position, Quaternion.identity);
                    var animator = aiBase.Instance.GetType().GetField("m_animator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(aiBase.Instance) as ZSyncAnimation;
                    animator.SetTrigger("consume");
                    float consumeHeal = aiBase.Character.GetMaxHealth() * HealPercentageOnConsume;
                    Common.Dbgl($"Time {Time.time},consumeHeal:{consumeHeal}", true);

                    if (consumeHeal > 0f)
                    {
                        aiBase.Instance.GetComponent<Character>().Heal(consumeHeal);
                    }
                    m_hungryTimer = 0f;
                    HungryTimeout = 1000;
                    FailedToFindFood = 0;
                    aiBase.HungerLevel = FailedToFindFood;
                    LastKnownFoodPosition = aiBase.Character.transform.position;
                    brain.Fire(Trigger.ConsumeItem);
                })
                .OnExit(t =>
                {
                    Debug.Log($"Time {Time.time},{m_aiBase.Character.GetHoverName()}: Exit EatingBehaviour via to {SuccessState}");
                });

            brain.Configure(State.HaveNoFoodItem)
                .SubstateOf(State.Hungry)
                .PermitIf(Trigger.ItemNotFound, State.Hungry)
                .OnEntry(t =>
                {
                    FailedToFindFood += 1;
                    aiBase.HungerLevel = FailedToFindFood;
                    brain.Fire(Trigger.ItemNotFound);
                });

        }

        public void Update(MobAIBase instance, float dt)
        {
            m_hungryTimer += dt;

            if (instance.Brain.IsInState(m_searchForItemsBehaviour.StartState))
            {
                m_searchForItemsBehaviour.Update(instance, dt);
            }

            if (instance.Brain.IsInState(State.Hungry))
            {
                Utils.Invoke<BaseAI>(instance.Instance, "RandomMovement", dt, LastKnownFoodPosition);
                instance.Brain.Fire(UpdateTrigger, dt);
            }
        }
    }
}
