using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlaveGreylings
{
    class SearchContainersForItemsBehaviour : IBehaviour
    {
        private const string Prefix = "RR_SFIIC";

        private string SearchForItemsInContainers_state = Prefix + "SearchForItemsInContainers";
        private string SearchForRandomContainer_state   = Prefix + "SearchForRandomContainer";
        private string MoveToContainer_state            = Prefix + "MoveToContainer";
        private string OpenContainer_state              = Prefix + "OpenContainer";
        private string SearchForItem_state              = Prefix + "SearchForItem";

        private string ItemFound_trigger                = Prefix + "ItemFound";
        private string ItemNotFound_trigger             = Prefix + "ItemNotFound";
        private string ContainerFound_trigger           = Prefix + "ContainerFound";
        private string ContainerNotFound_trigger        = Prefix + "ContainerNotFound";
        private string ContainerIsClose_trigger         = Prefix + "ContainerIsClose";
        private string Failed_trigger                   = Prefix + "Failed";
        private string ContainerOpened_trigger          = Prefix + "ContainerOpened";
        private string Update_trigger                   = Prefix + "Update";

        StateMachine<string, string>.TriggerWithParameters<(MobAIBase aiBase, float dt)> UpdateTrigger;
        private float OpenChestTimer;

        public IEnumerable<ItemDrop> Items { get; set; }
        public MaxStack<Container> KnownContainers { get; set; }
        public string[] AcceptedContainerNames { get; set; }

        public ItemDrop.ItemData FoundItem { get; private set; }
        public float OpenChestDelay { get; private set; } = 1;

        public void Configure(StateMachine<string, string> brain, string ExitState)
        {
            UpdateTrigger = brain.SetTriggerParameters<(MobAIBase aiBase, float dt)>(Update_trigger);

            brain.Configure(SearchForItemsInContainers_state)
                .InitialTransition(SearchForRandomContainer_state)
                .Permit(Update_trigger, SearchForRandomContainer_state)
                .OnEntry(t =>
                {
                });

            brain.Configure(SearchForRandomContainer_state)
                .SubstateOf(SearchForItemsInContainers_state)
                .Permit(ContainerFound_trigger, MoveToContainer_state)
                .Permit(ContainerNotFound_trigger, ExitState)
                .OnEntry(t =>
                {
                    var aiBase = t.Parameters[0] as MobAIBase;
                    if (KnownContainers.Any())
                    {
                        var matchingContainer = KnownContainers.Where(c => c.GetInventory().GetAllItems().Any(i => Items.Any(it => i.m_shared.m_name == it.m_itemData.m_shared.m_name))).RandomOrDefault();
                        KnownContainers.Remove(matchingContainer);
                        KnownContainers.Push(matchingContainer);
                        brain.Fire(ContainerFound_trigger);
                    }
                    else
                    {
                        Container nearbyChest = Common.FindRandomNearbyContainer(aiBase.Instance.transform.position, KnownContainers, AcceptedContainerNames);
                        if (nearbyChest != null)
                        {
                            KnownContainers.Push(nearbyChest);
                            aiBase.Brain.Fire(ContainerFound_trigger);
                        }
                        else
                        {
                            KnownContainers.Clear();
                            aiBase.Brain.Fire(ContainerNotFound_trigger);
                        }
                    }
                });
            brain.Configure(MoveToContainer_state)
                .SubstateOf(SearchForItemsInContainers_state)
                .Permit(ContainerIsClose_trigger, OpenContainer_state)
                .Permit(Failed_trigger, SearchForRandomContainer_state)
                .OnEntry(t =>
                {

                });
            brain.Configure(OpenContainer_state)
                .SubstateOf(SearchForItemsInContainers_state)
                .Permit(ContainerOpened_trigger, SearchForItem_state)
                .Permit(Failed_trigger, SearchForRandomContainer_state)
                .OnEntry(t =>
                {
                    if (KnownContainers.Peek().IsInUse())
                    {
                        brain.Fire(Failed_trigger);
                    }
                    KnownContainers.Peek().SetInUse(inUse: true);
                    OpenChestTimer = 0f;
                });

            brain.Configure(SearchForItem_state)
                .SubstateOf(SearchForItemsInContainers_state)
                .Permit(ItemFound_trigger, ExitState)
                .Permit(ItemNotFound_trigger, SearchForRandomContainer_state)
                .OnEntry(t =>
                {

                });
        }

        public IEnumerable<string> RegisterStates()
        {
            return new List<string>()
            {
                SearchForItemsInContainers_state,
                SearchForRandomContainer_state,
                MoveToContainer_state,
                OpenContainer_state,
                SearchForItem_state,
            };
        }

        public IEnumerable<string> RegisterTriggers()
        {
            return new List<string>
            {
                ItemFound_trigger,
                ItemNotFound_trigger,
                ContainerFound_trigger,
                ContainerNotFound_trigger,
                ContainerIsClose_trigger,
                Failed_trigger,
                ContainerOpened_trigger
            };
        }

        public void Update(MobAIBase aiBase, float dt)
        {

            bool containerIsInvalid = KnownContainers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
            if (containerIsInvalid)
            {
                KnownContainers.Pop();
                aiBase.Brain.Fire(Failed_trigger);
                return;
            }

            if (aiBase.Brain.IsInState(MoveToContainer_state))
            {
                Common.Invoke<MonsterAI>(aiBase.Instance, "MoveAndAvoid", dt, KnownContainers.Peek().transform.position, 0.5f, false);
                if (Vector3.Distance(aiBase.Instance.transform.position, KnownContainers.Peek().transform.position) < 1.5)
                {
                    aiBase.Brain.Fire(ContainerIsClose_trigger);
                }
                return;
            }

            if (aiBase.Brain.IsInState(OpenContainer_state))
            {
                if (OpenChestTimer += dt > OpenChestDelay)
                return ("OpenContainer", null);
            }
            //else if (foundItem != null)
            //{
            //    Debug.Log("Item found, Close chest");

            //    KnownContainers.Peek().SetInUse(inUse: false);

            //    KnownContainers.Peek().GetInventory().RemoveItem(foundItem, 1);
            //    Invoke<Container>(KnownContainers.Peek(), "Save");
            //    Invoke<Inventory>(KnownContainers.Peek(), "Changed");
            //    return (ItemFound_trigger, foundItem);
            //}
            //else
            //{
            //    Debug.Log("Item not found, Close chest");
            //    KnownContainers.Peek().SetInUse(inUse: false);
            //}
            //return ("", null);

        }
    }
}
