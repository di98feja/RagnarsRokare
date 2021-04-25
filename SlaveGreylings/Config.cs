using BepInEx.Configuration;

namespace SlaveGreylings
{
    public static class GreylingsConfig
    {
        public static ConfigEntry<string> TamingItemList;
        public static ConfigEntry<int> FeedDuration;
        public static ConfigEntry<int> TamingTime;
        public static ConfigEntry<int> AssignmentSearchRadius;
        public static ConfigEntry<int> ItemSearchRadius;
        public static ConfigEntry<int> ContainerSearchRadius;
        public static ConfigEntry<bool> IncludeSmelterInAssignments;
        public static ConfigEntry<bool> IncludeKilnInAssignments;
        public static ConfigEntry<bool> IncludeFireplaceInAssignments;
        public static ConfigEntry<bool> IncludeHearthInAssignments;
        public static ConfigEntry<bool> IncludeStandingWoodTorchInAssignments;
        public static ConfigEntry<bool> IncludeStandingIronTorchInAssignments;
        public static ConfigEntry<bool> IncludeStandingGreenTorchInAssignments;
        public static ConfigEntry<bool> IncludeWallTorchInAssignments;
        public static ConfigEntry<bool> IncludeBrazierInAssignments;
        public static ConfigEntry<bool> IncludeBlastFurnaceInAssignments;
        public static ConfigEntry<bool> IncludeWindmillInAssignments;
        public static ConfigEntry<bool> IncludeSpinningWheelInAssignments;
        public static ConfigEntry<string> IncludedContainersList;
        public static ConfigEntry<int> MaxContainersInMemory;
        public static ConfigEntry<int> TimeBeforeAssignmentCanBeRepeated;
        public static ConfigEntry<int> TimeLimitOnAssignment;
        public static ConfigEntry<int> NexusID;
        public static ConfigEntry<string> CallHomeCommandKey;
        public static ConfigEntry<bool> PrintAIStatusMessageToDebug;
        public static void Init(ConfigFile Config)
        {
            TamingItemList = Config.Bind<string>("General", "Greyling_TamingItemList", "SilverNecklace", "Comma separated list if items used to tame Greylings");
            FeedDuration = Config.Bind<int>("General", "Greyling_FeedDuration", 500, "Time before getting hungry after consuming one item");
            TamingTime = Config.Bind<int>("General", "Greyling_TamingTime", 1000, "Total time it takes to tame a greyling");
            AssignmentSearchRadius = Config.Bind<int>("General", "Greyling_AssignmentSearchRadius", 30, "Radius to search for new assignments within");
            ItemSearchRadius = Config.Bind<int>("General", "Greyling_ItemSearchRadius", 10, "Radius to search for items on the ground");
            ContainerSearchRadius = Config.Bind<int>("General", "Greyling_ContainerSearchRadius", 10, "Radius to search for containers");
            IncludeSmelterInAssignments = Config.Bind<bool>("General", "Greyling_IncludeSmelterInAssignment", true, "Should Smelters be included when searching for assigments");
            IncludeKilnInAssignments = Config.Bind<bool>("General", "Greyling_IncludeKilnInAssignments", true, "Should Kiln be included when searching for assigments");
            IncludeFireplaceInAssignments = Config.Bind<bool>("General", "Greyling_IncludeFireplaceInAssignments", true, "Should Fireplace be included when searching for assigments");
            IncludeHearthInAssignments = Config.Bind<bool>("General", "Greyling_IncludeHearthInAssignments", true, "Should Hearth be included when searching for assigments");
            IncludeStandingWoodTorchInAssignments = Config.Bind<bool>("General", "Greyling_IncludeStandingWoodTorchInAssignment", true, "Should StandingWoodTorch be included when searching for assigments");
            IncludeStandingIronTorchInAssignments = Config.Bind<bool>("General", "Greyling_IncludeStandingIronTorchInAssignment", true, "Should StandingIronTorch be included when searching for assigments");
            IncludeStandingGreenTorchInAssignments = Config.Bind<bool>("General", "Greyling_IncludeStandingGreenTorchInAssignment", true, "Should StandingGreenTorch be included when searching for assigments");
            IncludeWallTorchInAssignments = Config.Bind<bool>("General", "Greyling_IncludeWallTorchInAssignment", true, "Should WallTorch be included when searching for assigments");
            IncludeBrazierInAssignments = Config.Bind<bool>("General", "Greyling_IncludeBrazierInAssignment", true, "Should Brazier be included when searching for assigments");
            IncludeBlastFurnaceInAssignments = Config.Bind<bool>("General", "Greyling_IncludeBlastFurnaceInAssignment", true, "Should BlastFurnace be included when searching for assigments");
            IncludeWindmillInAssignments = Config.Bind<bool>("General", "Greyling_IncludeWindmillInAssignment", true, "Should Windmill be included when searching for assigments");
            IncludeSpinningWheelInAssignments = Config.Bind<bool>("General", "Greyling_IncludeSpinningWheelInAssignment", true, "Should SpinningWheel be included when searching for assigments");
            IncludedContainersList = Config.Bind<string>("General", "Greyling_IncludedContainersList", "piece_chest_wood", "Comma separated list of container piece names to be searchable by Greylings");
            MaxContainersInMemory = Config.Bind<int>("General", "Greylings_MaxContainersInMemory", 3, "How many containers Greyling should remember contents from");
            TimeBeforeAssignmentCanBeRepeated = Config.Bind<int>("General", "Greylings_TimeBeforeAssignmentCanBeRepeated", 120, "How long before assignment can be done again");
            TimeLimitOnAssignment = Config.Bind<int>("General", "Greylings_TimeLimitOnAssignment", 60, "How long before moving on to next assignment");
            NexusID = Config.Bind<int>("General", "NexusID", 970, "Nexus mod ID for updates");
            CallHomeCommandKey = Config.Bind<string>("General", "CallHomeCommandKey", "Home", "Call all enslaved mobs within earshot");
            PrintAIStatusMessageToDebug = Config.Bind<bool>("General", "PrintAIStateToDebug", false, "Print all AI state changes for all mobs to debug. Can cause performance drop if there are many mobs.");
        }
    }
}
