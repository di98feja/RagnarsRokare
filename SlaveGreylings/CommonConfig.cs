using BepInEx.Configuration;

namespace RagnarsRokare.SlaveGreylings
{
    public static class CommonConfig
    {
        public static ConfigEntry<int> NexusID;
        public static ConfigEntry<string> CallHomeCommandKey;
        public static ConfigEntry<string> UpdateSignFromContainerKey;
        public static ConfigEntry<bool> PrintAIStatusMessageToDebug;
        public static void Init(ConfigFile Config)
        {
            NexusID = Config.Bind<int>("General", "NexusID", 970, "Nexus mod ID for updates");
            CallHomeCommandKey = Config.Bind<string>("General", "CallHomeCommandKey", "Home", "Call all enslaved mobs within earshot");
            UpdateSignFromContainerKey = Config.Bind<string>("General", "UpdateSignFromContainerKey", "Insert", "Write inventory of closest chest on sign (max 50 chars)");
            PrintAIStatusMessageToDebug = Config.Bind<bool>("General", "PrintAIStateToDebug", false, "Print all AI state changes for all mobs to debug. Can cause performance drop if there are many mobs.");
        }
    }
}
