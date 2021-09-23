using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public class MobConfig
    {
        public IEnumerable<ItemDrop> PreTameConsumables { get; set; }
        public IEnumerable<ItemDrop> PostTameConsumables { get; set; }
        public float PreTameFeedDuration { get; set; }
        public float PostTameFeedDuration { get; set; }
        public float TamingTime { get; set; }
        public string AIType { get; set; }
        public MobAIBaseConfig AIConfig { get; set; }
    }

    public static class MobConfigManager
    {
        public static bool IsControllableMob(string mobType)
        {
            var type = Common.GetPrefabName(mobType);
            if (type == "Greyling") return true;
            if (type == "Greydwarf_Elite") return true;
            if (type == "Greydwarf") return true;
            return false;
        }

        public static IEnumerable<ItemDrop> CreateDropItemList(IEnumerable<string> itemNames)
        {
            foreach (var itemName in itemNames)
            {
                var item = ObjectDB.instance.GetItemByName(itemName);
                if (null == item)
                {
                    Debug.LogWarning($"Cannot find item {itemName} in objectDB");
                    continue;
                }
                yield return item;
            }
        }

        public static MobConfig GetMobConfig(string mobType)
        {
            Debug.Log(mobType);
            var type = Common.GetPrefabName(mobType);
            switch (type)
            {
                case "Greyling":
                    {
                        Debug.Log($"QueensJam:{ObjectDB.instance.GetItemByName("QueensJam").m_itemData.m_shared.m_name}");
                        Debug.Log($"Raspberry:{ObjectDB.instance.GetItemByName("Raspberry").m_itemData.m_shared.m_name}");
                        return new MobConfig
                        {
                            PostTameConsumables = CreateDropItemList(GreydwarfConfig.PostTameConsumables),
                            PostTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                            PreTameConsumables = CreateDropItemList(GreylingsConfig.PreTameConsumables),
                            PreTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                            TamingTime = GreylingsConfig.TamingTime.Value,
                            AIType = "Worker",
                            AIConfig = new WorkerAIConfig
                            {
                                FeedDuration = GreylingsConfig.FeedDuration.Value,
                                IncludedContainers = GreylingsConfig.IncludedContainersList.Value.Replace(" ", "").Split(',', ';'),
                                TimeLimitOnAssignment = GreylingsConfig.TimeLimitOnAssignment.Value,
                                Agressiveness = GreylingsConfig.Agressiveness.Value,
                                Awareness = GreylingsConfig.Awareness.Value,
                                Mobility = GreylingsConfig.Mobility.Value,
                                Intelligence = GreylingsConfig.Intelligence.Value,
                                AIStateCustomStrings = GreylingsConfig.AIStateDictionary,
                                WorkableAssignments = GreylingsConfig.WorkableAssignments
                            }
                        };
                    }
                case "Greydwarf_Elite":
                    {
                        return new MobConfig
                        {
                            PostTameConsumables = CreateDropItemList(BruteConfig.PostTameConsumables),
                            PostTameFeedDuration = BruteConfig.PostTameFeedDuration.Value,
                            PreTameConsumables = CreateDropItemList(BruteConfig.PreTameConsumables),
                            PreTameFeedDuration = BruteConfig.PreTameFeedDuration.Value,
                            TamingTime = BruteConfig.TamingTime.Value,
                            AIType = "Fixer",
                            AIConfig = new FixerAIConfig
                            {
                                PostTameFeedDuration = BruteConfig.PostTameFeedDuration.Value,
                                IncludedContainers = BruteConfig.IncludedContainersList.Value.Replace(" ", "").Split(',', ';'),
                                TimeLimitOnAssignment = BruteConfig.TimeLimitOnAssignment.Value,
                                Agressiveness = BruteConfig.Agressiveness.Value,
                                Awareness = BruteConfig.Awareness.Value,
                                Mobility = BruteConfig.Mobility.Value,
                                Intelligence = BruteConfig.Intelligence.Value,
                                AIStateCustomStrings = BruteConfig.AIStateDictionary
                            }
                        };
                    }

                case "Greydwarf":
                    {
                        return new MobConfig
                        {
                            PostTameConsumables = CreateDropItemList(GreydwarfConfig.PostTameConsumables),
                            PostTameFeedDuration = GreydwarfConfig.PostTameFeedDuration.Value,
                            PreTameConsumables = CreateDropItemList(GreydwarfConfig.PreTameConsumables),
                            PreTameFeedDuration = GreydwarfConfig.PreTameFeedDuration.Value,
                            TamingTime = GreydwarfConfig.TamingTime.Value,
                            AIType = "Sorter",
                            AIConfig = new SorterAIConfig
                            {
                                PostTameFeedDuration = GreydwarfConfig.PostTameFeedDuration.Value,
                                IncludedContainers = GreydwarfConfig.IncludedContainersList.Value.Replace(" ", "").Split(',',';'),
                                Agressiveness = GreydwarfConfig.Agressiveness.Value,
                                Awareness = GreydwarfConfig.Awareness.Value,
                                Mobility = GreydwarfConfig.Mobility.Value,
                                Intelligence = GreydwarfConfig.Intelligence.Value,
                                AIStateCustomStrings = GreydwarfConfig.AIStateDictionary,
                                WorkableAssignments = GreydwarfConfig.WorkableAssignments
                            }
                        };
                    }
                default:
                    return null;
            }
        }
    }
}
