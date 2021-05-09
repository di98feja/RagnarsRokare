﻿using BepInEx;
using HarmonyLib;
using System.Reflection;

namespace RagnarsRokare.MobAI
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin(ModId, ModName, ModVersion)]
    public partial class MobAILib : BaseUnityPlugin
    {
        public const string ModId = "RagnarsRokare.MobAILib";
        public const string ModName = "RagnarsRökare Mob AI";
        public const string ModVersion = "0.1.0";

        private void Awake()
        {
            CommonConfig.Init(Config);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
    }
}