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
        public string AIConfig { get; set; }
    }

    public static class MobConfigManager
    {
        public static bool IsControllableMob(string mobType)
        {
            var type = Common.GetPrefabName(mobType);
            if (type == "Greyling") return true;
            if (type == "Greydwarf_Elite") return true;
            return false;
        }

        public static MobConfig GetMobConfig(string mobType)
        {
            var type = Common.GetPrefabName(mobType);
            switch (type)
            {
                case "Greyling":
                    {
                        
                        return new MobConfig
                        {
                            PostTameConsumables = GreylingsConfig.PostTameConsumables.Select(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault()),
                            PostTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                            PreTameConsumables = GreylingsConfig.PreTameConsumables.Select(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault()),
                            PreTameFeedDuration = GreylingsConfig.FeedDuration.Value,
                            TamingTime = GreylingsConfig.TamingTime.Value,
                            AIType = "Worker",
                            AIConfig = JsonUtility.ToJson(new WorkerAIConfig
                            {
                                AssignmentSearchRadius = GreylingsConfig.AssignmentSearchRadius.Value,
                                ContainerSearchRadius = GreylingsConfig.ContainerSearchRadius.Value,
                                FeedDuration = GreylingsConfig.FeedDuration.Value,
                                IncludedContainers = GreylingsConfig.IncludedContainersList.Value.Split(','),
                                ItemSearchRadius = GreylingsConfig.ItemSearchRadius.Value,
                                MaxContainersInMemory = GreylingsConfig.MaxContainersInMemory.Value,
                                TimeBeforeAssignmentCanBeRepeated = GreylingsConfig.TimeBeforeAssignmentCanBeRepeated.Value,
                                TimeLimitOnAssignment = GreylingsConfig.TimeLimitOnAssignment.Value
                            })
                        };
                    }
                case "Greydwarf_Elite":
                    {
                        string config = JsonUtility.ToJson(new FixerAIConfig
                        {
                            AssignmentSearchRadius = BruteConfig.AssignmentSearchRadius.Value,
                            ContainerSearchRadius = BruteConfig.ContainerSearchRadius.Value,
                            PostTameFeedDuration = BruteConfig.PostTameFeedDuration.Value,
                            IncludedContainers = BruteConfig.IncludedContainersList.Value.Split(','),
                            ItemSearchRadius = BruteConfig.ItemSearchRadius.Value,
                            MaxContainersInMemory = BruteConfig.MaxContainersInMemory.Value,
                            TimeLimitOnAssignment = BruteConfig.TimeLimitOnAssignment.Value
                        });
                        return new MobConfig
                        {
                            PostTameConsumables = BruteConfig.PostTameConsumables.Select(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault()),
                            PostTameFeedDuration = BruteConfig.PostTameFeedDuration.Value,
                            PreTameConsumables = BruteConfig.PreTameConsumables.Select(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault()),
                            PreTameFeedDuration = BruteConfig.PreTameFeedDuration.Value,
                            TamingTime = BruteConfig.TamingTime.Value,
                            AIType = "Fixer",
                            AIConfig = config
                        };
                    }
                default:
                    return null;
            }
        }
    }
}
