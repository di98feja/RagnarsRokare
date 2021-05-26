using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    class ItemSortingBehaviour : IBehaviour
    {
        private const string Prefix = "RR_ISB";
        private class State
        {
            public const string Main = Prefix + "Main";
            public const string SearchForRandomContainer = Prefix + "SearchForRandomContainer";
            public const string OpenContainer = Prefix + "OpenContainer";
            public const string OpenStorageContainer = Prefix + "OpenStorageContainer";
            public const string AddContainerItemsToItemDictionary = Prefix + "AddContainerItemsToItemDictionary";
            public const string UnloadIntoStorageContainer = Prefix + "UnloadIntoStorageContainer";
            public const string SearchItemsOnGround = Prefix + "SearchItemsOnGround";
            public const string MoveToGroundItem = Prefix + "MoveToGroundItem";
            public const string PickUpItemFromGround = Prefix + "PickUpItemFromGround";
            public const string MoveToDumpContainer = Prefix + "MoveToDumpContainer";
            public const string MoveToContainer = Prefix + "MoveToContainer";
            public const string MoveToStorageContainer = Prefix + "MoveToStorageContainer";
        }

        private class Trigger
        {
            public const string ItemFound = Prefix + "ItemFound";
            public const string ContainerFound = Prefix + "ContainerFound";
            public const string ContainerNotFound = Prefix + "ContainerNotFound";
            public const string ContainerIsClose = Prefix + "ContainerIsClose";
            public const string Failed = Prefix + "Failed";
            public const string ContainerOpened = Prefix + "ContainerOpened";
            public const string ContainerSearched = Prefix + "ContainerSearched";
            public const string Timeout = Prefix + "Timeout";
            public const string GroundItemIsClose = Prefix + "GroundItemIsClose";
            public const string FoundGroundItem = Prefix + "FoundGroundItem";
            public const string GroundItemLost = Prefix + "GroundItemLost";
            public const string ItemSorted = Prefix + "ItemSorted";
        }

        // Input
        public string[] AcceptedContainerNames { get; set; }

        // Output

        // Settings
        public float MaxSearchTime { get; set; } = 60f;
        public string StartState { get { return State.Main; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float OpenChestDelay { get; private set; } = 2;

        private Dictionary<string, (Container container, int count, float time)> m_itemsDictionary;

        private ItemDrop m_item;
        private ItemDrop.ItemData m_carriedItem;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTime;
        private int m_searchRadius;
        private MaxStack<Container> m_knownContainers;
        private Vector3 m_startPosition;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            m_searchRadius = aiBase.Awareness * 5;
            m_knownContainers = new MaxStack<Container>(aiBase.Intelligence);
            m_itemsDictionary = new Dictionary<string, (Container container, int count, float time)>();

            brain.Configure(State.Main)
                .InitialTransition(State.SearchForRandomContainer)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered ItemSortingBehaviour");
                    m_startPosition = aiBase.Character.transform.position;
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.SearchForRandomContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerFound, State.MoveToContainer)
                .Permit(Trigger.ContainerNotFound, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    //Common.Dbgl("Entered SearchForRandomContainer");
                    m_currentSearchTime = 0f;
                });

            brain.Configure(State.MoveToContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerIsClose, State.OpenContainer)
                .Permit(Trigger.ContainerNotFound, State.SearchForRandomContainer)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToContainer);
                    m_currentSearchTime = 0;
                });

            brain.Configure(State.MoveToStorageContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerIsClose, State.OpenStorageContainer)
                .Permit(Trigger.ContainerNotFound, State.SearchForRandomContainer)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToStorageContainer);
                    m_currentSearchTime = 0;
                });

            brain.Configure(State.OpenContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.AddContainerItemsToItemDictionary)
                .Permit(Trigger.ContainerNotFound, State.SearchForRandomContainer)
                .OnEntry(t =>
                {
                    if (m_knownContainers.Peek() == null)
                    {
                        m_knownContainers.Pop();
                        brain.Fire(Trigger.ContainerNotFound);
                    }
                    else
                    {
                        m_knownContainers.Peek().SetInUse(inUse: true);
                        m_openChestTimer = 0f;
                    }
                });

            brain.Configure(State.OpenStorageContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.UnloadIntoStorageContainer)
                .OnEntry(t =>
                {
                    if (m_knownContainers.Peek() == null)
                    {
                        m_knownContainers.Pop();
                        brain.Fire(Trigger.ContainerNotFound);
                    }
                    else
                    {
                        m_knownContainers.Peek().SetInUse(inUse: true);
                        m_openChestTimer = 0f;
                    }
                });
            

            brain.Configure(State.UnloadIntoStorageContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ItemSorted, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    if (m_itemsDictionary[m_carriedItem.m_shared.m_name].container.GetInventory().CanAddItem(m_carriedItem))
                    {
                        m_itemsDictionary[m_carriedItem.m_shared.m_name].container.GetInventory().AddItem(m_carriedItem);
                    }
                    else
                    {
                        (aiBase.Character as Humanoid).DropItem((aiBase.Character as Humanoid).GetInventory(), m_carriedItem, m_carriedItem.m_stack);
                    }
                    Common.Dbgl($"Item Keys: {string.Join(",", m_itemsDictionary.Keys)}");
                    m_knownContainers.Peek().SetInUse(inUse: false);
                    brain.Fire(Trigger.ItemSorted);
                });


            brain.Configure(State.AddContainerItemsToItemDictionary)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerSearched, State.SearchItemsOnGround)
                .Permit(Trigger.ContainerNotFound, State.SearchForRandomContainer)
                .OnEntry(t =>
                {
                    if (m_knownContainers.Peek() == null)
                    {
                        brain.Fire(Trigger.ContainerNotFound);
                        return;
                    }
                    List<ItemDrop.ItemData> FoundItem = m_knownContainers.Peek().GetInventory().GetAllItems();
                    if (FoundItem.Any())
                    {
                        Dictionary<string, int> chestInventory = new Dictionary<string, int>();
                        foreach (ItemDrop.ItemData Item in FoundItem)
                        {
                            string Key = Common.GetPrefabName(Item.m_shared.m_name);
                            Common.Dbgl($"Key: {Key}");
                            if (chestInventory.ContainsKey(Key))
                            {
                                chestInventory[Key] += Item.m_stack;
                            }
                            else
                            {
                                chestInventory.Add(Key, Item.m_stack);
                            }
                        }
                        foreach (KeyValuePair<string, int> Item in chestInventory)
                        {
                            if (m_itemsDictionary.ContainsKey(Item.Key) && m_itemsDictionary[Item.Key].count < Item.Value)
                            {
                                m_itemsDictionary[Item.Key] = (m_knownContainers.Peek(), Item.Value, Time.time);
                            }
                            else if (!m_itemsDictionary.ContainsKey(Item.Key))
                            {
                                m_itemsDictionary.Add(Item.Key, (m_knownContainers.Peek(), Item.Value, Time.time));
                            }
                        }
                    }
                    m_knownContainers.Peek().SetInUse(inUse: false);
                    brain.Fire(Trigger.ContainerSearched);
                });

            brain.Configure(State.SearchItemsOnGround)
                .SubstateOf(State.Main)
                .Permit(Trigger.FoundGroundItem, State.MoveToGroundItem)
                .Permit(Trigger.GroundItemLost, State.SearchForRandomContainer)
                .Permit(Trigger.ItemFound, State.MoveToStorageContainer)
                .OnEntry(t =>
                {
                    //List<ItemDrop.ItemData> CarriedItem = (aiBase.Character as Humanoid).GetInventory().GetAllItems();
                    //foreach (ItemDrop.ItemData invItem in CarriedItem)
                        //if (m_itemsDictionary.Keys.Contains(Common.GetPrefabName(invItem.m_shared.m_name)) && invItem.m_stack > 0)
                        //{
                        //    m_carriedItem = invItem;
                        //    Common.Dbgl($"Already got a {m_carriedItem.m_shared.m_name} here.");
                        //    brain.Fire(Trigger.ItemFound);
                        //    return;
                        //}
                });

            brain.Configure(State.MoveToGroundItem)
                .SubstateOf(State.Main)
                .Permit(Trigger.GroundItemIsClose, State.PickUpItemFromGround)
                .Permit(Trigger.GroundItemLost, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToGroundItem, m_item.m_itemData.m_shared.m_name);
                });

            brain.Configure(State.PickUpItemFromGround)
                .SubstateOf(State.Main)
                .Permit(Trigger.ItemFound, State.MoveToStorageContainer)
                .Permit(Trigger.GroundItemLost, State.SearchItemsOnGround)
                .OnEntry(t =>
                {
                    Common.Dbgl("PickUpItemFromGround");
                    m_carriedItem = m_item.m_itemData;
                    if (m_item == null || Common.GetNView(m_item)?.IsValid() != true)
                    {
                        brain.Fire(Trigger.GroundItemLost);
                        return;
                    }
                    m_aiBase.UpdateAiStatus(State.PickUpItemFromGround, m_carriedItem.m_shared.m_name);
                    m_item.Pickup(aiBase.Character as Humanoid);
                    brain.Fire(Trigger.ItemFound);
                });

        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.SearchForRandomContainer))
            {
                //Common.Dbgl("Update SearchForContainer");
                Container newContainer = Common.FindRandomNearbyContainer(aiBase.Instance, m_knownContainers, AcceptedContainerNames, m_searchRadius);
                //Common.Dbgl($"Update SearchForContainer found new container {newContainer?.name}");
                if (newContainer != null)
                {
                    m_knownContainers.Push(newContainer);
                    m_startPosition = newContainer.transform.position;
                    aiBase.Brain.Fire(Trigger.ContainerFound);
                    aiBase.StopMoving();
                    //Common.Dbgl("Update SearchForContainer new container not null");
                    return;
                }
                Common.Invoke<BaseAI>(aiBase.Instance, "RandomMovement", dt, m_startPosition);
                aiBase.Brain.Fire(Trigger.ContainerNotFound);
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToContainer))
            {
                //Common.Dbgl($"State MoveToContainer: {KnownContainers.Peek().name}");
                if (m_knownContainers.Peek() == null)
                {
                    aiBase.StopMoving();
                    m_knownContainers.Pop();
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    //Common.Dbgl("Container = null");
                    return;
                }
                
                if (aiBase.MoveAndAvoidFire(m_knownContainers.Peek().transform.position, dt, 2f))
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerIsClose);
                    //Debug.Log($"{KnownContainers.Peek().name} is close");
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToStorageContainer))
            {
                //Common.Dbgl($"State MoveToContainer: {KnownContainers.Peek().name}");
                if (m_itemsDictionary[m_carriedItem.m_shared.m_name].container == null)
                {
                    aiBase.StopMoving();
                    m_knownContainers.Pop();
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    Common.Dbgl("Container = null");
                    return;
                }
                
                if (aiBase.MoveAndAvoidFire(m_itemsDictionary[m_carriedItem.m_shared.m_name].container.transform.position, dt, 2f))
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerIsClose);
                    Debug.Log($"{m_knownContainers.Peek().name} is close");
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.SearchItemsOnGround))
            {
                ItemDrop groundItem = Common.GetNearbyItem(m_aiBase.Instance, m_itemsDictionary.Keys, m_searchRadius);
                if (groundItem != null)
                {
                    m_aiBase.UpdateAiStatus(State.SearchItemsOnGround, groundItem.m_itemData.m_shared.m_name);
                    m_item = groundItem;
                    aiBase.Brain.Fire(Trigger.FoundGroundItem);
                    return;
                }
                Common.Invoke<BaseAI>(aiBase.Instance, "RandomMovement", dt, m_startPosition);
                aiBase.Brain.Fire(Trigger.GroundItemLost);
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToGroundItem))
            {
                if (m_item == null || m_item?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_item = null;
                    aiBase.StopMoving();
                    Debug.Log("GroundItem = null");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                if (aiBase.MoveAndAvoidFire(m_item.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    Debug.Log("GroundItem is close");
                    aiBase.Brain.Fire(Trigger.GroundItemIsClose);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.OpenContainer) || aiBase.Brain.IsInState(State.OpenStorageContainer))
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
