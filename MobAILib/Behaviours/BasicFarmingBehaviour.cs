// sapling_turnip, sapling_carrot, sapling_flax, sapling_barley, sapling_seedcarrot, sapling_seedonion, sapling_onion, sapling_seedturnip

/*

Pickable    Drop    Seed        Sapling
Carrot      Carrot  CarrotSeeds sapling_carrot
Turnip      Turnip  TurnipSeeds sapling_turnip
Onion       Onion   OnionSeeds  sapling_onion
Barley      Barley  Barley      sapling_barley
Flax        Flax    Flax        sapling_flax

*/

using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class BasicFarmingBehaviour : IBehaviour
    {
        private const string Prefix = "RR_BFARM";

        private class State
        {
            public const string Main = Prefix + "Main";
            public const string InitHarvest = Prefix + "InitHarvest";
            public const string FindCropSeed = Prefix + "FindCropSeed";
            public const string MoveToCrop = Prefix + "MoveToCrop";
            public const string Harvest = Prefix + "Harvest";
            public const string Plant = Prefix + "Plant";
            public const string Abandon = Prefix + "Abandon";
            public const string HarvestCompleted = Prefix + "HarvestCompleted";
        }
        private class Trigger
        {
            public const string StartSearch = Prefix + "StartSearch";
            public const string SeedFound = Prefix + "SeedFound";
            public const string Failed = Prefix + "Failed";
            public const string CropFound = Prefix + "CropFound";
            public const string CropIsClose = Prefix + "CropIsClose";
            public const string CropNotFound = Prefix + "CropNotFound";
            public const string HarvestSucceeded = Prefix + "HarvestSucceeded";
            public const string HarvestFailed = Prefix + "HarvestFailed";
            public const string PlantSucceeded = Prefix + "PlantSucceeded";
            public const string PlantFailed = Prefix + "PlantFailed";
            public const string Completed = Prefix + "Completed";
        }

        private class Crop
        {
            public string Pickable { get; set; }
            public string SeedName { get; set; }
            public ItemDrop.ItemData Seed { get; set; }
            public string SaplingName { get; set; }
            public Piece Sapling { get; set; }
        }

        private Crop[] AllCrops { get; } = new Crop[]
        {
            new Crop {Pickable = "Pickable_Carrot", SeedName = "CarrotSeeds", SaplingName = "sapling_carrot"},
            new Crop {Pickable = "Pickable_Turnip", SeedName = "TurnipSeeds", SaplingName = "sapling_turnip"},
            new Crop {Pickable = "Pickable_Onion", SeedName = "OnionSeeds", SaplingName = "sapling_onion"},
            new Crop {Pickable = "Pickable_Barley", SeedName = "Barley", SaplingName = "sapling_barley"},
            new Crop {Pickable = "Pickable_Flax", SeedName = "Flax", SaplingName = "sapling_flax"},
            new Crop {Pickable = "Pickable_SeedCarrot", SeedName = "Carrot", SaplingName = "sapling_seedcarrot"},
            new Crop {Pickable = "Pickable_SeedTurnip", SeedName = "Turnip", SaplingName = "sapling_seedturnip"},
            new Crop {Pickable = "Pickable_SeedOnion", SeedName = "Onion", SaplingName = "sapling_seedonion"},
        };

        private Crop[] Crops { get; set; }

        SearchForItemsBehaviour m_searchForItemsBehaviour;


        // Triggers
        private StateMachine<string, string>.TriggerWithParameters<IEnumerable<ItemDrop.ItemData>, string, string> LookForItemTrigger;

        // Settings
        public float MaxSearchTime { get; set; } = 60f;
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public string SearchForItemsState;
        public float CloseEnoughTimeout { get; private set; } = 45;
        public Pickable PickableToHarvest { get; set; }
        public string[] AcceptedContainerNames { get; set; } = new string[] { };
        public MaxStack<Container> KnownContainers { get; set; } = new MaxStack<Container>(5);

        // Timers
        private float m_currentSearchTimeLimit;
        private float m_closeEnoughTimer;

        private Crop m_cropToHarvest;
        private ItemDrop.ItemData m_seedToPlant;
        private Vector3 m_cropPosition;
        private MobAIBase m_aiBase;
        private bool m_hasBeenInitialized = false;
        private int m_initTryCount = 0;

        /// <summary>
        /// This method finds the PieceTable from the cultivator and stores the sapling Pieces in the Crops array so we can create instances of them later
        /// </summary>
        /// <returns>True if successful</returns>
        public bool Init()
        {
            PieceTable pieceTable = Resources.FindObjectsOfTypeAll<PieceTable>()?.SingleOrDefault(pt => pt.gameObject.name == "_CultivatorPieceTable");
            if (pieceTable == null)
            {
                Debug.LogWarning($"_cultivatorPieceTable not found");
                m_initTryCount++;
                return false;
            }
            if (ObjectDB.instance == null)
            {
                Debug.LogWarning("No ObjectDB instance in BasicFarmingBehaviour.Init()");
                m_initTryCount++;
                return false;
            }
            var resultCrops = new List<Crop>();
            foreach (var crop in AllCrops)
            {
                crop.Sapling = pieceTable.m_pieces.SingleOrDefault(p => p.gameObject.name == crop.SaplingName)?.GetComponent<Piece>();
                crop.Seed = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, crop.SeedName).FirstOrDefault()?.m_itemData;

                if (crop.Sapling != null && crop.Seed != null)
                {
                    Common.Dbgl($"Adding crop {crop.Pickable}", true, "");
                    resultCrops.Add(crop);
                }
                else
                {
                    Common.Dbgl($"Skipping crop {crop.Pickable}", true, "BasicFarming");
                }
            }
            Crops = resultCrops.ToArray();
            m_hasBeenInitialized = true;
            return true;
        }

        public bool IsHarvestableCrop(Pickable pickable)
        {
            return Crops.Any(c => c.Pickable == Common.GetPrefabName(pickable.gameObject.name));
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_aiBase = aiBase;
            m_searchForItemsBehaviour = new SearchForItemsBehaviour();
            m_searchForItemsBehaviour.Postfix = Prefix;
            m_searchForItemsBehaviour.Configure(aiBase, brain, State.FindCropSeed);
            LookForItemTrigger = brain.SetTriggerParameters<IEnumerable<ItemDrop.ItemData>, string, string>(Trigger.SeedFound);

            brain.Configure(State.Main)
                .InitialTransition(State.InitHarvest)
                .SubstateOf(parentState)
                .PermitDynamic(Trigger.Failed, () => FailState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered BasicFarmingBehaviour", true, "BasicFarming");
                    if (!m_hasBeenInitialized && m_initTryCount < 5)
                    {
                        m_hasBeenInitialized = Init();
                        if (m_initTryCount == 5)
                        {
                            Debug.LogError("Unable to init BasicFarmingBehaviour");
                        }
                    }
                });

            brain.Configure(State.Abandon)
                .SubstateOf(State.Main)
                .Permit(Trigger.Failed, FailState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Abandon BasicFarmingBehaviour", true, "BasicFarming");
                    Reset(aiBase);
                    brain.Fire(Trigger.Failed);
                });

            brain.Configure(State.InitHarvest)
                .SubstateOf(State.Main)
                .Permit(Trigger.CropFound, State.FindCropSeed)
                .Permit(Trigger.CropNotFound, State.Abandon)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered InitHarvest", true, "BasicFarming");
                });

            brain.Configure(State.FindCropSeed)
                .SubstateOf(State.Main)
                .Permit(Trigger.StartSearch, m_searchForItemsBehaviour.StartState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered FindCropSeed", true, "BasicFarming");
                    m_currentSearchTimeLimit = 0f;
                    m_searchForItemsBehaviour.KnownContainers = KnownContainers;
                    m_searchForItemsBehaviour.Items = new ItemDrop.ItemData[] { m_cropToHarvest.Seed };
                    m_searchForItemsBehaviour.AcceptedContainerNames = AcceptedContainerNames;
                    m_searchForItemsBehaviour.SuccessState = State.MoveToCrop;
                    m_searchForItemsBehaviour.FailState = State.Abandon;
                    m_searchForItemsBehaviour.IncludePickables = false;
                    brain.Fire(Trigger.StartSearch);
                });

            brain.Configure(State.MoveToCrop)
                .SubstateOf(State.Main)
                .Permit(Trigger.CropIsClose, State.Harvest)
                .Permit(Trigger.CropNotFound, State.Abandon)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered MoveToCrop", true, "BasicFarming");
                    m_aiBase.UpdateAiStatus(State.MoveToCrop, PickableToHarvest.GetHoverName());
                    m_currentSearchTimeLimit = Time.time + MaxSearchTime;
                    m_closeEnoughTimer = Time.time + CloseEnoughTimeout;
                });

            brain.Configure(State.Harvest)
                .SubstateOf(State.Main)
                .Permit(Trigger.HarvestSucceeded, State.Plant)
                .Permit(Trigger.HarvestFailed, State.Abandon)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered Harvest", true, "BasicFarming");
                    m_aiBase.UpdateAiStatus(State.Harvest, PickableToHarvest.GetHoverName());
                });

            brain.Configure(State.Plant)
                .SubstateOf(State.Main)
                .Permit(Trigger.PlantSucceeded, State.HarvestCompleted)
                .Permit(Trigger.PlantFailed, State.Abandon)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered Plant", true, "BasicFarming");
                    m_aiBase.UpdateAiStatus(State.Plant, m_cropToHarvest.Seed.m_shared.m_name);
                });

            brain.Configure(State.HarvestCompleted)
                .SubstateOf(State.Main)
                .Permit(Trigger.Completed, SuccessState)
                .OnEntry(t =>
                {
                    Common.Dbgl("Entered HarvestCompleted", true, "BasicFarming");
                    m_aiBase.UpdateAiStatus(State.HarvestCompleted);
                    Reset(aiBase);
                    aiBase.Brain.Fire(Trigger.Completed);
                });
        }

        private void Reset(MobAIBase aiBase)
        {
            if (m_seedToPlant != null)
            {
                var mob = (aiBase.Character as Humanoid);
                mob.DropItem((aiBase.Character as Humanoid).GetInventory(), m_seedToPlant, m_seedToPlant.m_stack);
                PickableToHarvest = null;
                m_seedToPlant = null;
            }
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.InitHarvest))
            {
                if (PickableToHarvest == null || !IsHarvestableCrop(PickableToHarvest))
                {
                    Common.Dbgl($"Have no suitable crop", true, "BasicFarming");
                    aiBase.Brain.Fire(Trigger.CropNotFound);
                    return;
                }
                m_cropToHarvest = Crops.Single(c => c.Pickable == Common.GetPrefabName(PickableToHarvest.gameObject.name));
                m_cropPosition = PickableToHarvest.transform.position;
                aiBase.Brain.Fire(Trigger.CropFound);
                return;
            }

            if (aiBase.Brain.IsInState(State.FindCropSeed))
            {
                m_searchForItemsBehaviour.Update(aiBase, dt);
                return;
            }

            if (aiBase.Brain.IsInState(State.MoveToCrop))
            {
                bool isCloseToTask = MoveToCrop(aiBase, dt);
                if (isCloseToTask)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:Reached crop position", true, "BasicFarming");
                    aiBase.Brain.Fire(Trigger.CropIsClose);
                }
                if (Time.time > m_currentSearchTimeLimit)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}:Failed to reach crop in time", true, "BasicFarming");
                    aiBase.Brain.Fire(Trigger.CropNotFound);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.Harvest))
            {
                bool successful = PickableToHarvest.Interact((aiBase.Character as Humanoid), false, false);
                if (successful)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}: Harvest successful", true, "BasicFarming");
                    aiBase.Brain.Fire(Trigger.HarvestSucceeded);
                }
                else
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}: Harvest failed", true, "BasicFarming");
                    aiBase.Brain.Fire(Trigger.HarvestFailed);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.Plant))
            {
                bool successful = PlantSapling();
                if (successful)
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}: Planting successful", true, "BasicFarming");
                    aiBase.Brain.Fire(Trigger.PlantSucceeded);
                }
                else
                {
                    Common.Dbgl($"{aiBase.Character.GetHoverName()}: Planting failed", true, "BasicFarming");
                    aiBase.Brain.Fire(Trigger.PlantFailed);
                }
                return;
            }
        }

        private bool PlantSapling()
        {
            TerrainModifier.SetTriggerOnPlaced(trigger: true);
            GameObject gameObject2 = UnityEngine.Object.Instantiate(m_cropToHarvest.Sapling.gameObject, m_cropPosition, Quaternion.identity);
            TerrainModifier.SetTriggerOnPlaced(trigger: false);
            PrivateArea component2 = gameObject2.GetComponent<PrivateArea>();
            if ((bool)component2)
            {
                component2.Setup(Game.instance.GetPlayerProfile().GetName());
            }
            WearNTear component3 = gameObject2.GetComponent<WearNTear>();
            if ((bool)component3)
            {
                component3.OnPlaced();
            }
            ItemDrop.ItemData rightItem = ((Humanoid)m_aiBase.Character).GetRightItem();
            if (rightItem != null)
            {
                m_aiBase.Character.transform.rotation = m_aiBase.Character.GetLookYaw();
                var zAnim = typeof(Character).GetField("m_zanim", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m_aiBase.Character) as ZSyncAnimation; 
                zAnim.SetTrigger(rightItem.m_shared.m_attack.m_attackAnimation);
            }
            m_cropToHarvest.Sapling.m_placeEffect.Create(m_cropPosition, Quaternion.identity, gameObject2.transform);
            m_aiBase.Character.AddNoise(50f);
            return true;
        }

        private bool MoveToCrop(MobAIBase aiBase, float dt)
        {
            if (PickableToHarvest == null /*|| PickableToHarvest?.GetComponent<ZNetView>()?.IsValid() != true*/)
            {
                aiBase.StopMoving();
                Common.Dbgl("Crop = null", true, "BasicFarming");
                aiBase.Brain.Fire(Trigger.CropNotFound);
                return false;
            }
            float distance = Time.time > m_closeEnoughTimer ? 0 : 0.5f;
            if (aiBase.MoveAndAvoidFire(PickableToHarvest.transform.position, dt, 0.5f + distance))
            {
                aiBase.StopMoving();
                return true;
            }
            if (Time.time > m_currentSearchTimeLimit)
            {
                Common.Dbgl($"Giving up on {PickableToHarvest.gameObject.name}", true, "BasicFarming");
                aiBase.StopMoving();
                aiBase.Brain.Fire(Trigger.CropNotFound);
            }
            return false;
        }

    }
}
