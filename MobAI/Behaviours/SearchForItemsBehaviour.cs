using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    class SearchForItemsBehaviour : IBehaviour
    {
        private const string Prefix = "RR_SFI";

        private class State
        {
            public const string Main = Prefix + "Main";
            public const string SearchItemsOnGround = Prefix + "SearchItemsOnGround";
            public const string MoveToGroundItem = Prefix + "MoveToGroundItem";
            public const string SearchForRandomContainer = Prefix + "SearchForRandomContainer";
            public const string MoveToContainer = Prefix + "MoveToContainer";
            public const string OpenContainer = Prefix + "OpenContainer";
            public const string SearchForItem = Prefix + "SearchForItem";
            public const string PickUpItemFromGround = Prefix + "PickUpItemFromGround";
            public const string AvoidFire = Prefix + "AvoidFire";
        }

        private class Trigger
        {
            public const string ItemFound = Prefix + "ItemFound";
            public const string ContainerFound = Prefix + "ContainerFound";
            public const string ContainerNotFound = Prefix + "ContainerNotFound";
            public const string ContainerIsClose = Prefix + "ContainerIsClose";
            public const string Failed = Prefix + "Failed";
            public const string ContainerOpened = Prefix + "ContainerOpened";
            public const string Timeout = Prefix + "Timeout";
            public const string GroundItemIsClose = Prefix + "GroundItemIsClose";
            public const string FoundGroundItem = Prefix + "FoundGroundItem";
        }

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
        public string InitState { get { return State.Main; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }

        private ItemDrop m_groundItem;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTime;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            FoundGroundItemTrigger = brain.SetTriggerParameters<ItemDrop>(Trigger.FoundGroundItem);

            brain.Configure(State.Main)
                .InitialTransition(State.SearchItemsOnGround)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Timeout, () => FailState)
                .OnEntry(t =>
                {
                    //Debug.Log("Entered SearchForItemsBehaviour");
                })
                .OnExit(t =>
                {
                    KnownContainers.Peek()?.SetInUse(inUse: false);
                });

            brain.Configure(State.SearchItemsOnGround)
                .SubstateOf(State.Main)
                .Permit(FoundGroundItemTrigger.Trigger, State.MoveToGroundItem)
                .Permit(Trigger.Failed, State.SearchForRandomContainer)
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
                    brain.Fire(Trigger.Failed);
                });

            brain.Configure(State.SearchForRandomContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerFound, State.MoveToContainer)
                .PermitDynamic(Trigger.ContainerNotFound, () => FailState)
                .PermitDynamic(Trigger.Failed, () => FailState)
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
                            brain.Fire(Trigger.ContainerFound);
                            return;
                        }
                    }
                    
                    Container nearbyChest = Common.FindRandomNearbyContainer(m_aiBase.Instance.transform.position, KnownContainers, AcceptedContainerNames);
                    if (nearbyChest != null)
                    {
                        KnownContainers.Push(nearbyChest);
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Look a bin!");
                        m_aiBase.Brain.Fire(Trigger.ContainerFound);
                    }
                    else
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Me give up, nottin found!");
                        KnownContainers.Clear();
                        m_aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    }
                });

            brain.Configure(State.MoveToGroundItem)
                .SubstateOf(State.Main)
                .Permit(Trigger.GroundItemIsClose, State.PickUpItemFromGround)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    m_groundItem = t.Parameters[0] as ItemDrop;
                    if (m_groundItem == null || Common.GetNView(m_groundItem)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed);
                        return;
                    }
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Heading to {m_groundItem.m_itemData.m_shared.m_name}");
                });

            brain.Configure(State.PickUpItemFromGround)
                .SubstateOf(State.Main)
                .PermitDynamic(Trigger.ItemFound, () => SuccessState)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    FoundItem = m_groundItem.m_itemData;
                    if (m_groundItem == null || Common.GetNView(m_groundItem)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed);
                        return;
                    }
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Got a {FoundItem.m_shared.m_name} from the ground");
                    if (m_groundItem.RemoveOne())
                    {
                        brain.Fire(Trigger.ItemFound);
                    }
                    else
                    {
                        brain.Fire(Trigger.Failed);
                    }
                });

            brain.Configure(State.MoveToContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerIsClose, State.OpenContainer)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .PermitDynamic(Trigger.ContainerNotFound, () => FailState)
                .OnEntry(t =>
                {
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Heading to that a bin");
                });

            brain.Configure(State.OpenContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.SearchForItem)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    if (KnownContainers.Peek() == null || Common.GetNView(KnownContainers.Peek())?.IsValid() != true || KnownContainers.Peek().IsInUse())
                    {
                        KnownContainers.Pop();
                        brain.Fire(Trigger.Failed);
                    }
                    else
                    {
                        KnownContainers.Peek().SetInUse(inUse: true);
                        m_openChestTimer = 0f;
                    }
                });

            brain.Configure(State.SearchForItem)
                .SubstateOf(State.Main)
                .PermitDynamic(Trigger.ItemFound, () => SuccessState)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    if (KnownContainers.Peek() == null || Common.GetNView(KnownContainers.Peek())?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed);
                        return;
                    }
                    FoundItem = KnownContainers.Peek().GetInventory().GetAllItems().Where(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name)).RandomOrDefault();
                    if (FoundItem != null)
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Found {FoundItem.m_shared.m_name} in this a bin!");
                        KnownContainers.Peek().GetInventory().RemoveItem(FoundItem, 1);
                        Common.Invoke<Container>(KnownContainers.Peek(), "Save");
                        Common.Invoke<Inventory>(KnownContainers.Peek().GetInventory(), "Changed");

                        brain.Fire(Trigger.ItemFound);
                    }
                    else
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Nottin in this a bin..");
                        brain.Fire(Trigger.Failed);
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
                aiBase.Brain.Fire(Trigger.Timeout);
            }

            if (aiBase.Brain.IsInState(State.MoveToContainer))
            {
                if (KnownContainers.Peek() == null || KnownContainers.Peek()?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    aiBase.StopMoving();
                    KnownContainers.Pop();
                    aiBase.Brain.Fire(Trigger.Failed);
                    return;
                }
                aiBase.MoveAndAvoidFire(KnownContainers.Peek().transform.position, dt, 0.5f);
                if (Vector3.Distance(aiBase.Instance.transform.position, KnownContainers.Peek().transform.position) < 1.5)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerIsClose);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToGroundItem))
            {
                if (m_groundItem == null || m_groundItem?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_groundItem = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Failed);
                    return;
                }
                aiBase.MoveAndAvoidFire(m_groundItem.transform.position, dt, 0.5f);
                if (Vector3.Distance(aiBase.Instance.transform.position, m_groundItem.transform.position) < 1.5)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.GroundItemIsClose);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.OpenContainer))
            {
                if ((m_openChestTimer += dt) > OpenChestDelay)
                {
                    aiBase.Brain.Fire(Trigger.ContainerOpened);
                }
            }
        }
    }
}
