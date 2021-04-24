using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace SlaveGreylings
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin(ModId, ModName, ModVersion)]
    public partial class SlaveGreylings : BaseUnityPlugin
    {
        public const string ModId = "RagnarsRokare.SlaveGreylings";
        public const string ModName = "RagnarsRökare SlaveGreylings";
        public const string ModVersion = "0.4";

        private static readonly bool isDebug = false;

        private void Awake()
        {
            GreylingsConfig.Init(Config);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(SlaveGreylings).Namespace + " " : "") + str);
        }
    }
}