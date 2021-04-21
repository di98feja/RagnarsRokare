using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SlaveGreylings
{
    public class Common
    {
        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        public static ItemDrop GetNearbyItem(Vector3 center, IEnumerable<ItemDrop> acceptedNames, int range = 10)
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
                if (item?.transform?.position != null && acceptedNames.Select(n => n.m_itemData.m_shared.m_name).Contains(item.m_itemData.m_shared.m_name) && (ClosestObject == null || Vector3.Distance(center, item.transform.position) < Vector3.Distance(center, ClosestObject.transform.position)))
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Assignment FindRandomNearbyAssignment(Vector3 centre, MaxStack<Assignment> knownassignments)
        {
            SlaveGreylings.Dbgl($"Enter {nameof(FindRandomNearbyAssignment)}");
            //Generate list of acceptable assignments
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(centre, (float)GreylingsConfig.AssignmentSearchRadius.Value, pieceList);
            var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => GetPrefabName(p.name) == a.PieceName && a.Activated));
            // no assignments detekted, return false
            if (!allAssignablePieces.Any())
            {
                return null;
            }
            SlaveGreylings.Dbgl($"Assignments found: {allAssignablePieces.Select(n => n.name).Join()}");
            // filter out assignments already in list
            var newAssignments = allAssignablePieces.Where(p => !knownassignments.Any(a => a.AssignmentObject == p.gameObject));
            SlaveGreylings.Dbgl($"Assignments after filter: {newAssignments.Select(n => n.name).Join()}");

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
            SlaveGreylings.Dbgl($"Returning assignment: {selekted.name}");
            Assignment randomAssignment = new Assignment(selekted);
            return randomAssignment;
        }

        public static Container FindRandomNearbyContainer(Vector3 center, MaxStack<Container> knownContainers, string[] m_acceptedContainerNames)
        {
            SlaveGreylings.Dbgl($"Enter {nameof(FindRandomNearbyContainer)}, looking for {m_acceptedContainerNames.Join()}");
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(center, (float)GreylingsConfig.ContainerSearchRadius.Value, pieceList);
            var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(GetPrefabName(p.name)));
            SlaveGreylings.Dbgl($"Found { allcontainerPieces.Count() } containers, filtering");
            var containers = allcontainerPieces?.Select(p => p.gameObject.GetComponent<Container>()).Where(c => !knownContainers.Contains(c));
            if (!containers.Any())
            {
                SlaveGreylings.Dbgl("No containers found, returning null");
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

        public static (string, ItemDrop.ItemData) SearchContainersforItems(MonsterAI instance, IEnumerable<ItemDrop> Items, ref MaxStack<Container> KnownContainers, string[] AcceptedContainerNames, float dt)
        {
            bool containerIsInvalid = KnownContainers.Peek()?.GetComponent<ZNetView>()?.IsValid() == false;
            if (containerIsInvalid)
            {
                KnownContainers.Pop();
                return ("ContainerLost", null);
            }

            ItemDrop.ItemData foundItem = null;
            bool isCloseToContainer = false;
            if (KnownContainers.Any())
            {
                isCloseToContainer = Vector3.Distance(instance.transform.position, KnownContainers.Peek().transform.position) < 1.5;
                foundItem = KnownContainers.Peek().GetInventory().GetAllItems().Where(i => Items.Any(it => i.m_shared.m_name == it.m_itemData.m_shared.m_name)).RandomOrDefault();
            }
            if (!KnownContainers.Any() || (isCloseToContainer && foundItem == null))
            {
                Container nearbyChest = FindRandomNearbyContainer(instance.transform.position, KnownContainers, AcceptedContainerNames);
                if (nearbyChest != null)
                {
                    KnownContainers.Push(nearbyChest);
                    //return ("FoundContainer", null);
                }
                else
                {
                    KnownContainers.Clear();
                    return ("CannotFindContainers", null);
                }
            }
            if (!isCloseToContainer)
            {
                Invoke<MonsterAI>(instance, "MoveAndAvoid", dt, KnownContainers.Peek().transform.position, 0.5f, false);
                return ("MovingtoContainer", null);
            }
            else if (!KnownContainers.Peek()?.IsInUse() ?? false)
            {
                Debug.Log("Open chest");
                KnownContainers.Peek().SetInUse(inUse: true);
                return ("OpenContainer", null);
            }
            else if (foundItem != null)
            {
                Debug.Log("Item found, Close chest");

                KnownContainers.Peek().SetInUse(inUse: false);

                KnownContainers.Peek().GetInventory().RemoveItem(foundItem, 1);
                Invoke<Container>(KnownContainers.Peek(), "Save");
                Invoke<Inventory>(KnownContainers.Peek(), "Changed");
                return ("ItemFound", foundItem);
            }
            else
            {
                Debug.Log("Item not found, Close chest");
                KnownContainers.Peek().SetInUse(inUse: false);
            }
            return ("", null);
        }

        public static bool EatFromContainers(MonsterAI instance, ref MaxStack<Container> KnownContainers, string[] AcceptedContainerNames, float dt)
        {
            (string trigger, ItemDrop.ItemData foundItem) = SearchContainersforItems(instance, instance.m_consumeItems, ref KnownContainers, AcceptedContainerNames, dt);
            if (foundItem != null)
            {
                instance.m_onConsumedItem(instance.m_consumeItems.FirstOrDefault());
                return true;
            }
            return false;
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

    }
}
