using BepInEx.Configuration;

namespace SlaveGreylings
{
    public static class BruteConfig
    {
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> FeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static ConfigEntry<int> AssignmentSearchRadius;
        public static ConfigEntry<int> ItemSearchRadius;
        public static ConfigEntry<int> ContainerSearchRadius;
        public static ConfigEntry<int> MaxContainersInMemory;
        public static ConfigEntry<int> TimeBeforeAssignmentCanBeRepeated;
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static ConfigEntry<string> IncludedContainersList;

        public static void Init(ConfigFile Config)
        {
            TamingItemList = Config.Bind<string>("General", "Brute_TamingItemList", "Dandelion", "Comma separated list if items used to tame Brutes");
            FeedDuration = Config.Bind<int>("General", "Brute_FeedDuration", 100, "Time before getting hungry after consuming one item");
            TamingTime = Config.Bind<int>("General", "Brute_TamingTime", 1000, "Total time it takes to tame a greyling");
            AssignmentSearchRadius = Config.Bind<int>("General", "Brute_AssignmentSearchRadius", 30, "Radius to search for new assignments within");
            ItemSearchRadius = Config.Bind<int>("General", "Brute_ItemSearchRadius", 10, "Radius to search for items on the ground");
            ContainerSearchRadius = Config.Bind<int>("General", "Brute_ContainerSearchRadius", 10, "Radius to search for containers");
            MaxContainersInMemory = Config.Bind<int>("General", "Brute_MaxContainersInMemory", 3, "How many containers the Brute should remember contents from");
            TimeBeforeAssignmentCanBeRepeated = Config.Bind<int>("General", "Brute_TimeBeforeAssignmentCanBeRepeated", 120, "How long before assignment can be done again");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Brute_TimeLimitOnAssignment", 60, "How long before moving on to next assignment");
            IncludedContainersList = Config.Bind<string>("General", "Brute_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
        }
    }
}
