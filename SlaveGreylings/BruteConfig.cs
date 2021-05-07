using BepInEx.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace RagnarsRokare.SlaveGreylings
{
    public static class BruteConfig
    {
        public static ConfigEntry<string> BrutePrefabName;
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> PreTameFeedDuration;
        public static ConfigEntry<int> PostTameFeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static ConfigEntry<int> AssignmentSearchRadius;
        public static ConfigEntry<int> ItemSearchRadius;
        public static ConfigEntry<int> ContainerSearchRadius;
        public static ConfigEntry<int> MaxContainersInMemory;
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static ConfigEntry<string> IncludedContainersList;

        public static IEnumerable<ItemDrop> PreTameConsumables;
        public static IEnumerable<ItemDrop> PostTameConsumables;

        public static void Init(ConfigFile Config)
        {
            BrutePrefabName = Config.Bind<string>("General", "Brute_PrefabName", "Greydwarf_Elite", "The prefab to use the Brute ai with (repair structures)");
            TamingItemList = Config.Bind<string>("General", "Brute_TamingItemList", "Dandelion", "Comma separated list if items used to tame Brutes");
            PreTameFeedDuration = Config.Bind<int>("General", "Brute_PreTameFeedDuration", 100, "Time before getting hungry after consuming one item during taming");
            PostTameFeedDuration = Config.Bind<int>("General", "Brute_PostTameFeedDuration", 1000, "Time before getting hungry after consuming one item when tame");
            TamingTime = Config.Bind<int>("General", "Brute_TamingTime", 1000, "Total time it takes to tame a Brute");
            AssignmentSearchRadius = Config.Bind<int>("General", "Brute_AssignmentSearchRadius", 10, "Radius to search for new assignments within");
            ItemSearchRadius = Config.Bind<int>("General", "Brute_ItemSearchRadius", 10, "Radius to search for items on the ground");
            ContainerSearchRadius = Config.Bind<int>("General", "Brute_ContainerSearchRadius", 10, "Radius to search for containers");
            MaxContainersInMemory = Config.Bind<int>("General", "Brute_MaxContainersInMemory", 3, "How many containers the Brute should remember contents from");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Brute_TimeLimitOnAssignment", 30, "How long before moving on to next assignment");
            IncludedContainersList = Config.Bind<string>("General", "Brute_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
            PreTameConsumables = TamingItemList.Value.Split(',').Select(i => ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, i).FirstOrDefault());
            PostTameConsumables = new List<ItemDrop> { ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Dandelion").Single() };
        }
    }
}
