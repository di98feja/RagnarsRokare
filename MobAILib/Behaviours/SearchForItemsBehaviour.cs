using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class SearchForItemsBehaviour : IBehaviour
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
        public string StartState { get { return State.Main + Postfix; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public Vector3 CenterPoint { get; set; }
        public bool IncludePickables { get; set; } = true;
        public string Postfix { get; set; } = string.Empty;

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
            FoundGroundItemTrigger = brain.SetTriggerParameters<ItemDrop>(Trigger.FoundGroundItem+Postfix);
            m_searchRadius = aiBase.Awareness * 5;



            brain.Configure(State.Main+Postfix)
                .InitialTransition(State.SearchItemsOnGround+Postfix)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Timeout+Postfix, () => FailState)
                .OnEntry(t =>
                {
                    m_currentSearchTime = 0f;
                    Common.Dbgl("Entered SearchForItemsBehaviour", true);
                })
                .OnExit(t =>
                {
                    KnownContainers.Peek()?.SetInUse(inUse: false);
                    CenterPoint = Vector3.zero;
                    m_aiBase.UpdateAiStatus(string.Empty);
                });

            brain.Configure(State.SearchItemsOnGround+Postfix)
                .SubstateOf(State.Main+Postfix)
                .Permit(FoundGroundItemTrigger.Trigger, State.MoveToGroundItem+Postfix)
                .Permit(Trigger.Failed+Postfix, State.SearchForPickable+Postfix)
                .OnEntry(t =>
                {
                    ItemDrop groundItem = Common.GetNearbyItem(m_aiBase.Instance, Items.Select(i => i.m_shared.m_name), m_searchRadius);
                    if (groundItem != null)
                    {
                        m_aiBase.UpdateAiStatus(State.SearchItemsOnGround, groundItem.m_itemData.m_shared.m_name);
                        brain.Fire(FoundGroundItemTrigger, groundItem);
                        return;
                    }
                    brain.Fire(Trigger.Failed+Postfix);
                });

            brain.Configure(State.SearchForPickable+Postfix)
                .SubstateOf(State.Main+Postfix)
                .Permit(Trigger.FoundPickable+Postfix, State.MoveToPickable+Postfix)
                .Permit(Trigger.Failed+Postfix, State.SearchForRandomContainer+Postfix)
                .OnEntry(t =>
                {
                    if (IncludePickables)
                    {
                        Pickable pickable = Common.GetNearbyPickable(m_aiBase.Instance, m_aiBase.m_trainedAssignments, m_searchRadius, Items.Select(i => i?.m_shared?.m_name));
                        if ((bool)pickable)
                        {
                            m_pickable = pickable;
                            Common.Dbgl($"Found pickable: {m_pickable.GetHoverName()}", true);
                            aiBase.Brain.Fire(Trigger.FoundPickable+Postfix);
                            return;
                        }
                    }
                    brain.Fire(Trigger.Failed+Postfix);
                });

            brain.Configure(State.SearchForRandomContainer+Postfix)
                .SubstateOf(State.Main+Postfix)
                .Permit(Trigger.ContainerFound+Postfix, State.MoveToContainer+Postfix)
                .PermitDynamic(Trigger.ContainerNotFound+Postfix, () => FailState)
                .PermitDynamic(Trigger.Failed+Postfix, () => FailState)
                .OnEntry(t =>
                {
                    if (KnownContainers.Any())
                    {
                        var matchingContainer = KnownContainers.Where(c => c.GetInventory().GetAllItems().Any(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name))).RandomOrDefault();
                        if (matchingContainer != null)
                        {
                            KnownContainers.Remove(matchingContainer);
                            KnownContainers.Push(matchingContainer);
                            brain.Fire(Trigger.ContainerFound+Postfix);
                            return;
                        }
                    }

                    Container nearbyChest = Common.FindRandomNearbyContainer(m_aiBase.Instance, KnownContainers, AcceptedContainerNames, m_searchRadius, CenterPoint);
                    if (nearbyChest != null)
                    {
                        KnownContainers.Push(nearbyChest);
                        m_aiBase.UpdateAiStatus(State.SearchForRandomContainer);
                        m_aiBase.Brain.Fire(Trigger.ContainerFound+Postfix);
                    }
                    else
                    {
                        KnownContainers.Clear();
                        m_aiBase.Brain.Fire(Trigger.ContainerNotFound+Postfix);
                    }
                });

            brain.Configure(State.MoveToGroundItem+Postfix)
                .SubstateOf(State.Main+Postfix)
                .Permit(Trigger.GroundItemIsClose+Postfix, State.PickUpItemFromGround+Postfix)
                .Permit(Trigger.Failed+Postfix, State.SearchItemsOnGround+Postfix)
                .OnEntry(t =>
                {
                    m_groundItem = t.Parameters[0] as ItemDrop;
                    if (m_groundItem == null || Common.GetNView(m_groundItem)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed+Postfix);
                        return;
                    }
                    m_aiBase.UpdateAiStatus(State.MoveToGroundItem, m_groundItem.m_itemData.m_shared.m_name);
                });

            brain.Configure(State.MoveToPickable+Postfix)
                .SubstateOf(State.Main+Postfix)
                .Permit(Trigger.WaitForPickable+Postfix, State.WaitingForPickable+Postfix)
                .Permit(Trigger.Failed+Postfix, State.SearchItemsOnGround+Postfix)
                .OnEntry(t =>
                {
                    if (m_pickable == null || Common.GetNView(m_pickable)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed+Postfix);
                        return;
                    }
                    m_aiBase.UpdateAiStatus(State.MoveToPickable, m_pickable.GetHoverName());
                    m_pickableTimer = Time.time + 0.7f;
                });

            brain.Configure(State.WaitingForPickable+Postfix)
                .SubstateOf(State.Main+Postfix)
                .Permit(Trigger.GroundItemIsClose+Postfix, State.PickUpItemFromGround+Postfix)
                .Permit(Trigger.Failed + Postfix, State.SearchItemsOnGround + Postfix)
                .OnEntry(t =>
                {
                    m_pickableTimer = Time.time + 0.7f;
                });


            brain.Configure(State.PickUpItemFromGround + Postfix)
                .SubstateOf(State.Main + Postfix)
                .PermitDynamic(Trigger.ItemFound + Postfix, () => SuccessState)
                .Permit(Trigger.Failed + Postfix, State.SearchItemsOnGround + Postfix)
                .OnEntry(t =>
                {
                    FoundItem = m_groundItem.m_itemData;
                    if (m_groundItem == null || Common.GetNView(m_groundItem)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed + Postfix);
                        return;
                    }
                    m_aiBase.UpdateAiStatus(State.PickUpItemFromGround, FoundItem.m_shared.m_name);
                    if (m_groundItem.RemoveOne())
                    {
                        brain.Fire(Trigger.ItemFound + Postfix);
                    }
                    else
                    {
                        brain.Fire(Trigger.Failed + Postfix);
                    }
                });

            brain.Configure(State.MoveToContainer + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(Trigger.ContainerIsClose + Postfix, State.OpenContainer + Postfix)
                .Permit(Trigger.Failed + Postfix, State.SearchItemsOnGround + Postfix)
                .PermitDynamic(Trigger.ContainerNotFound + Postfix, () => FailState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToContainer);
                });

            brain.Configure(State.OpenContainer + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(Trigger.ContainerOpened + Postfix, State.SearchForItem + Postfix)
                .Permit(Trigger.Failed + Postfix, State.SearchItemsOnGround + Postfix)
                .OnEntry(t =>
                {
                    if (KnownContainers.Peek() == null || KnownContainers.Peek().IsInUse())
                    {
                        KnownContainers.Pop();
                        brain.Fire(Trigger.Failed + Postfix);
                    }
                    else
                    {
                        KnownContainers.Peek().SetInUse(inUse: true);
                        m_openChestTimer = 0f;
                    }
                });

            brain.Configure(State.SearchForItem + Postfix)
                .SubstateOf(State.Main + Postfix)
                .PermitDynamic(Trigger.ItemFound + Postfix, () => SuccessState)
                .Permit(Trigger.Failed + Postfix, State.SearchItemsOnGround + Postfix)
                .OnEntry(t =>
                {
                    if (KnownContainers.Peek() == null)
                    {
                        brain.Fire(Trigger.Failed + Postfix);
                        return;
                    }
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}: Search container", true, "");
                    FoundItem = KnownContainers.Peek().GetInventory().GetAllItems().Where(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name)).RandomOrDefault();
                    if (FoundItem != null)
                    {
                        m_aiBase.UpdateAiStatus(State.SearchForItem, FoundItem.m_shared.m_name);
                        KnownContainers.Peek().GetInventory().RemoveItem(FoundItem, 1);
                        Utils.Invoke<Container>(KnownContainers.Peek(), "Save");
                        Utils.Invoke<Inventory>(KnownContainers.Peek().GetInventory(), "Changed");

                        brain.Fire(Trigger.ItemFound + Postfix);
                    }
                    else
                    {
                        brain.Fire(Trigger.Failed + Postfix);
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
                aiBase.Brain.Fire(Trigger.Timeout + Postfix);
            }

            if (aiBase.Brain.IsInState(State.MoveToContainer + Postfix))
            {
                if (KnownContainers.Peek() == null)
                {
                    aiBase.StopMoving();
                    KnownContainers.Pop();
                    aiBase.Brain.Fire(Trigger.Failed + Postfix);
                    //Common.Dbgl("Container = null");
                    return;
                }
                if (aiBase.MoveAndAvoidFire(KnownContainers.Peek().transform.position, dt, 2.0f))
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerIsClose + Postfix);
                    //Debug.Log($"{KnownContainers.Peek().name} is close");
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToGroundItem + Postfix))
            {
                if (m_groundItem == null || m_groundItem?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_groundItem = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Failed + Postfix);
                    //Debug.Log("GroundItem = null");
                    return;
                }
                
                if (aiBase.MoveAndAvoidFire(m_groundItem.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.GroundItemIsClose + Postfix);
                    //Debug.Log("GroundItem is close");
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToPickable + Postfix))
            {
                if (m_pickable == null || m_pickable?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.Failed + Postfix);
                    return;
                }

                if (aiBase.MoveAndAvoidFire(m_pickable.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable is close", true);
                    if (m_pickable.Interact((aiBase.Character as Humanoid), false, false))
                    {
                        aiBase.Brain.Fire(Trigger.WaitForPickable + Postfix);
                        return;
                    }
                    else
                    {
                        m_pickable = null;
                        aiBase.Brain.Fire(Trigger.Failed + Postfix);
                        return;
                    }
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.WaitingForPickable + Postfix))
            {
                if (Time.time < m_pickableTimer) return;

                if (m_pickable == null || m_pickable?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable = null", true);
                    aiBase.Brain.Fire(Trigger.Failed + Postfix);
                    return;
                }
                m_groundItem = Common.GetClosestItem(aiBase.Instance, 3, m_pickable.m_itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name, false);
                if (m_groundItem == null)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable dropped item not found", true);
                    aiBase.Brain.Fire(Trigger.Failed + Postfix);
                    return;
                }
                Common.Dbgl($"Pickable itemdrop:{m_groundItem?.m_itemData?.m_shared?.m_name ?? "is null"}", true);
                aiBase.Brain.Fire(Trigger.GroundItemIsClose + Postfix);
            }

            if (aiBase.Brain.IsInState(State.OpenContainer + Postfix))
            {
                if ((m_openChestTimer += dt) > OpenChestDelay)
                {
                    //Debug.Log("Open Container");
                    aiBase.Brain.Fire(Trigger.ContainerOpened + Postfix);
                }
            }
        }
    }
}
