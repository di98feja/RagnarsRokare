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
            public const string SearchForPickable = Prefix + "SearchForPickable";
            public const string MoveToGroundItem = Prefix + "MoveToGroundItem";
            public const string SearchForRandomContainer = Prefix + "SearchForRandomContainer";
            public const string MoveToContainer = Prefix + "MoveToContainer";
            public const string OpenContainer = Prefix + "OpenContainer";
            public const string SearchForItem = Prefix + "SearchForItem";
            public const string PickUpItemFromGround = Prefix + "PickUpItemFromGround";
            public const string AvoidFire = Prefix + "AvoidFire";
            public const string MoveToPickable = Prefix + "MoveToPickable";
            public const string WaitingForPickable = Prefix + "WaitingForPickable";
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
            public const string FoundPickable = Prefix + "FoundPickable";
            public const string WaitForPickable = Prefix + "WaitForPickable";
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
        public string StartState { get { return State.Main; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }

        private ItemDrop m_groundItem;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTime;
        private int m_searchRadius;
        private Pickable m_pickable;
        private float m_pickableTimer;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            FoundGroundItemTrigger = brain.SetTriggerParameters<ItemDrop>(Trigger.FoundGroundItem);
            m_searchRadius = aiBase.Awareness * 5;



            brain.Configure(State.Main)
                .InitialTransition(State.SearchItemsOnGround)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Timeout, () => FailState)
                .OnEntry(t =>
                {
                    m_currentSearchTime = 0f;
                    Common.Dbgl("Entered SearchForItemsBehaviour");
                })
                .OnExit(t =>
                {
                    KnownContainers.Peek()?.SetInUse(inUse: false);
                    m_aiBase.UpdateAiStatus(string.Empty);
                });

            brain.Configure(State.SearchItemsOnGround)
                .SubstateOf(State.Main)
                .Permit(FoundGroundItemTrigger.Trigger, State.MoveToGroundItem)
                .Permit(Trigger.Failed, State.SearchForPickable)
                .OnEntry(t =>
                {
                    ItemDrop groundItem = Common.GetNearbyItem(m_aiBase.Instance, Items.Select(i => i.m_shared.m_name), m_searchRadius);
                    if (groundItem != null)
                    {
                        m_aiBase.UpdateAiStatus(State.SearchItemsOnGround, groundItem.m_itemData.m_shared.m_name);
                        brain.Fire(FoundGroundItemTrigger, groundItem);
                        return;
                    }
                    brain.Fire(Trigger.Failed);
                });

            brain.Configure(State.SearchForPickable)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundPickable, State.MoveToPickable)
                .Permit(Trigger.Failed, State.SearchForRandomContainer)
                .OnEntry(t =>
                {
                    Pickable pickable = Common.GetNearbyPickable(m_aiBase.Instance, m_aiBase.m_trainedAssignments, m_searchRadius, Items.Select(i => i?.m_shared?.m_name));
                    if ((bool)pickable)
                    {
                        m_pickable = pickable;
                        Common.Dbgl($"Found pickable: {m_pickable.GetHoverName()}");
                        aiBase.Brain.Fire(Trigger.FoundPickable);
                        return;
                    }
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
                            brain.Fire(Trigger.ContainerFound);
                            return;
                        }
                    }

                    Container nearbyChest = Common.FindRandomNearbyContainer(m_aiBase.Instance, KnownContainers, AcceptedContainerNames, m_searchRadius);
                    if (nearbyChest != null)
                    {
                        KnownContainers.Push(nearbyChest);
                        m_aiBase.UpdateAiStatus(State.SearchForRandomContainer);
                        m_aiBase.Brain.Fire(Trigger.ContainerFound);
                    }
                    else
                    {
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
                    m_aiBase.UpdateAiStatus(State.MoveToGroundItem, m_groundItem.m_itemData.m_shared.m_name);
                });

            brain.Configure(State.MoveToPickable)
                .SubstateOf(State.Main)
                .Permit(Trigger.WaitForPickable, State.WaitingForPickable)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    if (m_pickable == null || Common.GetNView(m_pickable)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed);
                        return;
                    }
                    m_aiBase.UpdateAiStatus(State.MoveToPickable, m_pickable.GetHoverName());
                    m_pickableTimer = Time.time + 0.7f;
                });

            brain.Configure(State.WaitingForPickable)
                .SubstateOf(State.Main)
                .Permit(Trigger.GroundItemIsClose, State.PickUpItemFromGround)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    m_pickableTimer = Time.time + 0.7f;
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
                    m_aiBase.UpdateAiStatus(State.PickUpItemFromGround, FoundItem.m_shared.m_name);
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
                    m_aiBase.UpdateAiStatus(State.MoveToContainer);
                });

            brain.Configure(State.OpenContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.SearchForItem)
                .Permit(Trigger.Failed, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    if (KnownContainers.Peek() == null || KnownContainers.Peek().IsInUse())
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
                    if (KnownContainers.Peek() == null)
                    {
                        brain.Fire(Trigger.Failed);
                        return;
                    }
                    FoundItem = KnownContainers.Peek().GetInventory().GetAllItems().Where(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name)).RandomOrDefault();
                    if (FoundItem != null)
                    {
                        m_aiBase.UpdateAiStatus(State.SearchForItem, FoundItem.m_shared.m_name);
                        KnownContainers.Peek().GetInventory().RemoveItem(FoundItem, 1);
                        Common.Invoke<Container>(KnownContainers.Peek(), "Save");
                        Common.Invoke<Inventory>(KnownContainers.Peek().GetInventory(), "Changed");

                        brain.Fire(Trigger.ItemFound);
                    }
                    else
                    {
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
                //Common.Dbgl($"State MoveToContainer: {KnownContainers.Peek().name}");
                if (KnownContainers.Peek() == null)
                {
                    aiBase.StopMoving();
                    KnownContainers.Pop();
                    aiBase.Brain.Fire(Trigger.Failed);
                    //Common.Dbgl("Container = null");
                    return;
                }
                aiBase.MoveAndAvoidFire(KnownContainers.Peek().transform.position, dt, 0.5f);
                if (Vector3.Distance(aiBase.Instance.transform.position, KnownContainers.Peek().transform.position) < 2)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerIsClose);
                    //Debug.Log($"{KnownContainers.Peek().name} is close");
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
                    //Debug.Log("GroundItem = null");
                    return;
                }
                aiBase.MoveAndAvoidFire(m_groundItem.transform.position, dt, 0.5f);
                if (Vector3.Distance(aiBase.Instance.transform.position, m_groundItem.transform.position) < 1.5)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.GroundItemIsClose);
                    //Debug.Log("GroundItem is close");
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToPickable))
            {
                if (m_pickable == null || m_pickable?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Failed);
                    return;
                }

                if (aiBase.MoveAndAvoidFire(m_pickable.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable is close");
                    if (m_pickable.Interact((aiBase.Character as Humanoid), false))
                    {
                        aiBase.Brain.Fire(Trigger.WaitForPickable);
                        return;
                    }
                    else
                    {
                        m_pickable = null;
                        aiBase.Brain.Fire(Trigger.Failed);
                        return;
                    }
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.WaitingForPickable))
            {
                if (Time.time < m_pickableTimer) return;

                if (m_pickable == null || m_pickable?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable = null");
                    aiBase.Brain.Fire(Trigger.Failed);
                    return;
                }
                m_groundItem = Common.GetClosestItem(aiBase.Instance, 3, m_pickable.m_itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name, false);
                if (m_groundItem == null)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable dropped item not found");
                    aiBase.Brain.Fire(Trigger.Failed);
                    return;
                }
                Common.Dbgl($"Pickable itemdrop:{m_groundItem?.m_itemData?.m_shared?.m_name ?? "is null"}");
                aiBase.Brain.Fire(Trigger.GroundItemIsClose);
            }

            if (aiBase.Brain.IsInState(State.OpenContainer))
            {
                if ((m_openChestTimer += dt) > OpenChestDelay)
                {
                    //Debug.Log("Open Container");
                    aiBase.Brain.Fire(Trigger.ContainerOpened);
                }
            }
        }
    }
}
