using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    public partial class MobAILibServer : BaseUnityPlugin
    {
        public const string ModId = "RagnarsRokare.MobAILib.Server";
        public const string ModName = "RagnarsRökare Mob AI Server";
        public const string ModVersion = "0.0.1";

        public static ConfigEntry<bool> PrintDebugLog;

        private void Awake()
        {
            if (IsDedicated())
            {
                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
                PrintDebugLog = Config.Bind<bool>("General", "PrintDebugLog", false, "Extended logging, will produce A LOT of messages in the log and potentially have an impact on the frame rate.");
            }
            else
            {
                Debug.LogError("MobAILib Server can only run on a dedicated server");
            }
        }

        public static bool IsDedicated()
        {
            return new ZNet().IsDedicated();
        }
    }
}