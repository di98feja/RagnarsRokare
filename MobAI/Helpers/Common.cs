using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class Common
    {
        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        public static ItemDrop GetNearbyItem(BaseAI instance, IEnumerable<ItemDrop.ItemData> acceptedNames, int range = 10)
        {
            Vector3 position = instance.transform.position;
            ItemDrop ClosestObject = null;
            foreach (Collider collider in Physics.OverlapSphere(position, range, LayerMask.GetMask(new string[] { "item" })))
            {
                ItemDrop item = collider.transform.parent?.parent?.gameObject?.GetComponent<ItemDrop>();
                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    item = collider.transform.parent?.gameObject?.GetComponent<ItemDrop>();
                    if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                    {
                        item = collider.transform?.gameObject?.GetComponent<ItemDrop>();
                        if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                        {
                            continue;
                        }
                    }
                }
                Common.Dbgl($"Item: {item.name} ");
                //StaticTarget componentInParent = collider?.GetComponentInParent<StaticTarget>();
                //Common.Dbgl($"Statictarget: {componentInParent} ");
                //Common.Dbgl($"Can see target: {instance.CanSeeTarget(componentInParent)} "); 

                if (item?.transform?.position != null && acceptedNames.Select(n => n.m_shared.m_name).Contains(item.m_itemData.m_shared.m_name) && (ClosestObject == null || Vector3.Distance(position, item.transform.position) < Vector3.Distance(position, ClosestObject.transform.position))) // && instance.CanSeeTarget(componentInParent)
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Assignment FindRandomNearbyAssignment(BaseAI instance, List<string> trainedAssignments, MaxStack<Assignment> knownassignments)
        {
            Common.Dbgl($"Enter {nameof(FindRandomNearbyAssignment)}");
            Vector3 position = instance.transform.position;
            //Generate list of acceptable assignments
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, (float)GreylingsConfig.AssignmentSearchRadius.Value, pieceList);
            var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => GetPrefabName(p.name) == a.PieceName && trainedAssignments.Contains(GetPrefabName(p.name))) ); //&& instance.CanSeeTarget(p?.GetComponentInChildren<StaticTarget>())
            // no assignments detekted, return false
            if (!allAssignablePieces.Any())
            {
                return null;
            }
            Common.Dbgl($"Assignments found: {allAssignablePieces.Select(n => n.name).Join()}");
            // filter out assignments already in list
            var newAssignments = allAssignablePieces.Where(p => !knownassignments.Any(a => a.AssignmentObject == p.gameObject));
            Common.Dbgl($"Assignments after filter: {newAssignments.Select(n => n.name).Join()}");

            // filter out inaccessible assignments
            //newAssignments = newAssignments.Where(p => Pathfinding.instance.GetPath(greylingPosition, p.gameObject.transform.position, null, Pathfinding.AgentType.Humanoid, true, true));

            if (!newAssignments.Any())
            {
                return null;
            }

            // select random piece
            //var random = new System.Random();
            //int index = random.Next(newAssignments.Count());
            var selekted = newAssignments.RandomOrDefault();
            Common.Dbgl($"Returning assignment: {selekted.name}");
            Assignment randomAssignment = new Assignment(selekted);
            return randomAssignment;
        }

        public static Container FindRandomNearbyContainer(BaseAI instance, MaxStack<Container> knownContainers, string[] m_acceptedContainerNames)
        {
            Common.Dbgl($"Enter {nameof(FindRandomNearbyContainer)}, looking for {m_acceptedContainerNames.Join()}");
            Vector3 position = instance.transform.position;
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, (float)GreylingsConfig.ContainerSearchRadius.Value, pieceList);
            var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(GetPrefabName(p.name)));
            var containers = allcontainerPieces?.Select(p => p.gameObject.GetComponentInChildren<Container>()).Where(c => !knownContainers.Contains(c)).ToList(); //instance.CanSeeTarget(c?.GetComponentInChildren<StaticTarget>())
            containers.AddRange(allcontainerPieces?.Select(p => p.gameObject.GetComponent<Container>()).Where(c => !knownContainers.Contains(c)));  //instance.CanSeeTarget(c?.GetComponentInChildren<StaticTarget>())
            if (!containers.Any())
            {
                Common.Dbgl("No containers found, returning null");
                return null;
            }
            // select random piece
            return containers.RandomOrDefault();
        }

        public static string GetPrefabName(string name)
        {
            char[] anyOf = new char[] { '(', ' ' };
            int num = name.IndexOfAny(anyOf);
            string result;
            if (num >= 0)
                result = name.Substring(0, num);
            else
                result = name;
            return result;
        }

        public static bool AssignmentTimeoutCheck(ref MaxStack<Assignment> assignments, float dt)
        {
            foreach (Assignment assignment in assignments)
            {
                assignment.AssignmentTime += dt;
                int multiplicator = 1;
                if (assignment.TypeOfAssignment.ComponentType == typeof(Fireplace))
                {
                    multiplicator = 3;
                }
                if (assignment.AssignmentTime > GreylingsConfig.TimeBeforeAssignmentCanBeRepeated.Value * multiplicator)
                {
                    Common.Dbgl($"GreAssignment: {assignment} forgotten");
                    assignments.Remove(assignment);
                    if (!assignments.Any())
                    {
                        return false;
                    }
                    break;
                }
            }
            return true;
        }

        public static ZNetView GetNView<T>(T obj)
        {
            return typeof(T).GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj) as ZNetView;
        }

        public static string GetOrCreateUniqueId(ZNetView nview)
        {
            var uniqueId = nview.GetZDO().GetString(Constants.Z_UniqueId);
            if (string.IsNullOrEmpty(uniqueId))
            {
                uniqueId = System.Guid.NewGuid().ToString();
                nview.GetZDO().Set(Constants.Z_UniqueId, uniqueId);
            }
            return uniqueId;
        }


        public static void Dbgl(string str = "", bool pref = true)
        {
            if (CommonConfig.PrintDebugLog.Value)
            {
                Debug.Log((pref ? typeof(MobAILib).Assembly + " " : "") + str);
            }
        }

    }
}