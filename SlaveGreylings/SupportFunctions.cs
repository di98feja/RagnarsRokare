using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SlaveGreylings
{
    public partial class SlaveGreylings
    {
        public static ItemDrop GetNearbyItem(Vector3 center, List<ItemDrop.ItemData> acceptedNames, int range = 10)
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

        public static bool FindRandomNearbyAssignment(int instanceId, Vector3 greylingPosition)
        {
            Dbgl($"Enter {nameof(FindRandomNearbyAssignment)}");
            //Generate list of acceptable assignments
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(greylingPosition, (float)GreylingsConfig.AssignmentSearchRadius.Value, pieceList);
            var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => GetPrefabName(p.name) == a.PieceName && a.Activated));
            // no assignments detekted, return false
            if (!allAssignablePieces.Any())
            {
                return false;
            }

            // filter out assignments already in list
            var newAssignments = allAssignablePieces.Where(p => !m_assignment[instanceId].Any(a => a.AssignmentObject == p.gameObject));

            // filter out inaccessible assignments
            //newAssignments = newAssignments.Where(p => Pathfinding.instance.GetPath(greylingPosition, p.gameObject.transform.position, null, Pathfinding.AgentType.Humanoid, true, true));

            if (!newAssignments.Any())
            {
                return false;
            }

            // select random piece
            var random = new System.Random();
            int index = random.Next(newAssignments.Count());
            Assignment randomAssignment = new Assignment(instanceId, newAssignments.ElementAt(index));
            // Create assignment and return true
            m_assignment[instanceId].Push(randomAssignment);
            m_assigned[instanceId] = true;
            m_assignedTimer[instanceId] = 0;
            m_fetchitems[instanceId].Clear();
            m_spottedItem[instanceId] = null;
            return true;
        }

        public static Container FindRandomNearbyContainer(Vector3 greylingPosition, MaxStack<Container> knownContainers)
        {
            Dbgl($"Enter {nameof(FindRandomNearbyContainer)}");
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(greylingPosition, (float)GreylingsConfig.ContainerSearchRadius.Value, pieceList);
            var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(GetPrefabName(p.name)));
            // no containers detected, return false

            var containers = allcontainerPieces?.Select(p => p.gameObject.GetComponent<Container>()).Where(c => !knownContainers.Contains(c));
            if (!containers.Any())
            {
                return null;
            }

            // select random piece
            var random = new System.Random();
            int index = random.Next(containers.Count());
            return containers.ElementAt(index);
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
    }
}