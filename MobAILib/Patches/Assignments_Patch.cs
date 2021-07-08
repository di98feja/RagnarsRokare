using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public partial class MobAILib
    {
        [HarmonyPatch(typeof(Switch), "Interact")]
        static class Switch_Interact_Patch
        {
            public static void Postfix(Switch __instance)
            {
                foreach (MobAIBase mob in MobManager.AliveMobs.Where(m => m.Value.HasInstance()).Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value))
                {
                    string interactName = Common.GetPrefabName(__instance.transform.parent.gameObject.name);
                    string prefabName = Common.GetPrefabName(__instance.transform.parent.gameObject.name);
                    if (!mob.CanWorkAssignment(prefabName))
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Can not operate the {interactName}!");
                        return;
                    }
                    if (mob.m_trainedAssignments.Contains(prefabName))
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
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Looks bewildered when looking at {interactName}.");
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
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: There is a shine in its eyes when loocking at the {interactName}.");
                        }
                        if (mob.learningRate == 5)
                        {
                            mob.m_trainedAssignments.Add(prefabName);
                            mob.NView.GetZDO().Set(Constants.Z_trainedAssignments, mob.m_trainedAssignments.Join());
                            mob.NView.InvokeRPC(ZNetView.Everybody, Constants.Z_updateTrainedAssignments, mob.UniqueID, mob.m_trainedAssignments.Join());
                            Debug.Log($"{interactName} learned.");
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
                foreach (MobAIBase mob in MobManager.AliveMobs.Where(m => m.Value.HasInstance()).Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value))
                {
                    string interactName = __instance.GetHoverName();
                    string prefabName = Common.GetPrefabName(__instance.gameObject.name);
                    if (!mob.CanWorkAssignment(prefabName))
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Can not operate the {interactName}!");
                        return;
                    }
                    if (mob.m_trainedAssignments.Contains(prefabName))
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
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Looks bewildered when looking at {interactName}.");
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
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: There is a shine in its eyes when loocking at the {interactName}.");
                        }
                        if (mob.learningRate == 5)
                        {
                            mob.m_trainedAssignments.Add(prefabName);
                            mob.NView.GetZDO().Set(Constants.Z_trainedAssignments, mob.m_trainedAssignments.Join());
                            mob.NView.InvokeRPC(ZNetView.Everybody, Constants.Z_updateTrainedAssignments, mob.UniqueID, mob.m_trainedAssignments.Join());
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Have now learned how to operate {interactName}.");
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: List of known assignments: {mob.m_trainedAssignments.Join()}.");
                            mob.learningTask = "";
                            mob.learningRate = 0;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Pickable), "Interact")]
        static class FPickable_Interact_Patch
        {
            public static void Postfix(Pickable __instance)
            {
                foreach (MobAIBase mob in MobManager.AliveMobs.Where(m => m.Value.HasInstance()).Where(m => (m.Value.Instance as MonsterAI).GetFollowTarget() == Player.m_localPlayer.gameObject).Select(m => m.Value))
                {
                    string interactName = __instance.GetHoverName();
                    string prefabName = Common.GetPrefabName(__instance.gameObject.name);
                    if (mob.m_trainedAssignments.Contains(prefabName))
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Already know how to operate the {interactName}!");
                        return;
                    }
                    if (Vector3.Distance(mob.Character.transform.position, __instance.transform.position) < 5 && !mob.m_trainedAssignments.Contains(prefabName))
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
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Looks bewildered when looking at {interactName}.");
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
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: There is a shine in its eyes when loocking at the {interactName}.");
                        }
                        if (mob.learningRate == 5)
                        {
                            mob.m_trainedAssignments.Add(prefabName);
                            mob.NView.GetZDO().Set(Constants.Z_trainedAssignments, mob.m_trainedAssignments.Join());
                            mob.NView.InvokeRPC(ZNetView.Everybody, Constants.Z_updateTrainedAssignments, mob.UniqueID, mob.m_trainedAssignments.Join());
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"{mob.Character.GetHoverName()}: Have now learned how to operate {interactName}.");
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