using BepInEx.Configuration;
using System.Collections.Generic;

namespace RagnarsRokare.SlaveGreylings
{
    public static class GreylingsConfig
    {
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> FeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static ConfigEntry<string> IncludedContainersList;
        public static ConfigEntry<int> TimeBeforeAssignmentCanBeRepeated;
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static IEnumerable<string> PreTameConsumables;
        public static IEnumerable<string> PostTameConsumables;
        public static ConfigEntry<int> Awareness;
        public static ConfigEntry<int> Agressiveness;
        public static ConfigEntry<int> Mobility;
        public static ConfigEntry<int> Intelligence;

        public static void Init(ConfigFile Config)
        {
            TamingItemList = Config.Bind<string>("General", "Greyling_TamingItemList", "SilverNecklace", "Comma separated list if items used to tame Greylings");
            FeedDuration = Config.Bind<int>("General", "Greyling_FeedDuration", 500, "Time before getting hungry after consuming one item");
            TamingTime = Config.Bind<int>("General", "Greyling_TamingTime", 1000, "Total time it takes to tame a greyling");
            IncludedContainersList = Config.Bind<string>("General", "Greyling_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
            TimeBeforeAssignmentCanBeRepeated = Config.Bind<int>("General", "Greylings_TimeBeforeAssignmentCanBeRepeated", 120, "How long before assignment can be done again");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Greylings_TimeLimitOnAssignment", 60, "How long before moving on to next assignment");
            PreTameConsumables = TamingItemList.Value.Split(',');
            PostTameConsumables = "Resin".Split(',');
            Awareness = Config.Bind<int>("General", "Greyling_Awareness", 4, "General awareness, used to calculate search ranges and ability to detect enemies");
            Agressiveness = Config.Bind<int>("General", "Greyling_Agressiveness", 2, "Agressivness determines how to behave when fighting and when to give up and flee");
            Mobility = Config.Bind<int>("General", "Greyling_Mobility", 5, "Mobility is used to determine how often and how far the mob moves");
            Intelligence = Config.Bind<int>("General", "Greyling_Intelligence", 3, "General intelligence, how much the mob can remember");
        }
    }
}
