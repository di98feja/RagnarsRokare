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
                if (item?.transform?.position != null && acceptedNames.Contains(item.m_itemData.m_shared.m_name) && CanSeeTarget(instance, collider) && (ClosestObject == null || Vector3.Distance(position, item.transform.position) < Vector3.Distance(position, ClosestObject.transform.position)))
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Pickable GetNearbyPickable(BaseAI instance, IEnumerable<string> acceptedPickables, int range = 10, IEnumerable<string> acceptedItemNames = null)
        {
            if (!acceptedPickables.Any())
            {
                return null;
            }
            return Physics.OverlapSphere(instance.transform.position, range, LayerMask.GetMask(new string[] { "Default_small", "piece", "item", "piece_nonsolid" }))
                .Select(c => c.transform?.GetComponentInParent<Pickable>())
                .Where(p => p != null)
                .Where(p => p.GetComponent<ZNetView>()?.IsValid() ?? false)
                .Where(p => acceptedPickables.Contains(GetPrefabName(p.gameObject.name)))
                .Where(p => !string.IsNullOrEmpty(p.GetHoverText()))
                .Where(p => p.transform?.position != null)
                .Where(p => CanSeeTarget(instance, p.gameObject))
                .Where(p => acceptedItemNames?.Contains(p.m_itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name) ?? true)
                .OrderBy(p => Vector3.Distance(instance.transform.position, p.transform.position))
                .FirstOrDefault();
        }

        public static ItemDrop GetClosestItem(BaseAI instance, int range = 10, string itemName = null, bool mustBeVisible = true)
        {
            Vector3 position = instance.transform.position;
            ItemDrop ClosestObject = null;
            foreach (Collider collider in Physics.OverlapSphere(position, range, LayerMask.GetMask(new string[] { "item" })))
            {
                ItemDrop item = collider.transform?.GetComponentInParent<ItemDrop>();
                if (item?.GetComponent<ZNetView>()?.IsValid() != true || item?.transform?.position == null) continue;
                if (mustBeVisible && !CanSeeTarget(instance, collider)) continue;
                if (!string.IsNullOrEmpty(itemName) && item.m_itemData.m_shared.m_name != itemName) continue;
                if ((ClosestObject == null || Vector3.Distance(position, item.transform.position) < Vector3.Distance(position, ClosestObject.transform.position)))
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Assignment FindRandomNearbyAssignment(BaseAI instance, IEnumerable<string> trainedAssignments, IEnumerable<Assignment> knownassignments, float assignmentSearchRadius, AssignmentType[] acceptedAssignmentTypes = null)
        {
            //Common.Dbgl($"Enter {nameof(FindRandomNearbyAssignment)}", "Extraction");
            Vector3 position = instance.transform.position;
            //Generate list of acceptable assignments
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, assignmentSearchRadius, pieceList);
            //Common.Dbgl($"Pieces in readius: {pieceList.Select(n => n.name).Join()}", "Extraction");
            //Common.Dbgl($"Trained assignments: {trainedAssignments.Join()}", "Extraction");
            //Common.Dbgl($"Known assignments: {knownassignments.Select(a => a.TypeOfAssignment.Name).Join()}", "Extraction");
            //Common.Dbgl($"Accepted assignments: {acceptedAssignmentTypes.Select(a => a.Name).Join()}", "Extraction");
            //Common.Dbgl($"Can see:{pieceList.Select(p => $"{p.name}:{CanSeeTarget(instance, p.GetComponentInParent<StaticTarget>())}").Join()}", "Extraction");
            var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => GetPrefabName(p.name) == a.PieceName && trainedAssignments.Contains(GetPrefabName(p.name)) && CanSeeTarget(instance, p.GetComponentInParent<StaticTarget>()))); //&& CanSeeTarget(instance, p.gameObject)
            //Common.Dbgl($"Assignments found 1: {allAssignablePieces.Select(n => n.name).Join()}", "Extraction");
            if (acceptedAssignmentTypes != null)
            {
                allAssignablePieces = allAssignablePieces.Where(p => acceptedAssignmentTypes.Any(a => a.PieceName == GetPrefabName(p.name)));
            }
            // no assignments detekted, return false
            if (!allAssignablePieces.Any())
            {
                return null;
            }
            //Common.Dbgl($"Assignments found 2: {allAssignablePieces.Select(n => n.name).Join()}", "Extraction");
            // filter out assignments already in list
            var newAssignments = allAssignablePieces.Where(p => !knownassignments.Any(a => a.AssignmentObject == p.gameObject));
            //Common.Dbgl($"Assignments after filter: {newAssignments.Select(n => n.name).Join()}", "Extraction");

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
            //Common.Dbgl($"Enter {nameof(FindRandomNearbyContainer)}, looking for {m_acceptedContainerNames.Join()} within {containerSearchRadius}");
            Vector3 position = instance.transform.position;
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, containerSearchRadius, pieceList);
            var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(GetPrefabName(p.name)) && CanSeeTarget(instance, p.GetComponentInParent<StaticTarget>())); //&& CanSeeTarget(instance, p.gameObject)
            var containers = allcontainerPieces?.Select(p => p.GetContainer()).Where(c => !knownContainers.Contains(c)).ToList(); 
            if (!containers.Any())
            {
                //Common.Dbgl("No containers found, returning null");
                return null;
            }
            // select random piece
            return containers.RandomOrDefault();
        }

        public static Container FindClosestContainer(Vector3 position, float containerSearchRadius)
        {
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, containerSearchRadius, pieceList);
            var containers = pieceList.Select(p => p.GetContainer()).Where(p => p is Container).ToList();
            if (!containers.Any())
            {
                return null;
            }
            // select closest
            return containers.OrderBy(c => Vector3.Distance(position, c.gameObject.transform.position)).FirstOrDefault();
        }

        public static Sign FindClosestSign(Vector3 position, float searchRadius) 
        {
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(position, searchRadius, pieceList);
            var matchingpieces = pieceList.Select(p => p.gameObject.GetComponentInChildren<Sign>()).Where(p => p is Sign).ToList();
            matchingpieces.AddRange(pieceList.Select(p => p.gameObject.GetComponent<Sign>()).Where(p => p is Sign));
            if (!matchingpieces.Any())
            {
                return null;
            }
            // select closest
            return matchingpieces.OrderBy(c => Vector3.Distance(position, c.gameObject.transform.position)).FirstOrDefault();
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
            var tempRaycastHits = Physics.RaycastAll(eyesPosition, rhs.normalized, rhs.magnitude, LayerMask.GetMask("Default", /*"terrain",*/ "static_solid", "Default_small", "piece", "viewblock", "vehicle", "item")); //, "terrain"
            //Debug.Log("#############################################");
            //Debug.Log($"Target: {item.name}, id:{item.GetInstanceID()} pos: {item.transform.position}");
            //foreach (RaycastHit RaycastHit in tempRaycastHits)
            //{
            //    Debug.Log($"RaycastHit:Collider:{RaycastHit.collider.GetInstanceID()}, id:{RaycastHit.collider.gameObject.GetInstanceID()}, pos:{RaycastHit.collider.transform.position}");
            //}

            return tempRaycastHits.All(h => Vector3.Distance(h.collider.transform.position, itemPosition) < 0.2f);

            //if (tempRaycastHits.Length < 2)
            //{
            //    return true;
            //}
            //return false;
        }

        private static RaycastHit[] m_tempRaycastHits = new RaycastHit[128];

        public static bool CanSeeTarget(BaseAI instance, StaticTarget target)
        {
            Vector3 center = target.GetCenter();
            if (Vector3.Distance(center, instance.transform.position) > instance.m_viewRange)
            {
                return false;
            }
            var character = instance.GetComponent<Character>();
            Vector3 rhs = center - character.m_eye.position;
            Vector3 rhs_temp = rhs;
            Vector3 beam = rhs;
            bool seen = true;
            for (int step = 0; step < 10; step++)
            {
                rhs_temp = Quaternion.AngleAxis(step, Vector3.up) * rhs;
                for (int rotation = 0; rotation < 360; rotation += 36/System.Math.Max(step, 1))
                {
                    beam = Quaternion.AngleAxis(rotation, rhs) * rhs_temp;
                    List<Collider> allColliders = target.GetAllColliders();
                    var viewBlockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "viewblock", "vehicle");
                    int num = Physics.RaycastNonAlloc(character.m_eye.position, beam.normalized, m_tempRaycastHits, rhs.magnitude, viewBlockMask);
                    for (int i = 0; i < num; i++)
                    {
                        RaycastHit raycastHit = m_tempRaycastHits[i];
                        if (!allColliders.Contains(raycastHit.collider))
                        {
                            seen = false;
                        }
                    }
                    if (!seen)
                    {
                        Dbgl($"{character.GetHoverName()}: nothing at angle {rotation}, {step}");
                        continue;
                    }
                    Dbgl($"{character.GetHoverName()}: detected {target.gameObject.name} at angle {rotation}, {step}");
                    return true;
                }
            }
            Dbgl($"{character.GetHoverName()}: Nothing found");
            return false;
        }

        public static bool CanSeeTarget(BaseAI instance, Collider target)
        {
            if (!(bool)target) return false;

            Vector3 center = target.ClosestPoint(instance.transform.position);
            if (Vector3.Distance(center, instance.transform.position) > instance.m_viewRange)
            {
                return false;
            }
            var character = instance.GetComponent<Character>();
            Vector3 rhs = center - character.m_eye.position;
            var viewBlockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "viewblock", "vehicle");
            int num = Physics.RaycastNonAlloc(character.m_eye.position, rhs.normalized, m_tempRaycastHits, rhs.magnitude, viewBlockMask);
            for (int i = 0; i < num; i++)
            {
                RaycastHit raycastHit = m_tempRaycastHits[i];
                if (target != raycastHit.collider)
                {
                    return false;
                }
            }
            return true;
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

        public static Container GetContainerById(string uniqueId)
        {
            var allPieces = typeof(Piece).GetField("m_allPieces", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as IEnumerable<Piece>;
            return allPieces
                .Where(p => GetNView(p)?.IsValid() ?? false)
                .Select(p => p.GetContainer())
                .Where(c => (bool)c)
                .Where(p => GetOrCreateUniqueId(GetNView(p)) == uniqueId)
                .SingleOrDefault();
        }
    }
}