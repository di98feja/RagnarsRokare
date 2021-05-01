using HarmonyLib;
using UnityEngine;
using RagnarsRokare.MobAI;
using System.Linq;

namespace SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Switch), "Interact")]
        static class Switch_Interact_Patch
        {
            public static void Postfix(Switch __instance)
            {
                foreach (MobAIBase mob in MobManager.Mobs.Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value))
                {
                    string interactName = Common.GetPrefabName(__instance.transform.parent.gameObject.name);
                    if (mob.learningTask == interactName)
                    {
                        mob.learningRate += 1;
                        Debug.Log($"Learning {interactName} increased to {mob.learningRate}/5.");
                    }
                    else
                    {
                        mob.learningTask = interactName;
                        mob.learningRate = 0;
                        Debug.Log($"Starting to learn {interactName}.");
                    }
                    if (mob.learningRate == 5 && !mob.trainedAssignments.Contains(interactName))
                    {
                        mob.trainedAssignments.Add(interactName);
                        Debug.Log($"{interactName} learnt .");
                        Debug.Log($"Accepted Assignments: {mob.trainedAssignments.Join()}.");
                        mob.learningTask = "";
                        mob.learningRate = 0;
                    }
                }
            }
        }
    }
}