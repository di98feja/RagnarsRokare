using HarmonyLib;
using UnityEngine;
using RagnarsRokare.MobAI;
using System.Linq;

namespace RagnarsRokare.SlaveGreylings
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
                    if (mob.m_trainedAssignments.Contains(interactName))
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Already know how to operate the {interactName}!");
                        return;
                    }
                    if (Vector3.Distance(mob.Character.transform.position, __instance.transform.position) < 5 && !mob.m_trainedAssignments.Contains(interactName))
                    { 
                        if (mob.learningTask == interactName)
                        {
                            mob.learningRate += 1;
                        }
                        else
                        {
                            mob.learningTask = interactName;
                            mob.learningRate = 0;
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Introduced to operate the {interactName}.");
                        }
                        if (mob.learningRate == 1)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Greyling is shaking the head when looking at {interactName}.");
                        }
                        if (mob.learningRate == 2)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Increasing the understanding of {interactName}.");
                        }
                        if (mob.learningRate == 3)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Got the basic consept of {interactName}.");
                        }
                        if (mob.learningRate == 4)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: There is a shine in the greylings eyes when loocking at the {interactName}.");
                        }
                        if (mob.learningRate == 5)
                        {
                            mob.m_trainedAssignments.Add(interactName);
                            mob.NView.GetZDO().Set(Constants.Z_trainedAssignments, mob.m_trainedAssignments.Join());
                            mob.NView.InvokeRPC(ZNetView.Everybody, Constants.Z_updateTrainedAssignments, mob.UniqueID, mob.m_trainedAssignments.Join());
                            Debug.Log($"{interactName} learnt .");
                            Debug.Log($"Accepted Assignments: {mob.m_trainedAssignments.Join()}.");
                            mob.learningTask = "";
                            mob.learningRate = 0;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Fireplace), "Interact")]
        static class Fireplace_Interact_Patch
        {
            public static void Postfix(Fireplace __instance)
            {
                foreach (MobAIBase mob in MobManager.Mobs.Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value))
                {
                    string interactName = Common.GetPrefabName(__instance.gameObject.name);
                    if (mob.m_trainedAssignments.Contains(interactName))
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Already know how to operate the {interactName}!");
                        return;
                    }
                    if (Vector3.Distance(mob.Character.transform.position, __instance.transform.position) < 5 && !mob.m_trainedAssignments.Contains(interactName))
                    {
                        if (mob.learningTask == interactName)
                        {
                            mob.learningRate += 1;
                        }
                        else
                        {
                            mob.learningTask = interactName;
                            mob.learningRate = 0;
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Introduced to operate the {interactName}.");
                        }
                        if (mob.learningRate == 1)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Greyling is shaking the head when looking at {interactName}.");
                        }
                        if (mob.learningRate == 2)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Increasing the understanding of {interactName}.");
                        }
                        if (mob.learningRate == 3)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Got the basic consept of {interactName}.");
                        }
                        if (mob.learningRate == 4)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: There is a shine in the greylings eyes when loocking at the {interactName}.");
                        }
                        if (mob.learningRate == 5)
                        {
                            mob.m_trainedAssignments.Add(interactName);
                            mob.NView.GetZDO().Set(Constants.Z_trainedAssignments, mob.m_trainedAssignments.Join());
                            mob.NView.InvokeRPC(ZNetView.Everybody, Constants.Z_updateTrainedAssignments, mob.UniqueID, mob.m_trainedAssignments.Join());
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: The greyling have now learnt how to operate {interactName}.");
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: List of known assignments: {mob.m_trainedAssignments.Join()}.");
                            mob.learningTask = "";
                            mob.learningRate = 0;
                        }
                    }
                }
            }
        }
    }
}