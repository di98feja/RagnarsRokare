using BepInEx.Configuration;

namespace RagnarsRokare.MobAI
{
    public static class CommonConfig
    {
        public static ConfigEntry<int> NexusID;
        public static ConfigEntry<bool> PrintDebugLog;
        public static ConfigEntry<bool> PrintAIStatusMessageToDebug;
        public static ConfigEntry<string> PrintDebugLogFilter;
        public static ConfigEntry<bool> RoamingAI;
        public static void Init(ConfigFile Config)
        {
            NexusID = Config.Bind<int>("General", "NexusID", 1188, "Nexus mod ID for updates");
            PrintDebugLog = Config.Bind<bool>("General", "PrintDebugLog", false, "Extended logging, will produce A LOT of messages in the log and potentially have an impact on the frame rate.");
            PrintAIStatusMessageToDebug = Config.Bind<bool>("General", "PrintAIStateToDebug", false, "Print all AI state changes for all mobs to debug. Can cause performance drop if there are many mobs.");
            PrintDebugLogFilter = Config.Bind<string>("General", "PrintDebugLogFilter", "", "Filter Extended logging, only include messages that contain the filter string");
            RoamingAI = Config.Bind<bool>("General", "RoamingAI", true, "Keep area around ai-mobs active even when there is no player around. Atleast one player must be logged in however.");
        }
    }
}
