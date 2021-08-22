using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Container), "Interact")]
        static class Fireplace_Interact_Patch
        {
            public static void Postfix(Container __instance, bool hold)
            {
                foreach (MobAIBase mob in MobManager.AliveMobs.Where(m => m.Value.HasInstance()).Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value))
                {
                    string interactName = Common.GetPrefabName(__instance.gameObject.name);
                    mob.NView.InvokeRPC(ZNetView.Everybody, Constants.Z_MobCommand, Player.m_localPlayer.GetZDOID(), "AssignDumpContainer");
                }
            }
        }
    }
}