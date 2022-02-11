using Stateless;
using System;
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
            public const string FindRandomTask = Prefix + "FindRandomTask";
            public const string OpenContainer = Prefix + "OpenContainer";
            public const string OpenStorageContainer = Prefix + "OpenStorageContainer";
            public const string AddContainerItemsToItemDictionary = Prefix + "AddContainerItemsToItemDictionary";
            public const string UnloadIntoStorageContainer = Prefix + "UnloadIntoStorageContainer";
            public const string MoveToGroundItem = Prefix + "MoveToGroundItem";
            public const string MoveToPickable = Prefix + "MoveToPickable";
            public const string PickUpItemFromGround = Prefix + "PickUpItemFromGround";
            public const string PickUpAnotherItemFromGround = Prefix + "PickUpItemAnotherFromGround";
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
            public const string FindingExtractionTask = Prefix + "FindingExtractionTask";
            public const string HarvestingCrop = Prefix + "HarvestingCrop";
            public const string HarvestIngFailed = Prefix + "HarvestingFailed";
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
            public const string FindExtractionTask = Prefix + "FindExtractionTask";
            public const string HarvestCrop = Prefix + "HarvestCrop";
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
        public string SearchForItemState { get; set; }
        public float OpenChestDelay { get; private set; } = 1;
        public float PutItemInChestFailedRetryTimeout { get; set; } = 120f;
        public float SearchDumpContainerRetryTimeout { get; set; } = 60f;
        public float SearchForExtractionTaskTimeout { get; set; } = 60f;
        public float FindRandomTaskDelay { get; set; } = 1.0f;

        public StorageContainer DumpContainer 
        { 
            get => m_dumpContainer; 
            set  
            { 
                m_dumpContainer = value;
                if (value != null)
                {
                    RemoveContainerFromItemsDict(value);
                }
            }
        }
        public float ReadSignDelay { get; private set; } = 1;
        public float TryFarmingAgainTimeout { get; private set; } = 30;

        private ExtractionBehaviour m_extractionBehaviour;
        private BasicFarmingBehaviour m_basicFarmingBehaviour;

        private Dictionary<string, IEnumerable<(StorageContainer container, int count)>> m_itemsDictionary;
        private Dictionary<string, float> m_putItemInContainerFailTimers;

        //Timers
        private float m_openChestTimer;
        private float m_currentSearchTimeout;
        private float m_dumpContainerTimer;
        private float m_readNearbySignTimer;
        private float m_pickableTimer;
        private float m_extractionBehaviourTimer;
        private float m_findRandomTaskTimer;
        private float m_basicFarmingBehaviourTimer;

        private ItemDrop m_item;
        private Pickable m_pickable;
        private StorageContainer m_container;
        private Sign m_nearbySign;
        private ItemDrop.ItemData m_carriedItem;
        private MobAIBase m_aiBase;
        private int m_searchRadius;
        private MaxStack<StorageContainer> m_knownContainers;
        private Vector3 m_startPosition;
        private Vector3 m_lastPickupPosition;
        private MaxStack<(StorageContainer container, int count)> m_itemStorageStack;
        private StorageContainer m_dumpContainer;

        public void SaveItemDictionary()
        {
            var serializedDict = string.Join(string.Empty, m_itemsDictionary.Select(d => $"[{d.Key}:{string.Join("", d.Value.Select(c => $"[{c.container.Serialize()};{c.count}]").ToArray())}]").ToArray());
            //Debug.Log($"save:{serializedDict}");
            m_aiBase.NView.GetZDO().Set(Constants.Z_SorterItemDict, serializedDict);
        }

        public void LoadItemDictionary()
        {
            m_itemsDictionary = new Dictionary<string, IEnumerable<(StorageContainer container, int count)>>();
            if (!m_aiBase.NView.IsValid()) return;

            var serializedDict = m_aiBase.NView.GetZDO().GetString(Constants.Z_SorterItemDict);
            if (string.IsNullOrEmpty(serializedDict)) return;
            try
            {
                foreach (var item in serializedDict.SplitBySqBrackets())
                {
                    var itemData = item.Split(':');
                    string key = itemData.First();
                    var containerList = new List<(StorageContainer, int)>();
                    foreach (var c in item.SplitBySqBrackets())
                    {
                        var sc = StorageContainer.DeSerialize(c.Split(';').First());
                        var num = int.Parse(c.Split(';').Last());
                        containerList.Add((sc, num));
                    }
                    m_itemsDictionary.Add(key, containerList);
                }
                Common.Dbgl($"{m_aiBase.NView.GetZDO().GetString(Constants.Z_GivenName)}:Loaded {m_itemsDictionary.Count} items",true, "Sorter");
            }
            catch (Exception)
            {
                Common.Dbgl($"Failed to load items dictionary", true);
            }
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            m_searchRadius = aiBase.Awareness * 5;
            m_knownContainers = new MaxStack<StorageContainer>(aiBase.Intelligence);
            m_putItemInContainerFailTimers = new Dictionary<string, float>();
            LoadItemDictionary();
            foreach (var container in m_itemsDictionary.Values.SelectMany(i => i.Select(c => c.container))?.Distinct(new Helpers.StorageContainerComparer()))
            {
                m_knownContainers.Push(container);
            }
            m_extractionBehaviour = new ExtractionBehaviour
            {
                SuccessState = State.FindRandomTask,
                FailState = State.FindRandomTask
            };
            m_extractionBehaviour.Configure(aiBase, aiBase.Brain, State.FindingExtractionTask);

            m_basicFarmingBehaviour = new BasicFarmingBehaviour
            {
                SuccessState = State.FindRandomTask,
                FailState = State.HarvestIngFailed,
                AcceptedContainerNames = AcceptedContainerNames,
                //KnownContainers = m_knownContainers
            };
            m_basicFarmingBehaviour.Init();
            m_basicFarmingBehaviour.Configure(aiBase, aiBase.Brain, State.HarvestingCrop);
            //m_basicFarmingBehaviour.SearchForItemsState = SearchForItemState;

            brain.Configure(State.Main)
                .InitialTransition(State.FindRandomTask)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered ItemSortingBehaviour", true, "Sorter");
                    m_startPosition = aiBase.Character.transform.position;
                })
                .OnExit(t =>
                {
                    m_aiBase.UpdateAiStatus(string.Empty);
                });

            brain.Configure(State.FindRandomTask)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerFound, State.MoveToContainer)
                .Permit(Trigger.FoundGroundItem, State.MoveToGroundItem)
                .Permit(Trigger.FoundPickable, State.MoveToPickable)
                .Permit(Trigger.SearchDumpContainer, State.MoveToDumpContainer)
                .Permit(Trigger.ItemFound, State.MoveToStorageContainer)
                .Permit(Trigger.FindExtractionTask, State.FindingExtractionTask)
                .Permit(Trigger.HarvestCrop, State.HarvestingCrop)
                .OnEntry(t =>
                {
                    //Common.Dbgl("Entered SearchForRandomContainer", "Sorter");
                    m_findRandomTaskTimer = Time.time + FindRandomTaskDelay;  //Delay before search initiates.
                    m_aiBase.UpdateAiStatus(State.FindRandomTask);
                });

            brain.Configure(State.FindingExtractionTask)
                .SubstateOf(State.Main)
                .InitialTransition(m_extractionBehaviour.StartState)
                .OnEntry(t =>
                {
                    m_extractionBehaviourTimer = Time.time + SearchForExtractionTaskTimeout;
                });

            brain.Configure(State.HarvestingCrop)
                .SubstateOf(State.Main)
                .InitialTransition(m_basicFarmingBehaviour.StartState)
                .OnEntry(t =>
                {
                    m_aiBase.UpdateAiStatus(State.HarvestingCrop);
                });

            brain.Configure(State.HarvestIngFailed)
                .SubstateOf(State.Main)
                .Permit(Trigger.Failed, State.FindRandomTask)
                .OnEntry(t =>
                {
                    m_basicFarmingBehaviourTimer = Time.time + TryFarmingAgainTimeout;
                    brain.Fire(Trigger.Failed);
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
                    m_knownContainers.Peek().Container.SetInUse(inUse: true);
                    m_openChestTimer = 0f;
                });

            brain.Configure(State.OpenStorageContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.UnloadIntoStorageContainer)
                .OnEntry(t =>
                {
                    m_itemStorageStack.Peek().container.Container.SetInUse(inUse: true);
                    m_openChestTimer = 0f;
                });


            brain.Configure(State.OpenDumpContainer)
                .SubstateOf(State.Main)
                .Permit(Trigger.ContainerOpened, State.GetItemFromDumpContainer)
                .Permit(Trigger.ContainerNotFound, State.FindRandomTask)
                .OnEntry(t =>
                {
                    DumpContainer.Container.SetInUse(inUse: true);
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
                    m_container?.Container.SetInUse(inUse: false);
                    SaveItemDictionary();
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
                    m_container?.Container.SetInUse(inUse: false);
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
                   DumpContainer?.Container.SetInUse(inUse: false);
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
                    m_pickableTimer = Time.time + 0.7f;
                });

            brain.Configure(State.PickUpItemFromGround)
                .SubstateOf(State.Main)
                .Permit(Trigger.ItemFound, State.PickUpAnotherItemFromGround)
                .Permit(Trigger.GroundItemLost, State.FindRandomTask)
                .OnEntry(t =>
                {
                    Common.Dbgl("PickUpItemFromGround", true, "Sorter");
                });
            brain.Configure(State.PickUpAnotherItemFromGround)
                .SubstateOf(State.Main)
                .Permit(Trigger.ItemFound, State.MoveToGroundItem)
                .Permit(Trigger.ItemNotFound, State.MoveToStorageContainer)
                .OnEntry(t =>
                {
                    Common.Dbgl("PickUpAnotherItemFromGround", true, "Sorter");
                });
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            UpdatePutItemInContainerFailTimers();

            if (aiBase.Brain.IsInState(State.FindRandomTask))
            {
                if (m_findRandomTaskTimer < Time.time)
                {
                    m_findRandomTaskTimer = Time.time + FindRandomTaskDelay;
                    RemoveNullContainers();
                    RemoveTimeoutedContainers();
                    if (HaveItemInInventory(m_item?.m_itemData) && m_itemsDictionary.ContainsKey(m_item.m_itemData.m_shared.m_name))
                    {
                        //Debug.LogWarning("resume store item task!");
                        m_carriedItem = m_item.m_itemData;
                        (aiBase.Character as Humanoid).EquipItem(m_carriedItem);
                        m_itemStorageStack = new MaxStack<(StorageContainer container, int count)>(m_itemsDictionary[m_carriedItem.m_shared.m_name]);
                        aiBase.Brain.Fire(Trigger.ItemFound);
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
                        if (m_basicFarmingBehaviour.IsHarvestableCrop(pickable))
                        {

                            if (Time.time > m_basicFarmingBehaviourTimer)
                            {
                                m_basicFarmingBehaviour.PickableToHarvest = pickable;
                                Common.Dbgl($"Found harvestable crop: {pickable.GetHoverName()}", true, "Sorter");
                                aiBase.Brain.Fire(Trigger.HarvestCrop);
                            }
                        }
                        else
                        {
                            m_pickable = pickable;
                            m_startPosition = pickable.transform.position;
                            Common.Dbgl($"Found pickable: {m_pickable.GetHoverName()}", true, "Sorter");
                            aiBase.Brain.Fire(Trigger.FoundPickable);
                        }
                        return;
                    }

                    if (Time.time > m_dumpContainerTimer && DumpContainer != null)
                    {
                        m_startPosition = DumpContainer.Position;
                        aiBase.Brain.Fire(Trigger.SearchDumpContainer);
                        return;
                    }

                    if (Time.time > m_extractionBehaviourTimer)
                    {
                        m_extractionBehaviour.AcceptedAssignments = Assignment.AssignmentTypes.Where(t => t.IsExtractable && m_itemsDictionary.Keys.Contains(t.ExtractableItemName)).ToArray();
                        aiBase.Brain.Fire(Trigger.FindExtractionTask);
                        return;
                    }

                    var knownContainers = m_knownContainers.ToList();
                    if (DumpContainer != null && !knownContainers.Contains(DumpContainer))
                    {
                        knownContainers.Add(DumpContainer);
                    }
                    Container newContainer = Common.FindRandomNearbyContainer(aiBase.Instance, knownContainers.Select(kc => kc.Container), AcceptedContainerNames, m_searchRadius, Vector3.zero);
                    if (newContainer != null)
                    {
                        var storageContainer = new StorageContainer(newContainer, Time.time + RememberChestTime);
                        m_knownContainers.Push(storageContainer);
                        Common.Dbgl($"Update FindRandomTask new container with timeout at :{storageContainer.Timestamp}", true, "Sorter");
                        m_startPosition = storageContainer.Position;
                        aiBase.Brain.Fire(Trigger.ContainerFound);
                        aiBase.StopMoving();
                        //Common.Dbgl("Update SearchForContainer new container not null", "Sorter");
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
                }
                Common.Invoke<BaseAI>(aiBase.Instance, "RandomMovement", dt, m_startPosition);
                return;
            }

            if (aiBase.Brain.IsInState(State.FindingExtractionTask))
            {
                m_extractionBehaviour.Update(aiBase, dt);
                return;
            }

            if (aiBase.Brain.IsInState(State.HarvestingCrop))
            {
                m_basicFarmingBehaviour.Update(aiBase, dt);
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
                if (aiBase.MoveAndAvoidFire(m_container.Position, dt, 2f))
                {
                    aiBase.StopMoving();
                    if (m_container.Container == null)
                    {
                        Common.Dbgl($"{aiBase.Character.GetHoverName()}({aiBase.Character.transform.position}):Is near container {m_container.UniqueId}({m_container.Position}) but it is still null", true, "Sorter");
                        return;
                    }
                    if (!m_container.Container.IsInUse())
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
                    Common.Dbgl("GroundItem = null", true, "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                if (aiBase.MoveAndAvoidFire(m_item.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    Common.Dbgl("GroundItem is close", true, "Sorter");
                    m_item.RequestOwn();
                    aiBase.Brain.Fire(Trigger.GroundItemIsClose);
                }
                if (Time.time > m_currentSearchTimeout)
                {
                    Common.Dbgl($"Giving up on {m_item.m_itemData.m_shared.m_name}", true, "Sorter");
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
                    Common.Dbgl("Pickable = null", true, "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                if (aiBase.MoveAndAvoidFire(m_pickable.transform.position, dt, 1.5f))
                {
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable is close", true, "Sorter");
                    if (m_pickable.Interact((aiBase.Character as Humanoid), false,  false))
                    {
                        aiBase.Brain.Fire(Trigger.WaitForPickable);
                        return;
                    }
                    m_currentSearchTimeout = 0f;
                }
                if (Time.time > m_currentSearchTimeout)
                {
                    Common.Dbgl($"Giving up on {m_pickable.gameObject.name}", true, "Sorter");
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
                    Common.Dbgl("Pickable = null", true, "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                m_item = Common.GetClosestItem(aiBase.Instance, 3, m_pickable.m_itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name, false);
                if (m_item == null)
                {
                    m_pickable = null;
                    aiBase.StopMoving();
                    Common.Dbgl("Pickable dropped item not found", true, "Sorter");
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                //m_item = m_pickable.m_itemPrefab.GetComponent<ItemDrop>();
                Common.Dbgl($"m_item:{m_item?.name ?? "is null"}", true);
                m_startPosition = m_item.transform.position;
                m_item.RequestOwn();
                aiBase.Brain.Fire(Trigger.GroundItemIsClose);
            }

            if (aiBase.Brain.IsInState(State.LookForNearbySign) || aiBase.Brain.IsInState(State.LookForNearbyStorageSign))
            {
                if (m_container?.Container == null)
                {
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                m_nearbySign = Common.FindClosestSign(m_container.Position, 1.5f);
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
                if (m_container?.Container == null)
                {
                    m_knownContainers.Remove(m_container);
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                if ((m_openChestTimer += dt) > OpenChestDelay)
                {
                    Common.Dbgl("Open Container", true, "Sorter");
                    m_container.Timestamp = Time.time + RememberChestTime;
                    Common.Dbgl($"Updated timeout for {m_container.Container.name}", true, "Sorter");
                    aiBase.Brain.Fire(Trigger.ContainerOpened);
                    return;
                }
            }

            if (aiBase.Brain.IsInState(State.ReadNearbySign) || aiBase.Brain.IsInState(State.ReadNearbyStorageSign))
            {
                if (m_container?.Container == null)
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

                Common.Dbgl($"NearbySign says:{m_nearbySign.GetText()}", true, "Sorter");
                ItemDrop.ItemData[] itemsOnSign = GetItemsOnSign(m_nearbySign);
                if (itemsOnSign.Length == 0)
                {
                    aiBase.Brain.Fire(Trigger.NearbySignNotFound);
                    return;
                }
                Common.Dbgl($"Deciphered {itemsOnSign.Length} items from nearbySign:{string.Join(",", itemsOnSign.Select(i => i.m_shared.m_name))}", true, "Sorter");
                RemoveContainerFromItemsDict(m_container);
                AddItemsToDictionary(itemsOnSign.ToDictionary(i => i.m_shared.m_name, e => 1000));
                SaveItemDictionary();
                aiBase.Brain.Fire(Trigger.SignHasBeenRead);
                return;
            }

            if (aiBase.Brain.IsInState(State.AddContainerItemsToItemDictionary))
            {
                if (m_container?.Container == null)
                {
                    aiBase.Brain.Fire(Trigger.ContainerNotFound);
                    return;
                }
                List<ItemDrop.ItemData> foundItems = m_container.Container.GetInventory().GetAllItems();
                if (foundItems.Any())
                {
                    Dictionary<string, int> chestInventory = new Dictionary<string, int>();
                    foreach (ItemDrop.ItemData item in foundItems)
                    {
                        string key = Common.GetPrefabName(item.m_shared.m_name);
                        Common.Dbgl($"Key: {key}", true, "Sorter");
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
                m_container.Container.SetInUse(inUse: false);
                Common.Dbgl($"Unload {m_carriedItem.m_shared.m_name} exists in {m_itemStorageStack.Count()} containers", true, "Sorter");
                var itemToStore = (aiBase.Character as Humanoid).GetInventory().GetAllItems().FirstOrDefault(i => i.m_shared.m_name == m_carriedItem.m_shared.m_name);

                if (m_container.Container.GetInventory().CanAddItem(itemToStore))
                {
                    Common.Dbgl($"Putting {itemToStore.m_stack} {itemToStore.m_shared.m_name} in container", true, "Sorter");
                    mob.UnequipItem(m_carriedItem);
                    m_container.Container.GetInventory().MoveItemToThis(mob.GetInventory(), itemToStore);
                }
                else if (m_itemStorageStack.Count() > 1)
                {
                    Common.Dbgl($"Container full", true, "Sorter");
                    m_itemStorageStack.Pop();
                    m_container = m_itemStorageStack.Peek().container;
                    aiBase.Brain.Fire(Trigger.ContainerIsFull);
                    return;
                }
                else
                {
                    Common.Dbgl($"Can't put {m_carriedItem.m_shared.m_name} in container, drop on ground", true, "Sorter");
                    mob.DropItem((aiBase.Character as Humanoid).GetInventory(), m_carriedItem, m_carriedItem.m_stack);
                    m_putItemInContainerFailTimers.Add(m_carriedItem.m_shared.m_name, Time.time + PutItemInChestFailedRetryTimeout);
                    //Debug.LogWarning($"Put {m_carriedItem.m_shared.m_name} on timeout");
                }
                Common.Dbgl($"Item Keys: {string.Join(",", m_itemsDictionary.Keys)}", true, "Sorter");
                m_carriedItem = null;
                m_itemStorageStack = null;
                aiBase.Brain.Fire(Trigger.ItemSorted);
            }

            if (aiBase.Brain.IsInState(State.GetItemFromDumpContainer))
            {
                if (DumpContainer?.Container == null)
                {
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                    return;
                }
                m_carriedItem = null;
                foreach (var item in DumpContainer.Container.GetInventory().GetAllItems())
                {
                    if (m_putItemInContainerFailTimers.ContainsKey(item.m_shared.m_name)) continue;
                    if (m_itemsDictionary.ContainsKey(item.m_shared.m_name))
                    {
                        m_carriedItem = item;
                        (aiBase.Character as Humanoid).GetInventory().MoveItemToThis(DumpContainer.Container.GetInventory(), item);
                        (aiBase.Character as Humanoid).EquipItem(item);
                        Common.Invoke<Container>(DumpContainer.Container, "Save");
                        Common.Invoke<Inventory>(DumpContainer.Container.GetInventory(), "Changed");

                        m_aiBase.UpdateAiStatus(State.GetItemFromDumpContainer, m_carriedItem.m_shared.m_name);

                        var itemContainers = m_itemsDictionary[item.m_shared.m_name];
                        m_itemStorageStack = new MaxStack<(StorageContainer container, int count)>(itemContainers);
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
                if (m_item == null || Common.GetNView(m_item)?.IsValid() != true || Common.GetNView(m_item)?.HasOwner() != true)
                {
                    Common.Dbgl($"GroundItem lost: {m_item == null}, {Common.GetNView(m_item)?.IsValid() != true}, {Common.GetNView(m_item)?.HasOwner() != true}", true);
                    aiBase.Brain.Fire(Trigger.GroundItemLost);
                    return;
                }
                if (!m_item.CanPickup())
                {
                    Common.Dbgl($"Can't pickup {m_item?.name ?? "thing"}", true);
                    return;
                }
                m_carriedItem = m_item.m_itemData;
                m_aiBase.UpdateAiStatus(State.PickUpItemFromGround, m_carriedItem.m_shared.m_name);
                m_itemStorageStack = new MaxStack<(StorageContainer container, int count)>(m_itemsDictionary[m_carriedItem.m_shared.m_name]);
                Common.Dbgl($"Pickup {m_carriedItem.m_shared.m_name} exists in {m_itemStorageStack.Count()} containers", true, "Sorter");
                m_item.Pickup(aiBase.Character as Humanoid);
                (aiBase.Character as Humanoid).EquipItem(m_carriedItem);
                m_lastPickupPosition = aiBase.Character.transform.position;
                aiBase.Brain.Fire(Trigger.ItemFound);
            }

            if (aiBase.Brain.IsInState(State.PickUpAnotherItemFromGround))
            {
                var existingInventoryItem = (aiBase.Character as Humanoid).GetInventory().GetAllItems().FirstOrDefault(i => i.m_shared.m_name == m_carriedItem.m_shared.m_name);
                if (existingInventoryItem == null)
                {
                    //Debug.Log("existingInventoryItem is null");
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                    return;
                }
                if (!(bool)m_item)
                {
                    //Debug.Log("m_item is null");
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                    return;
                }
                //if (!Common.GetNView(m_item).IsValid())
                //{
                //    Debug.Log("not valid");
                //    aiBase.Brain.Fire(Trigger.ItemNotFound);
                //    return;
                //}
                //if (!Common.GetNView(m_item)?.IsOwner() ?? true)
                //{
                //    Debug.Log("not the owner");
                //    aiBase.Brain.Fire(Trigger.ItemNotFound);
                //    return;
                //}
                if (existingInventoryItem?.m_stack >= existingInventoryItem.m_shared.m_maxStackSize)
                {
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                    return;
                }
                var anotherItem = Common.GetClosestItem(aiBase.Instance, m_searchRadius, m_carriedItem.m_shared.m_name);
                if ((bool)anotherItem)
                {
                    m_item = anotherItem;
                    m_aiBase.UpdateAiStatus(State.PickUpAnotherItemFromGround, m_carriedItem.m_shared.m_name);
                    aiBase.Brain.Fire(Trigger.ItemFound);
                    return;
                }
                else
                {
                    aiBase.Brain.Fire(Trigger.ItemNotFound);
                    return;
                }
            }
        }

        private bool HaveItemInInventory(ItemDrop.ItemData m_carriedItem)
        {
            if (m_carriedItem == null) return false;
            return (m_aiBase.Character as Humanoid).GetInventory().HaveItem(m_carriedItem.m_shared.m_name);
        }

        private void RemoveTimeoutedContainers()
        {
            var activeContainers = new MaxStack<StorageContainer>(m_knownContainers);
            foreach (StorageContainer container in activeContainers)
            {
                if (container.Timestamp < Time.time)
                {
                    m_knownContainers.Remove(container);
                    Common.Dbgl($"Container {container.UniqueId} timeouted", true, "Sorter");
                }
            }
        }

        private void RemoveContainerFromItemsDict(StorageContainer container)
        {
            var newItemsDict = new Dictionary<string, IEnumerable<(StorageContainer, int)>>();
            foreach (string key in m_itemsDictionary.Keys)
            {
                var containersForItem = m_itemsDictionary[key].Where(c => c.container.Container != null);
                containersForItem = containersForItem.Where(c => c.container.UniqueId != container.UniqueId);
                if (containersForItem.Count() > 0)
                {
                    newItemsDict.Add(key, containersForItem);
                }
            }
            m_itemsDictionary = newItemsDict;
        }

        private void RemoveNullContainers()
        {
            var newItemsDict = new Dictionary<string, IEnumerable<(StorageContainer, int)>>();
            foreach (string key in m_itemsDictionary.Keys)
            {
                var containersForItem = m_itemsDictionary[key].Where(c => c.container?.Container != null);
                if (containersForItem.Count() > 0)
                {
                    newItemsDict.Add(key, containersForItem);
                }
                foreach (var (container, count) in m_itemsDictionary[key].Where(c => c.container?.Container == null))
                {
                    if (m_knownContainers.Contains(container))
                    {
                        m_knownContainers.Remove(container);
                    }
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
                    var containerExists = m_itemsDictionary[item.Key].Any(s => s.container.UniqueId == m_container.UniqueId);
                    if (containerExists)
                    {
                        var container = m_itemsDictionary[item.Key].First(s => s.container.UniqueId == m_container.UniqueId);
                        container.count = item.Value;
                    }
                    else
                    {
                        m_itemsDictionary[item.Key] = m_itemsDictionary[item.Key].Append((m_container, item.Value));
                    }
                    m_itemsDictionary[item.Key] = m_itemsDictionary[item.Key].OrderByDescending(c => c.count);
                    Common.Dbgl($"{item.Key} exists in {m_itemsDictionary[item.Key].Count()} containers", true, "Sorter");
                }
                else if (!m_itemsDictionary.ContainsKey(item.Key))
                {
                    m_itemsDictionary.Add(item.Key, new List<(StorageContainer, int)> { (m_container, item.Value) });
                    Common.Dbgl($"Added {item.Key} to dict", true, "Sorter");
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
                    //Debug.LogWarning($"remove {key} from timeout");
                    m_putItemInContainerFailTimers.Remove(key);
                }
            }
        }
    }
}
