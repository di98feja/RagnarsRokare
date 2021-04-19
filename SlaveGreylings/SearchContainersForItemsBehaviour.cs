using RagnarsRokare.MobAI;
using Stateless;
using System.Collections.Generic;

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
        public IEnumerable<ItemDrop> Items { get; set; }
        public MaxStack<Container> KnownContainers { get; set; }
        public string[] AcceptedContainerNames { get; set; }

        public void Configure(StateMachine<string, string> brain, string ExitState)
        {
            brain.Configure(SearchForItemsInContainers_state)
                .Permit(ItemFound_trigger, ExitState)
                .OnEntry(t =>
                {

                });
            brain.Configure(SearchForRandomContainer_state)
                .SubstateOf(SearchForItemsInContainers_state)
                .Permit(ContainerFound_trigger, MoveToContainer_state)
                .Permit(ContainerNotFound_trigger, ExitState)
                .OnEntry(t =>
                {

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

        public void Update(MobAIBase instance, float dt)
        {
            bool containerIsInvalid = KnownContainers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
            if (containerIsInvalid)
            {
                KnownContainers.Pop();
                instance.Brain.Fire(Failed_trigger);
                return;
            }
            //ItemDrop.ItemData foundItem = null;
            //bool isCloseToContainer = false;
            //if (instance.Brain.IsInState(SearchForRandomContainer_state))
            //{
            //    if (KnownContainers.Any())
            //    {
            //        isCloseToContainer = Vector3.Distance(instance.transform.position, KnownContainers.Peek().transform.position) < 1.5;
            //        foundItem = KnownContainers.Peek().GetInventory().GetAllItems().Where(i => Items.Any(it => i.m_shared.m_name == it.m_itemData.m_shared.m_name)).RandomOrDefault();
            //    }
            //    if (!KnownContainers.Any() || (isCloseToContainer && foundItem == null))
            //    {
            //        Container nearbyChest = FindRandomNearbyContainer(instance.transform.position, KnownContainers, AcceptedContainerNames);
            //        if (nearbyChest != null)
            //        {
            //            KnownContainers.Push(nearbyChest);
            //            return (ContainerFound, null);
            //        }
            //        else
            //        {
            //            KnownContainers.Clear();
            //            return ("CannotFindContainers", null);
            //        }
            //    }
            //}
            //if (!isCloseToContainer)
            //{
            //    Invoke<MonsterAI>(instance, "MoveAndAvoid", dt, KnownContainers.Peek().transform.position, 0.5f, false);
            //    return ("MovingtoContainer", null);
            //}
            //else if (!KnownContainers.Peek()?.IsInUse() ?? false)
            //{
            //    Debug.Log("Open chest");
            //    KnownContainers.Peek().SetInUse(inUse: true);
            //    return ("OpenContainer", null);
            //}
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
