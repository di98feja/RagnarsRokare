using BepInEx.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace RagnarsRokare.SlaveGreylings
{
    public static class GreylingsConfig
    {
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> FeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static ConfigEntry<string> IncludedContainersList;
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static IEnumerable<string> PreTameConsumables;
        public static IEnumerable<string> PostTameConsumables;
        public static ConfigEntry<int> Awareness;
        public static ConfigEntry<int> Agressiveness;
        public static ConfigEntry<int> Mobility;
        public static ConfigEntry<int> Intelligence;

        public static void Init(ConfigFile Config)
        {
            TamingItemList = Config.Bind<string>("General", "Greyling_TamingItemList", "SilverNecklace,Amber,AmberPearl,Ruby", "Comma separated list if items used to tame Greylings");
            FeedDuration = Config.Bind<int>("General", "Greyling_FeedDuration", 500, "Time before getting hungry after consuming one item");
            TamingTime = Config.Bind<int>("General", "Greyling_TamingTime", 1000, "Total time it takes to tame a greyling");
            IncludedContainersList = Config.Bind<string>("General", "Greyling_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Greylings_TimeLimitOnAssignment", 60, "How long before moving on to next assignment");
            PreTameConsumables = TamingItemList.Value.Replace(" ", "").Split(',', ';');
            PostTameConsumables = "Resin".Split(',');
            Awareness = Config.Bind<int>("General", "Greyling_Awareness", 4, "General awareness, used to calculate search ranges and ability to detect enemies");
            Agressiveness = Config.Bind<int>("General", "Greyling_Agressiveness", 2, "Agressivness determines how to behave when fighting and when to give up and flee");
            Mobility = Config.Bind<int>("General", "Greyling_Mobility", 5, "Mobility is used to determine how often and how far the mob moves");
            Intelligence = Config.Bind<int>("General", "Greyling_Intelligence", 3, "General intelligence, how much the mob can remember");

            WorkableAssignments = new HashSet<string>(MobAI.Assignment.AssignmentTypes.Where(t => !t.IsExtractable).Select(a => a.PieceName));
        }
        public static Dictionary<string, string> AIStateDictionary { get; } = new Dictionary<string, string>()
        {
            {"RR_FIGHTMain","hmm?"},
            {"RR_FIGHTIdentifyEnemy", "EIIII See {0}!"},
            {"RR_FIGHTDoneFighting","*looks relieved*"},
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
            {"Flee", "AOWEEE!"},
            {"Follow", "Follow bigboss"},
            {"MoveAwayFrom", "Ahhh Scary!"},
            {"Assigned", "I'm on it Boss" },
            {"HaveAssignment", "Trying to Pickup {0}"},
            {"MoveToAssignment", "Moving to assignment {0}"},
            {"CheckingAssignment","Chekkin dis {0}"},
            {"UnloadToAssignment","Stuffin dis {0} full"},
            {"DoneWithAssignment", "Done doin worksignment!"}
        };

        public static HashSet<string> WorkableAssignments { get; set; }
    }
}
