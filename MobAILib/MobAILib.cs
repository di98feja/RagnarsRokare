using BepInEx;
using HarmonyLib;
using System.Reflection;

namespace RagnarsRokare.MobAI
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    public partial class MobAILib : BaseUnityPlugin
    {
        public const string ModId = "RagnarsRokare.MobAILib";
        public const string ModName = "RagnarsRökare Mob AI";
        public const string ModVersion = "0.2.0";

        private void Awake()
        {
            CommonConfig.Init(Config);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
    }
}