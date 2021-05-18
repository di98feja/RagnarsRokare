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
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static ConfigEntry<string> IncludedContainersList;
        public static IEnumerable<string> PreTameConsumables;
        public static IEnumerable<string> PostTameConsumables;
        public static ConfigEntry<int> Awareness;
        public static ConfigEntry<int> Agressiveness;
        public static ConfigEntry<int> Mobility;
        public static ConfigEntry<int> Intelligence;

        public static void Init(ConfigFile Config)
        {
            BrutePrefabName = Config.Bind<string>("General", "Brute_PrefabName", "Greydwarf_Elite", "The prefab to use the Brute ai with (repair structures)");
            TamingItemList = Config.Bind<string>("General", "Brute_TamingItemList", "Dandelion", "Comma separated list if items used to tame Brutes");
            PreTameFeedDuration = Config.Bind<int>("General", "Brute_PreTameFeedDuration", 100, "Time before getting hungry after consuming one item during taming");
            PostTameFeedDuration = Config.Bind<int>("General", "Brute_PostTameFeedDuration", 1000, "Time before getting hungry after consuming one item when tame");
            TamingTime = Config.Bind<int>("General", "Brute_TamingTime", 1000, "Total time it takes to tame a Brute");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Brute_TimeLimitOnAssignment", 30, "How long before moving on to next assignment");
            IncludedContainersList = Config.Bind<string>("General", "Brute_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
            PreTameConsumables = TamingItemList.Value.Split(',');
            PostTameConsumables = "Dandelion".Split(',');
            Awareness = Config.Bind<int>("General", "Brute_Awareness", 6, "General awareness, used to calculate search ranges and ability to detect enemies");
            Agressiveness = Config.Bind<int>("General", "Brute_Agressiveness", 8, "Agressivness determines how to behave when fighting and when to give up and flee");
            Mobility = Config.Bind<int>("General", "Brute_Mobility", 10, "Mobility is used to determine how often and how far the mob moves");
            Intelligence = Config.Bind<int>("General", "Brute_Intelligence", 5, "General intelligence, how much the mob can remember");
        }
    }
}
