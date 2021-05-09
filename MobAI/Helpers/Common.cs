using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class Common
    {
        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        public static ItemDrop GetNearbyItem(Vector3 center, IEnumerable<ItemDrop.ItemData> acceptedNames, int range = 10)
        {
            ItemDrop ClosestObject = null;
            foreach (Collider collider in Physics.OverlapSphere(center, range, LayerMask.GetMask(new string[] { "item" })))
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
                if (item?.transform?.position != null && acceptedNames.Select(n => n.m_shared.m_name).Contains(item.m_itemData.m_shared.m_name) && (ClosestObject == null || Vector3.Distance(center, item.transform.position) < Vector3.Distance(center, ClosestObject.transform.position)))
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Assignment FindRandomNearbyAssignment(Vector3 centre, List<string> trainedAssignments, MaxStack<Assignment> knownassignments, float assignmentSearchRadius)
        {
            Common.Dbgl($"Enter {nameof(FindRandomNearbyAssignment)}");
            //Generate list of acceptable assignments
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(centre, (float)assignmentSearchRadius, pieceList);
            var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => GetPrefabName(p.name) == a.PieceName && trainedAssignments.Contains(GetPrefabName(p.name))));
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

        public static Container FindRandomNearbyContainer(Vector3 center, MaxStack<Container> knownContainers, string[] m_acceptedContainerNames, float containerSearchRadius)
        {
            Common.Dbgl($"Enter {nameof(FindRandomNearbyContainer)}, looking for {m_acceptedContainerNames.Join()}");
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(center, (float)containerSearchRadius, pieceList);
            var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(GetPrefabName(p.name)));
            Common.Dbgl($"Found { allcontainerPieces.Count() } containers, filtering");
            var containers = allcontainerPieces?.Select(p => p.gameObject.GetComponent<Container>()).Where(c => !knownContainers.Contains(c));
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

        public static (string, ItemDrop.ItemData) SearchContainersforItems(MonsterAI instance, IEnumerable<ItemDrop> items, 
            ref MaxStack<Container> knownContainers, string[] acceptedContainerNames, float dt, float containerSearchRadius)
        {
            bool containerIsInvalid = knownContainers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
            if (containerIsInvalid)
            {
                knownContainers.Pop();
                return ("ContainerLost", null);
            }

            ItemDrop.ItemData foundItem = null;
            bool isCloseToContainer = false;
            if (knownContainers.Any())
            {
                isCloseToContainer = Vector3.Distance(instance.transform.position, knownContainers.Peek().transform.position) < 1.5;
                foundItem = knownContainers.Peek().GetInventory().GetAllItems().Where(i => items.Any(it => i.m_shared.m_name == it.m_itemData.m_shared.m_name)).RandomOrDefault();
            }
            if (!knownContainers.Any() || (isCloseToContainer && foundItem == null))
            {
                Container nearbyChest = FindRandomNearbyContainer(instance.transform.position, knownContainers, acceptedContainerNames, containerSearchRadius);
                if (nearbyChest != null)
                {
                    knownContainers.Push(nearbyChest);
                    //return ("FoundContainer", null);
                }
                else
                {
                    knownContainers.Clear();
                    return ("CannotFindContainers", null);
                }
            }
            if (!isCloseToContainer)
            {
                Invoke<MonsterAI>(instance, "MoveAndAvoid", dt, knownContainers.Peek().transform.position, 0.5f, false);
                return ("MovingtoContainer", null);
            }
            else if (!knownContainers.Peek()?.IsInUse() ?? false)
            {
                knownContainers.Peek().SetInUse(inUse: true);
                return ("OpenContainer", null);
            }
            else if (foundItem != null)
            {
                knownContainers.Peek().SetInUse(inUse: false);

                knownContainers.Peek().GetInventory().RemoveItem(foundItem, 1);
                Invoke<Container>(knownContainers.Peek(), "Save");
                Invoke<Inventory>(knownContainers.Peek(), "Changed");
                return ("ItemFound", foundItem);
            }
            else
            {
                knownContainers.Peek().SetInUse(inUse: false);
            }
            return ("", null);
        }

        public static bool AssignmentTimeoutCheck(ref MaxStack<Assignment> assignments, float dt, float timeBeforeAssignmentCanBeRepeated)
        {
            foreach (Assignment assignment in assignments)
            {
                assignment.AssignmentTime += dt;
                int multiplicator = 1;
                if (assignment.TypeOfAssignment.ComponentType == typeof(Fireplace))
                {
                    multiplicator = 3;
                }
                if (assignment.AssignmentTime > timeBeforeAssignmentCanBeRepeated * multiplicator)
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
