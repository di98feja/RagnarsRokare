using BepInEx.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace RagnarsRokare.SlaveGreylings
{
    public static class BruteConfig
    {
        public static ConfigEntry<string> BrutePrefabName;
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<string> HungryItemList;
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
            HungryItemList = Config.Bind<string>("General", "Brute_PostTameConsumables", "QueensJam,Raspberry,Honey,Blueberry,Resin,Dandelions", "Comma separated list if items Brutes eat when hungry");
            PreTameFeedDuration = Config.Bind<int>("General", "Brute_PreTameFeedDuration", 100, "Time before getting hungry after consuming one item during taming");
            PostTameFeedDuration = Config.Bind<int>("General", "Brute_PostTameFeedDuration", 1000, "Time before getting hungry after consuming one item when tame");
            TamingTime = Config.Bind<int>("General", "Brute_TamingTime", 1000, "Total time it takes to tame a Brute");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Brute_TimeLimitOnAssignment", 30, "How long before moving on to next assignment");
            IncludedContainersList = Config.Bind<string>("General", "Brute_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
            PreTameConsumables = TamingItemList.Value.Replace(" ", "").Split(',', ';');
            PostTameConsumables = HungryItemList.Value.Replace(" ", "").Split(',', ';');
            Awareness = Config.Bind<int>("General", "Brute_Awareness", 6, "General awareness, used to calculate search ranges and ability to detect enemies");
            Agressiveness = Config.Bind<int>("General", "Brute_Agressiveness", 8, "Agressivness determines how to behave when fighting and when to give up and flee");
            Mobility = Config.Bind<int>("General", "Brute_Mobility", 10, "Mobility is used to determine how often and how far the mob moves");
            Intelligence = Config.Bind<int>("General", "Brute_Intelligence", 5, "General intelligence, how much the mob can remember");
        }

        public static Dictionary<string, string> AIStateDictionary { get; } = new Dictionary<string, string>()
        {
            {"RR_FIGHTMain","HUH?"},
            {"RR_FIGHTIdentifyEnemy", "ME BASH DIS {0}"},
            {"RR_FIGHTDoneFighting","GUD BASH!"},
            {"RR_EATHungry", "Is hungry, no work a do"},
            {"RR_EATHaveFoodItem","*burps*"},
            {"RR_ISBMoveToContainer", "Heading to that a bin"},
            {"RR_ISBMoveToStorageContainer", "Heading to that a bin"},
            {"RR_ISBMoveToGroundItem", "Heading to {0}"},
            {"RR_ISBPickUpItemFromGround", "Got a {0} from the ground"},
            {"RR_ISBSearchItemsOnGround", "Look, there is a {0} on da grund"},
            {"RR_SFISearchItemsOnGround","Look, there is a {0} on da grund"},
            {"RR_SFISearchForRandomContainer","Look a bin!"},
            {"RR_SFIMoveToGroundItem","Heading to {0}"},
            {"RR_SFIMoveToPickable","Heading to {0}"},
            {"RR_SFIPickUpItemFromGround","Got a {0} from the ground"},
            {"RR_SFIMoveToContainer","Heading to that a bin"},
            {"RR_SFISearchForItem","Found {0} in this a bin!"},
            {"Idle", "Nothing to do, bored"},
            {"Flee", "Takin a short breather"},
            {"Follow", "Follow punyboss"},
            {"Assigned", "uuhhhmm..  checkin' dis over 'ere"},
            {"MoveToAssignment", "Moving to assignment {0}"},
            {"CheckRepairState","Naah dis {0} goood"},
            {"RepairAssignment","Fixin Dis {0}"}
        };
    }
}
