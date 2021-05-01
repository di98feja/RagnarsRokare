using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    class SearchForItemsBehaviour : IBehaviour
    {
        private const string Prefix = "RR_SFI";

        private const string Main_state = Prefix + "Main";
        private const string SearchItemsOnGround_state = Prefix + "SearchItemsOnGround";
        private const string MoveToGroundItem_state = Prefix + "MoveToGroundItem"; 
        private const string SearchForRandomContainer_state = Prefix + "SearchForRandomContainer";
        private const string MoveToContainer_state = Prefix + "MoveToContainer";
        private const string OpenContainer_state = Prefix + "OpenContainer";
        private const string SearchForItem_state = Prefix + "SearchForItem"; 
        private const string PickUpItemFromGround_state = Prefix + "PickUpItemFromGround";
        private const string AvoidFire_state = Prefix + "AvoidFire";

        private const string ItemFound_trigger = Prefix + "ItemFound";
        private const string ContainerFound_trigger = Prefix + "ContainerFound";
        private const string ContainerNotFound_trigger = Prefix + "ContainerNotFound";
        private const string ContainerIsClose_trigger = Prefix + "ContainerIsClose";
        private const string Failed_trigger = Prefix + "Failed";
        private const string ContainerOpened_trigger = Prefix + "ContainerOpened";
        private const string Timeout_trigger = Prefix + "Timeout";
        private const string GroundItemIsClose_trigger = Prefix + "GroundItemIsClose";
        private const string FoundGroundItem_Trigger = Prefix + "FoundGroundItem";

        StateMachine<string, string>.TriggerWithParameters<ItemDrop> FoundGroundItemTrigger;

        // Input
        public IEnumerable<ItemDrop.ItemData> Items { get; set; }
        public MaxStack<Container> KnownContainers { get; set; }
        public string[] AcceptedContainerNames { get; set; }

        // Output
        public ItemDrop.ItemData FoundItem { get; private set; }

        // Settings
        public float OpenChestDelay { get; private set; } = 1;
        public float MaxSearchTime { get; set; } = 60;
        public string InitState { get { return Main_state; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }

        private ItemDrop m_groundItem;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTime;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            FoundGroundItemTrigger = brain.SetTriggerParameters<ItemDrop>(FoundGroundItem_Trigger);

            brain.Configure(Main_state)
                .InitialTransition(SearchItemsOnGround_state)
                .SubstateOf(parentState)
                .PermitDynamic(Timeout_trigger, () => FailState)
                .OnEntry(t =>
                {
                    //Debug.Log("Entered SearchForItemsBehaviour");
                });

            brain.Configure(SearchItemsOnGround_state)
                .SubstateOf(Main_state)
                .Permit(FoundGroundItemTrigger.Trigger, MoveToGroundItem_state)
                .Permit(Failed_trigger, SearchForRandomContainer_state)
                .OnEntry(t =>
                {
                    ItemDrop groundItem = Common.GetNearbyItem(m_aiBase.Instance.transform.position, Items, GreylingsConfig.ItemSearchRadius.Value);
                    if (groundItem != null)
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Look, there is a {groundItem.m_itemData.m_shared.m_name} on da grund");
                        brain.Fire(FoundGroundItemTrigger, groundItem);
                        return;
                    }
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"I seen nottin on da ground.");
                    brain.Fire(Failed_trigger);
                });

            brain.Configure(SearchForRandomContainer_state)
                .SubstateOf(Main_state)
                .Permit(ContainerFound_trigger, MoveToContainer_state)
                .PermitDynamic(ContainerNotFound_trigger, () => FailState)
                .PermitDynamic(Failed_trigger, () => FailState)
                .OnEntry(t =>
                {
                    if (KnownContainers.Any())
                    {
                        var matchingContainer = KnownContainers.Where(c => c.GetInventory().GetAllItems().Any(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name))).RandomOrDefault();
                        if (matchingContainer != null)
                        {
                            KnownContainers.Remove(matchingContainer);
                            KnownContainers.Push(matchingContainer);
                            MobAIBase.UpdateAiStatus(m_aiBase.NView, $"I seen this in that a bin");
                            brain.Fire(ContainerFound_trigger);
                            return;
                        }
                    }
                    
                    Container nearbyChest = Common.FindRandomNearbyContainer(m_aiBase.Instance.transform.position, KnownContainers, AcceptedContainerNames);
                    if (nearbyChest != null)
                    {
                        KnownContainers.Push(nearbyChest);
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Look a bin!");
                        m_aiBase.Brain.Fire(ContainerFound_trigger);
                    }
                    else
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Me give up, nottin found!");
                        KnownContainers.Clear();
                        m_aiBase.Brain.Fire(ContainerNotFound_trigger);
                    }
                });

            brain.Configure(MoveToGroundItem_state)
                .SubstateOf(Main_state)
                .Permit(GroundItemIsClose_trigger, PickUpItemFromGround_state)
                .Permit(Failed_trigger, SearchItemsOnGround_state)
                .OnEntry(t =>
                {
                    m_groundItem = t.Parameters[0] as ItemDrop;
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Heading to {m_groundItem.m_itemData.m_shared.m_name}");
                });

            brain.Configure(PickUpItemFromGround_state)
                .SubstateOf(Main_state)
                .PermitDynamic(ItemFound_trigger, () => SuccessState)
                .Permit(Failed_trigger, SearchItemsOnGround_state)
                .OnEntry(t =>
                {
                    FoundItem = m_groundItem.m_itemData;
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Got a {FoundItem.m_shared.m_name} from the ground");
                    if (m_groundItem.RemoveOne())
                    {
                        brain.Fire(ItemFound_trigger);
                    }
                    else
                    {
                        brain.Fire(Failed_trigger);
                    }
                });

            brain.Configure(MoveToContainer_state)
                .SubstateOf(Main_state)
                .Permit(ContainerIsClose_trigger, OpenContainer_state)
                .Permit(Failed_trigger, SearchItemsOnGround_state)
                .PermitDynamic(ContainerNotFound_trigger, () => FailState)
                .OnEntry(t =>
                {
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Heading to that a bin");
                });

            brain.Configure(OpenContainer_state)
                .SubstateOf(Main_state)
                .Permit(ContainerOpened_trigger, SearchForItem_state)
                .Permit(Failed_trigger, SearchItemsOnGround_state)
                .OnEntry(t =>
                {
                    if (KnownContainers.Peek().IsInUse())
                    {
                        KnownContainers.Pop();
                        brain.Fire(Failed_trigger);
                    }
                    else
                    {
                        KnownContainers.Peek().SetInUse(inUse: true);
                        m_openChestTimer = 0f;
                    }
                });

            brain.Configure(SearchForItem_state)
                .SubstateOf(Main_state)
                .PermitDynamic(ItemFound_trigger, () => SuccessState)
                .Permit(Failed_trigger, SearchItemsOnGround_state)
                .OnEntry(t =>
                {
                    FoundItem = KnownContainers.Peek().GetInventory().GetAllItems().Where(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name)).RandomOrDefault();
                    if (FoundItem != null)
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Found {FoundItem.m_shared.m_name} in this a bin!");
                        KnownContainers.Peek().GetInventory().RemoveItem(FoundItem, 1);
                        Common.Invoke<Container>(KnownContainers.Peek(), "Save");
                        Common.Invoke<Inventory>(KnownContainers.Peek().GetInventory(), "Changed");

                        brain.Fire(ItemFound_trigger);
                    }
                    else
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Nottin in this a bin..");
                        brain.Fire(Failed_trigger);
                    }
                })
                .OnExit(t =>
                {
                    KnownContainers.Peek().SetInUse(inUse: false);
                });
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if ((m_currentSearchTime += dt) > MaxSearchTime)
            {
                m_currentSearchTime = 0f;
                aiBase.Brain.Fire(Timeout_trigger);
            }

            if (aiBase.Brain.IsInState(MoveToContainer_state))
            {
                bool containerIsInvalid = KnownContainers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
                if (containerIsInvalid)
                {
                    aiBase.StopMoving();
                    KnownContainers.Pop();
                    aiBase.Brain.Fire(Failed_trigger);
                    return;
                }
                aiBase.MoveAndAvoidFire(KnownContainers.Peek().transform.position, dt, 0.5f);
                if (Vector3.Distance(aiBase.Instance.transform.position, KnownContainers.Peek().transform.position) < 1.5)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(ContainerIsClose_trigger);
                }
                return;
            }

            if (aiBase.Brain.IsInState(MoveToGroundItem_state))
            {
                if (m_groundItem?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_groundItem = null;
                    aiBase.Brain.Fire(Failed_trigger);
                    aiBase.StopMoving();
                    return;
                }
                aiBase.MoveAndAvoidFire(m_groundItem.transform.position, dt, 0.5f);
                if (Vector3.Distance(aiBase.Instance.transform.position, m_groundItem.transform.position) < 1.5)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(GroundItemIsClose_trigger);
                }
                return;
            }

            if (aiBase.Brain.IsInState(OpenContainer_state))
            {
                if ((m_openChestTimer += dt) > OpenChestDelay)
                {
                    aiBase.Brain.Fire(ContainerOpened_trigger);
                }
            }

        }
    }
}
