using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace SlaveGreylings
{
    [BepInPlugin("RagnarsRokare.SlaveGreylings", "SlaveGreylings", "0.4")]
    public partial class SlaveGreylings : BaseUnityPlugin
    {
        private static readonly bool isDebug = true;

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