using BepInEx.Configuration;

namespace SlaveGreylings
{
    public static class CommonConfig
    {
        public static ConfigEntry<int> NexusID;
        public static ConfigEntry<string> CallHomeCommandKey;
        public static ConfigEntry<bool> PrintAIStatusMessageToDebug;
        public static void Init(ConfigFile Config)
        {
            NexusID = Config.Bind<int>("General", "NexusID", 970, "Nexus mod ID for updates");
            CallHomeCommandKey = Config.Bind<string>("General", "CallHomeCommandKey", "Home", "Call all enslaved mobs within earshot");
            PrintAIStatusMessageToDebug = Config.Bind<bool>("General", "PrintAIStateToDebug", false, "Print all AI state changes for all mobs to debug. Can cause performance drop if there are many mobs.");
        }
    }
}
