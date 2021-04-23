using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlaveGreylings
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


        private const string ItemFound_trigger = Prefix + "ItemFound";
        private const string ItemNotFound_trigger = Prefix + "ItemNotFound";
        private const string ContainerFound_trigger = Prefix + "ContainerFound";
        private const string ContainerNotFound_trigger = Prefix + "ContainerNotFound";
        private const string ContainerIsClose_trigger = Prefix + "ContainerIsClose";
        private const string Failed_trigger = Prefix + "Failed";
        private const string ContainerOpened_trigger = Prefix + "ContainerOpened";
        private const string Timeout_trigger = Prefix + "Timeout";
        private const string Update_trigger = Prefix + "Update";
        private const string GroundItemIsClose_trigger = Prefix + "GroundItemIsClose";
        private const string FoundGroundItem_Trigger = Prefix + "FoundGroundItem";

        StateMachine<string, string>.TriggerWithParameters<ItemDrop> FoundGroundItemTrigger;
        private float OpenChestTimer;
        private float CurrentSearchTime;

        public IEnumerable<ItemDrop.ItemData> Items { get; set; }
        public MaxStack<Container> KnownContainers { get; set; }
        public string[] AcceptedContainerNames { get; set; }

        public ItemDrop.ItemData FoundItem { get; private set; }
        public float OpenChestDelay { get; private set; } = 1;

        public float MaxSearchTime { get; set; } = 60;

        public string InitState { get { return Main_state; } }

        private ItemDrop GroundItem;
        private MobAIBase m_aiBase;


        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string SuccessState, string FailState, string parentState)
        {
            m_aiBase = aiBase;
            FoundGroundItemTrigger = brain.SetTriggerParameters<ItemDrop>(FoundGroundItem_Trigger);

            brain.Configure(Main_state)
                .InitialTransition(SearchItemsOnGround_state)
                .SubstateOf(parentState)
                .Permit(Timeout_trigger, FailState)
                .OnEntry(t =>
                {
                    Debug.Log("Entered SearchForItemsBehaviour");
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
                        Debug.Log($"GroundItem:{groundItem.m_itemData.m_dropPrefab.name ?? string.Empty}");
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Found {groundItem.m_itemData.m_shared.m_name} on the ground");
                        brain.Fire(FoundGroundItemTrigger, groundItem);
                        return;
                    }
                    brain.Fire(Failed_trigger);
                });

            brain.Configure(SearchForRandomContainer_state)
                .SubstateOf(Main_state)
                .Permit(ContainerFound_trigger, MoveToContainer_state)
                .Permit(ContainerNotFound_trigger, FailState)
                .Permit(Failed_trigger, FailState)
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
                    GroundItem = t.Parameters[0] as ItemDrop;
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Heading to {GroundItem.m_itemData.m_shared.m_name}");
                });


            brain.Configure(PickUpItemFromGround_state)
                .SubstateOf(Main_state)
                .Permit(ItemFound_trigger, SuccessState)
                .Permit(Failed_trigger, SearchItemsOnGround_state)
                .OnEntry(t =>
                {
                    FoundItem = GroundItem.m_itemData;
                    MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Got a {FoundItem.m_shared.m_name} from the ground");
                    if (GroundItem.RemoveOne())
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
                .Permit(ContainerNotFound_trigger, FailState)
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
                        brain.Fire(Failed_trigger);
                    }
                    else
                    {
                        KnownContainers.Peek().SetInUse(inUse: true);
                        OpenChestTimer = 0f;
                    }
                });

            brain.Configure(SearchForItem_state)
                .SubstateOf(Main_state)
                .Permit(ItemFound_trigger, SuccessState)
                .Permit(Failed_trigger, SearchItemsOnGround_state)
                .OnEntry(t =>
                {
                    FoundItem = KnownContainers.Peek().GetInventory().GetAllItems().Where(i => Items.Any(it => i.m_shared.m_name == it.m_shared.m_name)).RandomOrDefault();
                    if (FoundItem != null)
                    {
                        MobAIBase.UpdateAiStatus(m_aiBase.NView, $"Found {FoundItem.m_shared.m_name} in this a bin!");
                        KnownContainers.Peek().GetInventory().RemoveItem(FoundItem, 1);
                        Common.Invoke<Container>(KnownContainers.Peek(), "Save");
                        Common.Invoke<Inventory>(KnownContainers.Peek(), "Changed");

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
            if ((CurrentSearchTime += dt) > MaxSearchTime)
            {
                CurrentSearchTime = 0f;
                aiBase.Brain.Fire(Timeout_trigger);
            }

            if (aiBase.Brain.IsInState(MoveToContainer_state))
            {
                bool containerIsInvalid = KnownContainers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
                if (containerIsInvalid)
                {
                    (aiBase.Character as Humanoid).SetMoveDir(Vector3.zero);
                    KnownContainers.Pop();
                    aiBase.Brain.Fire(Failed_trigger);
                    return;
                }
                Common.Invoke<MonsterAI>(aiBase.Instance, "MoveAndAvoid", dt, KnownContainers.Peek().transform.position, 0.5f, false);
                if (Vector3.Distance(aiBase.Instance.transform.position, KnownContainers.Peek().transform.position) < 1.5)
                {
                    (aiBase.Character as Humanoid).SetMoveDir(Vector3.zero);
                    aiBase.Brain.Fire(ContainerIsClose_trigger);
                }
                return;
            }

            if (aiBase.Brain.IsInState(MoveToGroundItem_state))
            {
                if (GroundItem?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    GroundItem = null;
                    aiBase.Brain.Fire(Failed_trigger);
                    (aiBase.Character as Humanoid).SetMoveDir(Vector3.zero);
                    return;
                }
                Common.Invoke<MonsterAI>(aiBase.Instance, "MoveAndAvoid", dt, GroundItem.transform.position, 0.5f, false);
                if (Vector3.Distance(aiBase.Instance.transform.position, GroundItem.transform.position) < 1.5)
                {
                    (aiBase.Character as Humanoid).SetMoveDir(Vector3.zero); 
                    aiBase.Brain.Fire(GroundItemIsClose_trigger);
                }
                return;
            }

            if (aiBase.Brain.IsInState(OpenContainer_state))
            {
                if ((OpenChestTimer += dt) > OpenChestDelay)
                {
                    aiBase.Brain.Fire(ContainerOpened_trigger);
                }
            }

        }
    }
}
