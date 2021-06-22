using Stateless;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    class ItemSortingBehaviour : IBehaviour
    {
        private const string Prefix = "RR_ISB";
        private class State
        {
            public const string Main = Prefix + "Main";
            public const string FindRandomTask = Prefix + "SearchForRandomContainer";
            public const string OpenContainer = Prefix + "OpenContainer";
            public const string OpenStorageContainer = Prefix + "OpenStorageContainer";
            public const string AddContainerItemsToItemDictionary = Prefix + "AddContainerItemsToItemDictionary";
            public const string UnloadIntoStorageContainer = Prefix + "UnloadIntoStorageContainer";
            public const string MoveToGroundItem = Prefix + "MoveToGroundItem";
            public const string MoveToPickable = Prefix + "MoveToPickable";
            public const string PickUpItemFromGround = Prefix + "PickUpItemFromGround";
            public const string MoveToDumpContainer = Prefix + "MoveToDumpContainer";
            public const string MoveToContainer = Prefix + "MoveToContainer";
            public const string MoveToStorageContainer = Prefix + "MoveToStorageContainer";
            public const string GetItemFromDumpContainer = Prefix + "GetItemFromDumpContainer";
            public const string OpenDumpContainer = Prefix + "OpenDumpContainer";
            public const string LookForNearbySign = Prefix + "LookForNearbySign";
            public const string ReadNearbySign = Prefix + "ReadNearbySign";
            public const string LookForNearbyStorageSign = Prefix + "LookForNearbyStorageSign";
            public const string ReadNearbyStorageSign = Prefix + "ReadNearbyStorageSign";
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
            public const string ContainerSearched = Prefix + "ContainerSearched";
            public const string Timeout = Prefix + "Timeout";
            public const string GroundItemIsClose = Prefix + "GroundItemIsClose";
            public const string FoundGroundItem = Prefix + "FoundGroundItem";
            public const string FoundPickable = Prefix + "FoundPickable";
            public const string GroundItemLost = Prefix + "GroundItemLost";
            public const string ItemSorted = Prefix + "ItemSorted";
            public const string SearchDumpContainer = Prefix + "SearchDumpChest";
            public const string ItemNotFound = Prefix + "ItemNotFound";
            public const string ContainerIsFull = Prefix + "ContainerIsFull";
            public const string NearbySignFound = Prefix + "NearbySignFound";
            public const string NearbySignNotFound = Prefix + "NearbySignNotFound";
            public const string SignHasBeenRead = Prefix + "SignHasBeenRead";
            public const string WaitForPickable = Prefix + "WaitForPickable";
        }

        // Input
        public string[] AcceptedContainerNames { get; set; } = new string[] { };

        // Output

        // Settings
        public float MaxSearchTime { get; set; } = 60f;
        public float RememberChestTime { get; set; } = 300f;
        public string StartState { get { return State.Main; } }
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float OpenChestDelay { get; private set; } = 1;
        public float PutItemInChestFailedRetryTimeout { get; set; } = 120f;
        public float SearchDumpContainerRetryTimeout { get; set; } = 60f;
        public Container DumpContainer { get; set; }
        public float ReadSignDelay { get; private set; } = 1;

        private Dictionary<string, IEnumerable<(Container container, int count)>> m_itemsDictionary;
        private Dictionary<string, float> m_putItemInContainerFailTimers;

        private ItemDrop m_item;
        private Pickable m_pickable;
        private Container m_container;
        private Sign m_nearbySign;
        private ItemDrop.ItemData m_carriedItem;
        private MobAIBase m_aiBase;
        private float m_openChestTimer;
        private float m_currentSearchTimeout;
        private int m_searchRadius;
        private MaxStack<Container> m_knownContainers;
        private Dictionary<string, float> m_knownContainersTimer;
        private Vector3 m_startPosition;
        private Vector3 m_lastPickupPosition;
        private float m_dumpContainerTimer;
        private MaxStack<(Container container, int count)> m_itemStorageStack;
        private float m_readNearbySignTimer;
        private float m_pickableTimer;

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            m_searchRadius = aiBase.Awareness * 5;
            m_knownContainers = new MaxStack<Container>(aiBase.Intelligence);
            m_knownContainersTimer = new Dictionary<string, float>();
            m_itemsDictionary = new Dictionary<string, IEnumerable<(Container container, int count)>>();
            m_putItemInContainerFailTimers = new Dictionary<string, float>();


            brain.Configure(State.Main)
                .InitialTransition(State.FindRandomTask)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered ItemSortingBehaviour", "Sorter");
                    m_startPosition = aiBase.Character.transform.position;
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.FindRandomTask)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerFound, State.MoveToContainer)
                .Permit(Trigger.FoundGroundItem, State.MoveToGroundItem)
                .Permit(Trigger.FoundPickable, State.MoveToPickable)
                .Permit(Trigger.SearchDumpContainer, State.MoveToDumpContainer)
                .OnEntry(t =>
                {
                    //Common.Dbgl("Entered SearchForRandomContainer", "Sorter");
                    m_currentSearchTimeout = Time.time + 2f;  //Delay before search initiates.
                });

            brain.Configure(State.MoveToContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerIsClose, State.LookForNearbySign)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToContainer);
                    m_currentSearchTimeout = Time.time + MaxSearchTime;
                    m_container = m_knownContainers.Peek();
                });

            brain.Configure(State.MoveToStorageContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerIsClose, State.LookForNearbyStorageSign)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToStorageContainer, m_carriedItem.m_shared.m_name);
                    m_currentSearchTimeout = Time.time + MaxSearchTime;
                    m_container = m_itemStorageStack.Peek().container;
                });

            brain.Configure(State.MoveToDumpContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerIsClose, State.OpenDumpContainer)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToDumpContainer);
                    m_container = DumpContainer;
                    m_currentSearchTimeout = Time.time + MaxSearchTime;
                });

            brain.Configure(State.LookForNearbySign)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .Permit(Trigger.NearbySignFound, State.ReadNearbySign)
                .Permit(Trigger.NearbySignNotFound, State.OpenContainer)
                .OnEntry(t => { });

            brain.Configure(State.LookForNearbyStorageSign)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .Permit(Trigger.NearbySignFound, State.ReadNearbyStorageSign)
                .Permit(Trigger.NearbySignNotFound, State.OpenStorageContainer)
                .OnEntry(t => { });

            brain.Configure(State.OpenContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.AddContainerItemsToItemDictionary)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_knownContainers.Peek().SetInUse(inUse: true);
                    m_openChestTimer = 0f;
                });

            brain.Configure(State.OpenStorageContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.UnloadIntoStorageContainer)
                .OnEntry(t =>
                {
                    m_itemStorageStack.Peek().container.SetInUse(inUse: true);
                    m_openChestTimer = 0f;
                });


            brain.Configure(State.OpenDumpContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.GetItemFromDumpContainer)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    DumpContainer.SetInUse(inUse: true);
                    m_openChestTimer = 0f;
                });

            brain.Configure(State.ReadNearbySign)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .Permit(Trigger.NearbySignNotFound, State.OpenContainer)
                .Permit(Trigger.SignHasBeenRead, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_readNearbySignTimer = 0f;
                    m_aiBase.UpdateAiStatus(State.ReadNearbySign);
                });

            brain.Configure(State.ReadNearbyStorageSign)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .Permit(Trigger.NearbySignNotFound, State.OpenStorageContainer)
                .Permit(Trigger.SignHasBeenRead, State.OpenStorageContainer)
                .OnEntry(t =>
                {
                    m_readNearbySignTimer = 0f;
                    m_aiBase.UpdateAiStatus(State.ReadNearbyStorageSign);
                });

            brain.Configure(State.AddContainerItemsToItemDictionary)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerSearched, State.FindRandomTask)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_container = m_knownContainers.Peek();
                })
                .OnExit(t =>
                {
                    m_container?.SetInUse(inUse: false);
                });

            brain.Configure(State.UnloadIntoStorageContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ItemSorted, State.FindRandomTask)
                .Permit(Trigger.ContainerIsFull, State.MoveToStorageContainer)
                .OnEntry(t =>
                {
                    m_container = m_itemStorageStack.Peek().container;
                })
                .OnExit(t =>
                {
                    m_container?.SetInUse(inUse: false);
                });

            brain.Configure(State.GetItemFromDumpContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ItemFound, State.MoveToStorageContainer)
                .Permit(Trigger.ItemNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                })
                .OnExit(t =>
               {
                   DumpContainer?.SetInUse(inUse: false);
               });

            brain.Configure(State.MoveToGroundItem)
                .SubstateOf(State.Main)
                .Permit(Trigger.GroundItemIsClose, State.PickUpItemFromGround)
                .Permit(Trigger.GroundItemLost, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToGroundItem, m_item.m_itemData.m_shared.m_name);
                    m_currentSearchTimeout = Time.time + MaxSearchTime;
                });

            brain.Configure(State.MoveToPickable)
                .SubstateOf(State.Main)
                .Permit(Trigger.WaitForPickable, State.WaitingForPickable)
                .Permit(Trigger.GroundItemLost, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToPickable, m_pickable.name);
                    m_currentSearchTimeout = Time.time + MaxSearchTime;
                });

            brain.Configure(State.WaitingForPickable)
                .SubstateOf(State.Main)
                .Permit(Trigger.GroundItemIsClose, State.PickUpItemFromGround)
                .Permit(Trigger.GroundItemLost, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.MoveToPickable, m_pickable.name);
                    m_pickableTimer = Time.time + 0.5f;
                });

            brain.Configure(State.PickUpItemFromGround)
                .SubstateOf(State.Main)
                .Permit(Trigger.ItemFound, State.MoveToStorageContainer)
                .Permit(Trigger.GroundItemLost, State.FindRandomTask)
                .OnEntry(t =>
                {
                    Common.Dbgl("PickUpItemFromGround", "Sorter");
                });
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            UpdatePutItemInContainerFailTimers();

            if (aiBase.Brain.IsInState(State.FindRandomTask))
            {
                if (m_currentSearchTimeout > Time.time) return;
                m_knownContainers.Remove(null);
                RemoveNullContainers();
                RemoveTimeoutedContainers();
                var knownContainers = m_knownContainers.ToList();
                if (DumpContainer != null)
                {
                    knownContainers.Add(DumpContainer);
                }
                Container newContainer = Common.FindRandomNearbyContainer(aiBase.Instance, knownContainers, AcceptedContainerNames, m_searchRadius);
                if (newContainer != null)
                {
                    m_knownContainers.Push(newContainer);
                    m_knownContainersTimer.Add(Common.GetOrCreateUniqueId(Common.GetNView(newContainer)), Time.time + RememberChestTime);
                    Common.Dbgl($"Update FindRandomTask new container with timeout at :{m_knownContainersTimer[Common.GetOrCreateUniqueId(Common.GetNView(newContainer))]}", "Sorter");
                    m_startPosition = newContainer.transform.position;
                    aiBase.Brain.Fire(Trigger.ContainerFound);
                    aiBase.StopMoving();
                    //Common.Dbgl("Update SearchForContainer new container not null", "Sorter");
                    return;
                }
                var wantedItems = m_itemsDictionary.Keys.Where(k => !m_putItemInContainerFailTimers.ContainsKey(k));
                ItemDrop groundItem = Common.GetNearbyItem(m_aiBase.Instance, wantedItems, m_searchRadius);
                if (groundItem != null)
                {
                    m_item = groundItem;
                    m_startPosition = groundItem.transform.position;
                    aiBase.Brain.Fire(Trigger.FoundGroundItem);
                    return;
                }
                Pickable pickable = Common.GetNearbyPickable(m_aiBase.Instance, m_aiBase.m_trainedAssignments, m_searchRadius, wantedItems);
                if (pickable != null)
                {
                    m_pickable = pickable;
                    m_startPosition = pickable.transform.position;
                    Common.Dbgl($"Found pickable: {m_pickable.GetHoverName()}", "Sorter");
                    aiBase.Brain.Fire(Trigger.FoundPickable);
                    return;
                }

                if (Time.time > m_dumpContainerTimer && DumpContainer != null)
                {
                    m_startPosition = DumpContainer.transform.position;
                    aiBase.Brain.Fire(Trigger.SearchDumpContainer);
                    return;
                }
                if (m_lastPickupPosition != Vector3.zero)
                {
                    if (aiBase.MoveAndAvoidFire(m_lastPickupPosition, dt, 1.0f))
                    {
                        m_lastPickupPosition = Vector3.zero;
                    }
                    return;
                }
                Common.Invoke<BaseAI>(aiBase.Instance, "RandomMovement", dt, m_startPosition);
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToContainer) || aiBase.Brain.IsInState(State.MoveToStorageContainer) || aiBase.Brain.IsInState(State.MoveToDumpContainer))
            {
                if (m_container == null)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                if (aiBase.MoveAndAvoidFire(m_container.transform.position, dt, 2f))
                {
                    aiBase.StopMoving();
                    if (!m_container.IsInUse())
                    {
                        aiBase.Brain.Fire(Trigger.ContainerIsClose);
                        return;
                    }
                }
                if (Time.time > m_currentSearchTimeout)
                {
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToGroundItem))
            {
                if (m_item == null || m_item?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_item = null;
                    aiBase.StopMoving();
                    Common.Dbgl("GroundItem = null", "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                if (aiBase.MoveAndAvoidFire(m_item.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    Common.Dbgl("GroundItem is close", "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemIsClose);
                }
                if (Time.time > m_currentSearchTimeout)
                {
                    Common.Dbgl($"Giving up on {m_item.m_itemData.m_shared.m_name}", "Sorter");
                    m_item = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToPickable))
            {
                if (m_pickable == null || m_pickable?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable = null", "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                if (aiBase.MoveAndAvoidFire(m_pickable.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable is close", "Sorter");
                    if (m_pickable.Interact((aiBase.Character as Humanoid), false))
                    {
                        aiBase.Brain.Fire(Trigger.WaitForPickable);
                        return;
                    }
                    m_currentSearchTimeout = 0f;
                }
                if (Time.time > m_currentSearchTimeout)
                {
                    Common.Dbgl($"Giving up on {m_pickable.gameObject.name}", "Sorter");
                    m_pickable = null;
                    aiBase.StopMoving();
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
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
                    Common.Dbgl("Pickable = null", "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                m_item = m_pickable.m_itemPrefab.GetComponent<ItemDrop>();
                Debug.Log($"m_item:{m_item?.name ?? "is null"}");
                m_startPosition = m_item.transform.position;
                aiBase.Brain.Fire(Trigger.GroundItemIsClose);
            }

            if (aiBase.Brain.IsInState(State.LookForNearbySign) || aiBase.Brain.IsInState(State.LookForNearbyStorageSign))
            {
                if (m_container == null)
                {
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                m_nearbySign = Common.FindClosestSign(m_container.transform.position, 1.5f);
                if ((bool)m_nearbySign)
                {
                    aiBase.Brain.Fire(Trigger.NearbySignFound);
                    return;
                }
                else
                {
                    aiBase.Brain.Fire(Trigger.NearbySignNotFound);
                    return;
                }
            }

            if (aiBase.Brain.IsInState(State.OpenContainer) || aiBase.Brain.IsInState(State.OpenStorageContainer) || aiBase.Brain.IsInState(State.OpenDumpContainer))
            {
                if (m_container == null)
                {
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                if ((m_openChestTimer += dt) > OpenChestDelay)
                {
                    Common.Dbgl("Open Container", "Sorter");
                    aiBase.Brain.Fire(Trigger.ContainerOpened);
                    if (m_knownContainersTimer.ContainsKey(Common.GetOrCreateUniqueId(Common.GetNView(m_container))))
                    {
                        m_knownContainersTimer[Common.GetOrCreateUniqueId(Common.GetNView(m_container))] = Time.time + RememberChestTime;
                        Common.Dbgl($"Updated timeout for {m_container.name}", "Sorter");
                    }
                    return;
                }
            }

            if (aiBase.Brain.IsInState(State.ReadNearbySign) || aiBase.Brain.IsInState(State.ReadNearbyStorageSign))
            {
                if (m_container == null)
                {
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                if (m_nearbySign == null)
                {
                    aiBase.Brain.Fire(Trigger.NearbySignNotFound);
                    return;
                }
                if ((m_readNearbySignTimer += dt) < ReadSignDelay)
                {
                    return;
                }

                Common.Dbgl($"NearbySign says:{m_nearbySign.GetText()}", "Sorter");
                ItemDrop.ItemData[] itemsOnSign = GetItemsOnSign(m_nearbySign);
                if (itemsOnSign.Length == 0)
                {
                    aiBase.Brain.Fire(Trigger.NearbySignNotFound);
                    return;
                }
                Common.Dbgl($"Deciphered {itemsOnSign.Length} items from nearbySign:{string.Join(",",itemsOnSign.Select(i => i.m_shared.m_name))}", "Sorter");
                RemoveContainerFromItemsDict(m_container);
                AddItemsToDictionary(itemsOnSign.ToDictionary(i => i.m_shared.m_name, e => 1000));
                aiBase.Brain.Fire(Trigger.SignHasBeenRead);
                return;
            }

            if (aiBase.Brain.IsInState(State.AddContainerItemsToItemDictionary))
            {
                if (m_container == null)
                {
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                List<ItemDrop.ItemData> foundItems = m_container.GetInventory().GetAllItems();
                if (foundItems.Any())
                {
                    Dictionary<string, int> chestInventory = new Dictionary<string, int>();
                    foreach (ItemDrop.ItemData item in foundItems)
                    {
                        string key = Common.GetPrefabName(item.m_shared.m_name);
                        Common.Dbgl($"Key: {key}", "Sorter");
                        if (chestInventory.ContainsKey(key))
                        {
                            chestInventory[key] += item.m_stack;
                        }
                        else
                        {
                            chestInventory.Add(key, item.m_stack);
                        }
                    }
                    AddItemsToDictionary(chestInventory);
                }
                aiBase.Brain.Fire(Trigger.ContainerSearched);
            }

            if (aiBase.Brain.IsInState(State.UnloadIntoStorageContainer))
            {
                var mob = (aiBase.Character as Humanoid);
                m_container.SetInUse(inUse: false);
                Common.Dbgl($"Unload {m_carriedItem.m_shared.m_name} exists in {m_itemStorageStack.Count()} containers", "Sorter");

                if (m_container.GetInventory().CanAddItem(m_carriedItem))
                {
                    Common.Dbgl($"Putting {m_carriedItem.m_shared.m_name} in container", "Sorter");
                    mob.UnequipItem(m_carriedItem);
                    m_container.GetInventory().MoveItemToThis(mob.GetInventory(), m_carriedItem);
                }
                else if (m_itemStorageStack.Count() > 1)
                {
                    Common.Dbgl($"Container full", "Sorter");
                    m_itemStorageStack.Pop();
                    m_container = m_itemStorageStack.Peek().container;
                    aiBase.Brain.Fire(Trigger.ContainerIsFull);
                    return;
                }
                else
                {
                    Common.Dbgl($"Can't put {m_carriedItem.m_shared.m_name} in container, drop on ground", "Sorter");
                    mob.DropItem((aiBase.Character as Humanoid).GetInventory(), m_carriedItem, m_carriedItem.m_stack);
                    m_putItemInContainerFailTimers.Add(m_carriedItem.m_shared.m_name, Time.time + PutItemInChestFailedRetryTimeout);
                    Debug.LogWarning($"Put {m_carriedItem.m_shared.m_name} on timeout");
                }
                Common.Dbgl($"Item Keys: {string.Join(",", m_itemsDictionary.Keys)}", "Sorter");
                m_carriedItem = null;
                m_itemStorageStack = null;
                aiBase.Brain.Fire(Trigger.ItemSorted);
            }

            if (aiBase.Brain.IsInState(State.GetItemFromDumpContainer))
            {
                if (DumpContainer == null)
                {
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                    return;
                }
                m_carriedItem = null;
                foreach (var item in DumpContainer.GetInventory().GetAllItems())
                {
                    if (m_putItemInContainerFailTimers.ContainsKey(item.m_shared.m_name)) continue;
                    if (m_itemsDictionary.ContainsKey(item.m_shared.m_name))
                    {
                        m_carriedItem = item;
                        (aiBase.Character as Humanoid).GetInventory().MoveItemToThis(DumpContainer.GetInventory(), item);
                        (aiBase.Character as Humanoid).EquipItem(item);
                        Common.Invoke<Container>(DumpContainer, "Save");
                        Common.Invoke<Inventory>(DumpContainer.GetInventory(), "Changed");

                        m_aiBase.UpdateAiStatus(State.GetItemFromDumpContainer, m_carriedItem.m_shared.m_name);

                        var itemContainers = m_itemsDictionary[item.m_shared.m_name];
                        m_itemStorageStack = new MaxStack<(Container container, int count)>(itemContainers);
                        aiBase.Brain.Fire(Trigger.ItemFound);
                        return;
                    }
                }
                if (m_carriedItem == null)
                {
                    m_dumpContainerTimer = Time.time + SearchDumpContainerRetryTimeout;
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                    return;
                }
            }

            if (aiBase.Brain.IsInState(State.PickUpItemFromGround))
            {
                if (m_item == null || Common.GetNView(m_item)?.IsValid() != true)
                {
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                m_carriedItem = m_item.m_itemData;
                m_aiBase.UpdateAiStatus(State.PickUpItemFromGround, m_carriedItem.m_shared.m_name);
                m_itemStorageStack = new MaxStack<(Container container, int count)>(m_itemsDictionary[m_carriedItem.m_shared.m_name]);
                Common.Dbgl($"Pickup {m_carriedItem.m_shared.m_name} exists in {m_itemStorageStack.Count()} containers", "Sorter");
                m_item.Pickup(aiBase.Character as Humanoid);
                (aiBase.Character as Humanoid).EquipItem(m_carriedItem);
                m_lastPickupPosition = aiBase.Character.transform.position;
                aiBase.Brain.Fire(Trigger.ItemFound);
            }
        }

        private void RemoveTimeoutedContainers()
        {
            var activeContainers = new MaxStack<Container>(m_knownContainers);
            foreach (Container container in m_knownContainers)
            {
                if (m_knownContainersTimer.ContainsKey(Common.GetOrCreateUniqueId(Common.GetNView(container))) && m_knownContainersTimer[Common.GetOrCreateUniqueId(Common.GetNView(container))] < Time.time)
                {
                    activeContainers.Remove(container);
                    m_knownContainersTimer.Remove(Common.GetOrCreateUniqueId(Common.GetNView(container)));
                    Common.Dbgl("Remove timeout containers from known containers.", "Sorter");
                }
            }
        }

        private void RemoveContainerFromItemsDict(Container container)
        {
            var newItemsDict = new Dictionary<string, IEnumerable<(Container, int)>>();
            foreach (string key in m_itemsDictionary.Keys)
            {
                var containersForItem = m_itemsDictionary[key].Where(c => c.container != null);
                containersForItem = containersForItem.Where(c => c.container != container);
                if (containersForItem.Count() > 0)
                {
                    newItemsDict.Add(key, containersForItem);
                }
            }
            m_itemsDictionary = newItemsDict;
        }

        private void RemoveNullContainers()
        {
            var newItemsDict = new Dictionary<string, IEnumerable<(Container, int)>>();
            foreach (string key in m_itemsDictionary.Keys)
            {
                var containersForItem = m_itemsDictionary[key].Where(c => c.container != null);
                if (m_itemsDictionary[key].Count() > 0)
                {
                    newItemsDict.Add(key, containersForItem);
                }
            }
            m_itemsDictionary = newItemsDict;
        }

        private void AddItemsToDictionary(Dictionary<string, int> items)
        {
            foreach (KeyValuePair<string, int> item in items)
            {
                if (m_itemsDictionary.ContainsKey(item.Key))
                {
                    string currentContainerId = Common.GetOrCreateUniqueId(Common.GetNView(m_container));
                    var containerExists = m_itemsDictionary[item.Key].Any(s => Common.GetOrCreateUniqueId(Common.GetNView(s.container)) == currentContainerId);
                    if (containerExists)
                    {
                        var container = m_itemsDictionary[item.Key].First(s => Common.GetOrCreateUniqueId(Common.GetNView(s.container)) == currentContainerId);
                        container.count = item.Value;
                    }
                    else
                    {
                        m_itemsDictionary[item.Key] = m_itemsDictionary[item.Key].Append((m_container, item.Value));
                    }
                    m_itemsDictionary[item.Key] = m_itemsDictionary[item.Key].OrderByDescending(c => c.count);
                    Common.Dbgl($"{item.Key} exists in {m_itemsDictionary[item.Key].Count()} containers", "Sorter");
                }
                else if (!m_itemsDictionary.ContainsKey(item.Key))
                {
                    m_itemsDictionary.Add(item.Key, new List<(Container, int)> { (m_container, item.Value) });
                    Common.Dbgl($"Added {item.Key} to dict", "Sorter");
                }
            }
        }

        private ItemDrop.ItemData[] GetItemsOnSign(Sign sign)
        {
            if (!(bool)sign) return new ItemDrop.ItemData[] { };
            var itemNames = sign.GetText().Split(',');
            return ObjectDB.instance.m_items
                .Select(g => g.GetComponent<ItemDrop>())
                .Where(i => itemNames.Contains(Localization.instance.Localize(i.m_itemData.m_shared.m_name)))
                .Select(i => i.m_itemData)
                .Distinct(new Helpers.ItemDataComparer())
                .ToArray();
        }

        private void UpdatePutItemInContainerFailTimers()
        {
            var keys = m_putItemInContainerFailTimers.Keys.ToArray();
            foreach (var key in keys)
            {
                if (Time.time > m_putItemInContainerFailTimers[key])
                {
                    Debug.LogWarning($"remove {key} from timeout");
                    m_putItemInContainerFailTimers.Remove(key);
                }
            }
        }
    }
}
