using HarmonyLib;
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

        public static ItemDrop GetNearbyItem(BaseAI instance, IEnumerable<string> acceptedNames, int range = 10)
        {
            Vector3 position = instance.transform.position;
            ItemDrop ClosestObject = null;
            foreach (Collider collider in Physics.OverlapSphere(position, range, LayerMask.GetMask(new string[] { "item" })))
            {
                ItemDrop item = collider.transform?.GetComponentInParent<ItemDrop>();
                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    continue;
                }
                if (item?.transform?.position != null && acceptedNames.Contains(item.m_itemData.m_shared.m_name) && CanSeeTarget(instance, item.gameObject) && (ClosestObject == null || Vector3.Distance(position, item.transform.position) < Vector3.Distance(position, ClosestObject.transform.position)))
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Pickable GetNearbyPickable(BaseAI instance, IEnumerable<string> acceptedNames, int range = 10)
        {
            if (!acceptedNames.Any())
            {
                return null;
            }
            //Debug.Log("GetNearbyPickable");
            Vector3 position = instance.transform.position;
            Pickable ClosestObject = null;
            foreach (Collider collider in Physics.OverlapSphere(position, range, LayerMask.GetMask(new string[] { "Default_small", "piece", "item" })))
            {
                Pickable pickable = collider?.transform?.GetComponentInParent<Pickable>();
                if (pickable == null || pickable?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    continue;
                }
                if (pickable?.transform?.position != null && CanSeeTarget(instance, pickable.gameObject) && acceptedNames.Contains(Common.GetPrefabName(pickable.gameObject.name)) && (ClosestObject == null || Vector3.Distance(position, pickable.transform.position) < Vector3.Distance(position, ClosestObject.transform.position))) //  
                {
                    ClosestObject = pickable;
                }
            }
            //Debug.Log($"Pickable detekted: {ClosestObject?.gameObject.name} containing {ClosestObject?.m_itemPrefab.name} in {acceptedNames.Join()}.");
            return ClosestObject;
        }

        public static ItemDrop GetClosestItem(BaseAI instance, int range = 10)
        {
            Vector3 position = instance.transform.position;
            ItemDrop ClosestObject = null;
            foreach (Collider collider in Physics.OverlapSphere(position, range, LayerMask.GetMask(new string[] { "item" })))
            {
                ItemDrop item = collider.transform?.GetComponentInParent<ItemDrop>();
                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    continue;
                }
                if (item?.transform?.position != null && CanSeeTarget(instance, item.gameObject) && (ClosestObject == null || Vector3.Distance(position, item.transform.position) < Vector3.Distance(position, ClosestObject.transform.position)))
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Assignment FindRandomNearbyAssignment(BaseAI instance, List<string> trainedAssignments, MaxStack<Assignment> knownassignments, float assignmentSearchRadius)
        {
            Common.Dbgl($"Enter {nameof(FindRandomNearbyAssignment)}");
            Vector3 position = instance.transform.position;
            //Generate list of acceptable assignments
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, assignmentSearchRadius, pieceList);
            var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => GetPrefabName(p.name) == a.PieceName && trainedAssignments.Contains(GetPrefabName(p.name)) && CanSeeTarget(instance, p.gameObject))); //&& CanSeeTarget(instance, p.gameObject)
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

        public static Container FindRandomNearbyContainer(BaseAI instance, IEnumerable<Container> knownContainers, string[] m_acceptedContainerNames, float containerSearchRadius)
        {
            //Common.Dbgl($"Enter {nameof(FindRandomNearbyContainer)}, looking for {m_acceptedContainerNames.Join()}");
            Vector3 position = instance.transform.position;
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, containerSearchRadius, pieceList);
            var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(GetPrefabName(p.name)) && CanSeeTarget(instance, p.gameObject)); //&& CanSeeTarget(instance, p.gameObject)
            var containers = allcontainerPieces?.Select(p => p.gameObject.GetComponentInChildren<Container>()).Where(c => !knownContainers.Contains(c)).ToList(); 
            containers.AddRange(allcontainerPieces?.Select(p => p.gameObject.GetComponent<Container>()).Where(c => !knownContainers.Contains(c)));
            if (!containers.Any())
            {
                //Common.Dbgl("No containers found, returning null");
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


        public static void Dbgl(string str = "", string filter = "", bool pref = true)
        {
            if (CommonConfig.PrintDebugLog.Value && filter.Contains(CommonConfig.PrintDebugLogFilter.Value))
            {
                Debug.Log((pref ? typeof(MobAILib).Namespace + " " : "") + str);
            }
        }

        public static bool CanSeeTarget(BaseAI instance, GameObject item)
        {
            Vector3 eyesPosition = instance.GetComponent<Character>().m_eye.position;
            Vector3 itemPosition = item.transform.position;
            if (Vector3.Distance(itemPosition, eyesPosition) > instance.m_viewRange)
            {
                return false;
            }
            Vector3 rhs = itemPosition - eyesPosition;
            var tempRaycastHits = Physics.RaycastAll(eyesPosition, rhs.normalized, rhs.magnitude, LayerMask.GetMask("Default", /*"terrain",*/ "static_solid", "Default_small", "piece", "viewblock", "vehicle")); //, "terrain"
            //Debug.Log("#############################################");
            //Debug.Log($"RaycastHit: {item.name} pos: {item.transform.position}");
            //foreach (RaycastHit RaycastHit in tempRaycastHits)
            //{
            //    Debug.Log($"RaycastHit: {RaycastHit.collider.name} pos:  {RaycastHit.collider.transform.position}");
            //}

            if (tempRaycastHits.Length < 2)
            {
                return true;
            }
            return false;
        }

        public static bool Alarmed(BaseAI instance, float Awareness)
        {
            foreach (Character allCharacter in Character.GetAllCharacters())
            {
                if (BaseAI.IsEnemy(instance.GetComponent<Character>(), allCharacter) && instance.CanSenseTarget(allCharacter) && Vector3.Distance(instance.transform.position, allCharacter.transform.position) < Awareness * 5)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool TurnToFacePosition(MobAIBase mob, Vector3 position)
        {
            bool isLookingAtTarget = (bool)Invoke<MonsterAI>(mob.Instance, "IsLookingAt", position, 10f);
            if (isLookingAtTarget) return true;

            Invoke<MonsterAI>(mob.Instance, "LookAt", position);
            return false;
        }
    }
}