using HarmonyLib;
using UnityEngine;
using RagnarsRokare.MobAI;

namespace SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Switch), "Interact")]
        static class Switch_Interact_Patch
        {
            public static void Postfix(Switch __instance)
            {
                foreach(MobAIBase mob in MobManager.Mobs.Where(s => s.))
                {
                    
                }

                string name = Common.GetPrefabName(__instance.gameObject.name);
            }
        }
    }
}