using BepInEx.Configuration;
using System.Collections.Generic;

namespace RagnarsRokare.SlaveGreylings
{
    public static class GreydwarfConfig
    {
        public static ConfigEntry<string> GreydwarfPrefabName;
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
            GreydwarfPrefabName = Config.Bind<string>("General", "Greydwarf_PrefabName", "Greydwarf", "The prefab to use the Greydwarf ai with (sort items)");
            TamingItemList = Config.Bind<string>("General", "Greydwarf_TamingItemList", "Honey", "Comma separated list if items used to tame Greydwarfs");
            PreTameFeedDuration = Config.Bind<int>("General", "Greydwarf_PreTameFeedDuration", 100, "Time before getting hungry after consuming one item during taming");
            PostTameFeedDuration = Config.Bind<int>("General", "Greydwarf_PostTameFeedDuration", 1000, "Time before getting hungry after consuming one item when tame");
            TamingTime = Config.Bind<int>("General", "Greydwarf_TamingTime", 1000, "Total time it takes to tame a Greydwarf");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Greydwarf_TimeLimitOnAssignment", 30, "How long before moving on to next assignment");
            IncludedContainersList = Config.Bind<string>("General", "Greydwarf_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
            PreTameConsumables = TamingItemList.Value.Split(',');
            PostTameConsumables = "QueensJam,Raspberry".Split(',');
            Awareness = Config.Bind<int>("General", "Greydwarf_Awareness", 5, "General awareness, used to calculate search ranges and ability to detect enemies");
            Agressiveness = Config.Bind<int>("General", "Greydwarf_Agressiveness", 4, "Agressivness determines how to behave when fighting and when to give up and flee");
            Mobility = Config.Bind<int>("General", "Greydwarf_Mobility", 6, "Mobility is used to determine how often and how far the mob moves");
            Intelligence = Config.Bind<int>("General", "Greydwarf_Intelligence", 8, "General intelligence, how much the mob can remember");
        }

        public static Dictionary<string, string> AIStateDictionary { get; } = new Dictionary<string, string>()
        {
            {"RR_FIGHTMain","hmm?"},
            {"RR_FIGHTIdentifyEnemy", "EIIII See {0}!"},
            {"RR_FIGHTDoneFighting","*looks relieved*"},
            {"RR_EATHungry", "Is hungry, no work a do"},
            {"RR_EATHaveFoodItem","*burps*"},
            {"RR_ISBMoveToContainer", "Heading to that a bin"},
            {"RR_ISBMoveToStorageContainer", "Puttin this {0} where it belongs"},
            {"RR_ISBMoveToDumpContainer", "Gonna check da messy bin"},
            {"RR_ISBMoveToGroundItem", "Heading to {0}"},
            {"RR_ISBMoveToPickable", "Heading to {0}"},
            {"RR_ISBPickUpItemFromGround", "Got a {0} from the ground"},
            {"RR_ISBPickUpAnotherItemFromGround", "Got another {0} from the ground"},
            {"RR_ISBGetItemFromDumpContainer", "Got a {0} from the messy bin"},
            {"RR_ISBSearchItemsOnGround", "*searches the ground*"},
            {"RR_ISBReadNearbySign", "*looks at sign*"},
            {"RR_ISBReadNearbyStorageSign", "*looks at sign*"},
            {"RR_SFISearchItemsOnGround","Look, there is a {0} on da grund"},
            {"RR_SFISearchForRandomContainer","Look a bin!"},
            {"RR_SFIMoveToGroundItem","Heading to {0}"},
            {"RR_SFIMoveToPickable","Heading to {0}"},
            {"RR_SFIPickUpItemFromGround","Got a {0} from the ground"},
            {"RR_SFIMoveToContainer","Heading to that a bin"},
            {"RR_SFISearchForItem","Found {0} in this a bin!"},
            {"Idle", "*looks bored*"},
            {"Flee", "Ahhh  big hurt!"},
            {"Follow", "Follow bossa"},
            {"MoveAwayFrom", "Jajaja!"}
        };
    }
}
