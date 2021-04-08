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
        public static ConfigEntry<bool> IncludeStandingWoodTorchInAssignments;
        public static ConfigEntry<bool> IncludeStandingIronTorchInAssignments;
        public static ConfigEntry<bool> IncludeStandingGreenTorchInAssignments;
        public static ConfigEntry<bool> IncludeWallTorchInAssignments;
        public static ConfigEntry<bool> IncludeBrazierInAssignments;
        public static ConfigEntry<bool> IncludeBlastFurnaceInAssignments;
        public static ConfigEntry<bool> IncludeWindmillInAssignments;
        public static ConfigEntry<bool> IncludeSpinningWheelInAssignments;


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
            IncludeStandingWoodTorchInAssignments = Config.Bind<bool>("General", "Greyling_StandingWoodTorchInAssignment", true, "Should StandingWoodTorch be included when searching for assigments");
            IncludeStandingIronTorchInAssignments = Config.Bind<bool>("General", "Greyling_StandingGreenTorch", true, "Should StandingGreenTorch be included when searching for assigments");
            IncludeStandingGreenTorchInAssignments = Config.Bind<bool>("General", "Greyling_StandingGreenTorch", true, "Should StandingGreenTorch be included when searching for assigments");
            IncludeWallTorchInAssignments = Config.Bind<bool>("General", "Greyling_WallTorch", true, "Should WallTorch be included when searching for assigments");
            IncludeBrazierInAssignments = Config.Bind<bool>("General", "Greyling_IncludeSmelterInAssignment", true, "Should Smelters be included when searching for assigments");
            IncludeBlastFurnaceInAssignments = Config.Bind<bool>("General", "Greyling_IncludeSmelterInAssignment", true, "Should Smelters be included when searching for assigments");
            IncludeWindmillInAssignments = Config.Bind<bool>("General", "Greyling_IncludeSmelterInAssignment", true, "Should Smelters be included when searching for assigments");
            IncludeSpinningWheelInAssignments = Config.Bind<bool>("General", "Greyling_IncludeSmelterInAssignment", true, "Should Smelters be included when searching for assigments");
        }
    }
}
