using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class SearchForItemsBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_SFI";


        private StateDef State { get; set; }
        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string SearchItemsOnGround { get { return $"{prefix}SearchItemsOnGround"; } }
            public string SearchForPickable { get { return $"{prefix}SearchForPickable"; } }
            public string MoveToGroundItem { get { return $"{prefix}MoveToGroundItem"; } }
            public string SearchForRandomContainer { get { return $"{prefix}SearchForRandomContainer"; } }
            public string MoveToContainer { get { return $"{prefix}MoveToContainer"; } }
            public string OpenContainer { get { return $"{prefix}OpenContainer"; } }
            public string SearchForItem { get { return $"{prefix}SearchForItem"; } }
            public string PickUpItemFromGround { get { return $"{prefix}PickUpItemFromGround"; } }
            public string AvoidFire { get { return $"{prefix}AvoidFire"; } }
            public string MoveToPickable { get { return $"{prefix}MoveToPickable"; } }
            public string WaitingForPickable { get { return $"{prefix}WaitingForPickable"; } }
            public StateDef(string prefix)
            {
                this.prefix = prefix;
            }
        }

        private TriggerDef Trigger { get; set; }
        private sealed class TriggerDef
        {
            private readonly string prefix;

            public string ItemFound { get { return $"{prefix}ItemFound"; } }
            public string ContainerFound { get { return $"{prefix}ContainerFound"; } }
            public string ContainerNotFound { get { return $"{prefix}ContainerNotFound"; } }
            public string ContainerIsClose { get { return $"{prefix}ContainerIsClose"; } }
            public string Failed { get { return $"{prefix}Failed"; } }
            public string ContainerOpened { get { return $"{prefix}ContainerOpened"; } }
            public string Timeout { get { return $"{prefix}Timeout"; } }
            public string GroundItemIsClose { get { return $"{prefix}GroundItemIsClose"; } }
            public string FoundGroundItem { get { return $"{prefix}FoundGroundItem"; } }
            public string FoundPickable { get { return $"{prefix}FoundPickable"; } }
            public string WaitForPickable { get { return $"{prefix}WaitForPickable"; } }
            public string Abort { get { return $"{prefix}Abort"; } }
            public TriggerDef(string prefix)
            {
                this.prefix = prefix;
            }
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
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public Vector3 CenterPoint { get; set; }
        public bool IncludePickables { get; set; } = true;
        public string Postfix { get; set; } = string.Empty;

        public string StartState { get { return State.Main + Postfix; } }

        private ItemDrop m_groundItem;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTime;
        private int m_searchRadius;
        private Pickable m_pickable;
        private float m_pickableTimer;

        public void Abort()
        {
            m_aiBase.Brain.Fire(Trigger.Abort);
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);

            m_aiBase = aiBase;
            FoundGroundItemTrigger = brain.SetTriggerParameters<ItemDrop>(Trigger.FoundGroundItem + Postfix);
            m_searchRadius = aiBase.Awareness * 5;
            AcceptedContainerNames = aiBase.AcceptedContainerNames;

            brain.Configure(State.Main + Postfix)
                .InitialTransition(State.SearchItemsOnGround + Postfix)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Timeout + Postfix, () => FailState)
                .Permit(Trigger.Abort + Postfix, FailState + Postfix)
                .OnEntry(t =>
                {
                    m_currentSearchTime = 0f;
                    Common.Dbgl("Entered SearchForItemsBehaviour", true);
                })
                .OnExit(t =>
                {
                    KnownContainers.Peek()?.SetInUse(inUse: false);
                    CenterPoint = Vector3.zero;
                    aiBase.KnownContainers = KnownContainers;
                    //Debug.Log("Exit SearchForItemsBehaviour");
                });

            brain.Configure(State.SearchItemsOnGround + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(FoundGroundItemTrigger.Trigger, State.MoveToGroundItem + Postfix)
                .Permit(Trigger.Failed + Postfix, State.SearchForPickable + Postfix)
                .OnEntry(t =>
                {
                    ItemDrop groundItem = Common.GetNearbyItem(m_aiBase.Instance, Items.Select(i => i.m_shared.m_name), m_searchRadius);
                    //Debug.Log($"groundItem = {groundItem}");
                    if (groundItem != null)
                    {
                        m_aiBase.UpdateAiStatus(State.SearchItemsOnGround, groundItem.m_itemData.m_shared.m_name);
                        brain.Fire(FoundGroundItemTrigger, groundItem);
                        return;
                    }
                    brain.Fire(Trigger.Failed + Postfix);
                });

            brain.Configure(State.SearchForPickable + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(Trigger.FoundPickable + Postfix, State.MoveToPickable + Postfix)
                .Permit(Trigger.Failed + Postfix, State.SearchForRandomContainer + Postfix)
                .OnEntry(t =>
                {
                    if (IncludePickables)
                    {
                        Pickable pickable = Common.GetNearbyPickable(m_aiBase.Instance, m_aiBase.m_trainedAssignments, m_searchRadius, Items.Select(i => i?.m_shared?.m_name));
                        if ((bool)pickable)
                        {
                            m_pickable = pickable;
                            Common.Dbgl($"Found pickable: {m_pickable.GetHoverName()}", true);
                            aiBase.Brain.Fire(Trigger.FoundPickable + Postfix);
                            return;
                        }
                    }
                    brain.Fire(Trigger.Failed + Postfix);
                });

            brain.Configure(State.SearchForRandomContainer + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(Trigger.ContainerFound + Postfix, State.MoveToContainer + Postfix)
                .PermitDynamic(Trigger.ContainerNotFound + Postfix, () => FailState)
                .PermitDynamic(Trigger.Failed + Postfix, () => FailState)
                .OnEntry(t =>
                {
                    if (KnownContainers.Any())
                    {
                        var matchingContainer = KnownContainers.Where(c => c.GetInventory().GetAllItems().Any(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name))).RandomOrDefault();
                        if (matchingContainer != null)
                        {
                            KnownContainers.Remove(matchingContainer);
                            KnownContainers.Push(matchingContainer);
                            brain.Fire(Trigger.ContainerFound + Postfix);
                            return;
                        }
                    }

                    Container nearbyChest = Common.FindRandomNearbyContainer(m_aiBase.Instance, KnownContainers, AcceptedContainerNames, m_searchRadius, CenterPoint);
                    if (nearbyChest != null)
                    {
                        KnownContainers.Push(nearbyChest);
                        m_aiBase.UpdateAiStatus(State.SearchForRandomContainer);
                        m_aiBase.Brain.Fire(Trigger.ContainerFound + Postfix);
                    }
                    else
                    {
                        KnownContainers.Clear();
                        m_aiBase.Brain.Fire(Trigger.ContainerNotFound + Postfix);
                    }
                });

            brain.Configure(State.MoveToGroundItem + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(Trigger.GroundItemIsClose + Postfix, State.PickUpItemFromGround + Postfix)
                .Permit(Trigger.Failed + Postfix, State.SearchItemsOnGround + Postfix)
                .OnEntry(t =>
                {
                    m_groundItem = t.Parameters[0] as ItemDrop;
                    if (m_groundItem == null || Common.GetNView(m_groundItem)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed + Postfix);
                        return;
                    }
                    m_aiBase.UpdateAiStatus(State.MoveToGroundItem, m_groundItem.m_itemData.m_shared.m_name);
                });

            brain.Configure(State.MoveToPickable + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(Trigger.WaitForPickable + Postfix, State.WaitingForPickable + Postfix)
                .Permit(Trigger.Failed + Postfix, State.SearchItemsOnGround + Postfix)
                .OnEntry(t =>
                {
                    if (m_pickable == null || Common.GetNView(m_pickable)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.Failed + Postfix);
                        return;
                    }
                    m_aiBase.UpdateAiStatus(State.MoveToPickable, m_pickable.GetHoverName());
                    m_pickableTimer = Time.time + 0.7f;
                });

            brain.Configure(State.WaitingForPickable + Postfix)
                .SubstateOf(State.Main + Postfix)
                .Permit(Trigger.GroundItemIsClose + Postfix, State.PickUpItemFromGround + Postfix)
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
                //Debug.Log("MoveToGroundItem");
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
