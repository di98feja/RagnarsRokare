using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public class Common
    {
        public static ItemDrop GetNearbyItem(BaseAI instance, IEnumerable<ItemDrop.ItemData> acceptedItems, int range = 10)
        {
            return GetNearbyItem(instance, acceptedItems.Select(i => i.m_shared.m_name), range);
        }

        public static ItemDrop GetNearbyItem(BaseAI instance, IEnumerable<string> acceptedNames, int range = 10)
        {
            Vector3 position = instance.transform.position;
            ItemDrop ClosestObject = null;
            //Debug.Log($"{instance.GetComponent<Character>().GetHoverName()}: AcceptedNames:{string.Join(",",acceptedNames)}");
            foreach (Collider collider in Physics.OverlapSphere(position, range, LayerMask.GetMask(new string[] { "item" })))
            {
                ItemDrop item = collider.transform?.GetComponentInParent<ItemDrop>();
                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                {
                    continue;
                }
                //Debug.Log($"Item:{item.GetHoverName()}, CanSee:{CanSeeTarget(instance, new Collider[] { collider })}");
                if (item?.transform?.position != null && acceptedNames.Contains(item.m_itemData.m_shared.m_name) && CanSeeTarget(instance, new Collider[] { collider }) && (ClosestObject == null || Vector3.Distance(position, item.transform.position) < Vector3.Distance(position, ClosestObject.transform.position)))
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
                .Where(p => acceptedPickables.Contains(Utils.GetPrefabName(p.gameObject.name)))
                .Where(p => !string.IsNullOrEmpty(p.GetHoverText()))
                .Where(p => p.transform?.position != null)
                .Where(p => CanSeeTarget(instance, p.GetComponentsInChildren<Collider>()))
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
                if (mustBeVisible && !CanSeeTarget(instance, new Collider[] { collider })) continue;
                if (!string.IsNullOrEmpty(itemName) && item.m_itemData.m_shared.m_name != itemName) continue;
                if ((ClosestObject == null || Vector3.Distance(position, item.transform.position) < Vector3.Distance(position, ClosestObject.transform.position)))
                {
                    ClosestObject = item;
                }
            }
            return ClosestObject;
        }

        public static Assignment FindRandomNearbyAssignment(BaseAI instance, List<string> trainedAssignments, MaxStack<Assignment> knownassignments, float assignmentSearchRadius)
        {
            return FindRandomNearbyAssignment(instance, trainedAssignments, knownassignments as IEnumerable<Assignment>, assignmentSearchRadius, null);
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
            var allAssignablePieces = pieceList.Where(p => Assignment.AssignmentTypes.Any(a => Utils.GetPrefabName(p.name) == a.PieceName && trainedAssignments.Contains(Utils.GetPrefabName(p.name)))); //&& CanSeeTarget(instance, p.gameObject)
            //Common.Dbgl($"Assignments found 1: {allAssignablePieces.Select(n => n.name).Join()}", "Extraction");
            if (acceptedAssignmentTypes != null)
            {
                allAssignablePieces = allAssignablePieces.Where(p => acceptedAssignmentTypes.Any(a => a.PieceName == Utils.GetPrefabName(p.name)));
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
            var visibleAssignments = new List<Piece>();
            foreach (var assignment in newAssignments)
            {
                Dbgl($"{instance.gameObject.GetComponent<Character>().GetHoverName()}:Looking for {assignment.name}", true);
                var allColliders = assignment.GetComponentsInChildren<Collider>();
                allColliders.AddRangeToArray(assignment.GetComponents<Collider>());
                if (CanSeeTarget(instance, allColliders))
                {
                    visibleAssignments.Add(assignment);
                }
            }
            if (!visibleAssignments.Any())
            {
                return null;
            }

            // select random piece
            //var random = new System.Random();
            //int index = random.Next(newAssignments.Count());
            var selekted = visibleAssignments.RandomOrDefault();
            Common.Dbgl($"Returning assignment: {selekted.name}", true);
            Assignment randomAssignment = new Assignment(selekted);
            return randomAssignment;
        }

        [System.Obsolete]
        public static Container FindRandomNearbyContainer(BaseAI instance, MaxStack<Container> knownContainers, string[] m_acceptedContainerNames, float containerSearchRadius)
        {
            return FindRandomNearbyContainer(instance, knownContainers as IEnumerable<Container>, m_acceptedContainerNames, containerSearchRadius, Vector2.zero);
        }

        public static Container FindRandomNearbyContainer(BaseAI instance, MaxStack<Container> knownContainers, string[] m_acceptedContainerNames, float containerSearchRadius, Vector3 centerPoint)
        {
            return FindRandomNearbyContainer(instance, knownContainers as IEnumerable<Container>, m_acceptedContainerNames, containerSearchRadius, centerPoint);
        }

        public static Container FindRandomNearbyContainer(BaseAI instance, IEnumerable<Container> knownContainers, string[] m_acceptedContainerNames, float containerSearchRadius, Vector3 centerPoint)
        {
            Common.Dbgl($"Enter {nameof(FindRandomNearbyContainer)}, looking for {m_acceptedContainerNames.Join(delimiter:":")} within {containerSearchRadius}", true, "Sorter");
            if (centerPoint == Vector3.zero)
            {
                centerPoint = instance.transform.position;
            }
            var pieceList = new List<Piece>();
            Piece.GetAllPiecesInRadius(centerPoint, containerSearchRadius, pieceList);
            var allcontainerPieces = pieceList.Where(p => m_acceptedContainerNames.Contains(Utils.GetPrefabName(p.name))); //&& CanSeeTarget(instance, p.gameObject)
            var containers = allcontainerPieces.Select(p => p.GetContainer()).Where(c => !knownContainers.Contains(c)).ToList();
            //Common.Dbgl($"Seen containers:{containers.Where(c => CanSeeTarget(instance, c.GetComponent<StaticTarget>()?.GetAllColliders()?.ToArray()))?.Count() ?? -1}", true, "Sorter");
            var seenContainers = containers.Where(c => CanSeeTarget(instance, c.GetStaticTarget()?.GetAllColliders()?.ToArray())).ToList();
            if (!seenContainers.Any())
            {
                Common.Dbgl("No containers found, returning null", true, "Sorter");
                return null;
            }
            // select random piece
            return seenContainers.RandomOrDefault();
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

        public static void Dbgl(string str, bool pref = true)
        {
            Dbgl(str, pref, "");
        }

        public static void Dbgl(string str = "", bool pref = true, string filter = "")
        {
            if (CommonConfig.PrintDebugLog.Value && filter.Contains(CommonConfig.PrintDebugLogFilter.Value))
            {
                Debug.Log((pref ? typeof(MobAILib).Namespace + " " : "") + str);
            }
        }

        public static bool CanSeeTarget(BaseAI instance, GameObject gameObject)
        {
            var target = gameObject.GetComponent<StaticTarget>();
            if ((bool)target)
            {
                return CanSeeTarget(instance, target.GetAllColliders().ToArray());
            }
            else
            {
                return CanSeeTarget(instance, gameObject.GetComponentsInChildren<Collider>());
            }
        }

        public static bool CanSeeTarget(BaseAI instance, Collider[] allColliders)
        {
            if (allColliders == null) return false;
            if (allColliders.Length == 0) return false;

            Vector3 eyesPosition = instance.GetComponent<Character>().m_eye.position;
            var bounds = allColliders[0].bounds;
            foreach (var collider in allColliders)
            {
                bounds.Encapsulate(collider.bounds);
            }
            //Debug.Log($"num colliders:{allColliders.Length}, Bounds:{bounds}");
            if (Vector3.Distance(bounds.center, eyesPosition) > instance.m_viewRange)
            {
                //Debug.Log($"To far");
                return false;
            }
            Vector3 rhs = bounds.center - eyesPosition;

            Vector3 rhs_temp = rhs;
            Vector3 beam = rhs;
            bool visible = true;
            //Dbgl($"Target colliders:{allColliders.Length}", true, "Sorter");
            var viewBlockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "viewblock", "vehicle");
            for (int step = 0; step < 10; step++)
            {
                rhs_temp = Quaternion.AngleAxis(step, Vector3.up) * rhs;
                bool outsideBounds = Mathf.Asin(Mathf.Abs(Vector3.Angle(rhs, rhs_temp))) * rhs_temp.magnitude > Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                //Debug.Log($"Angle:{Vector3.Angle(rhs, rhs_temp)}, side:{Mathf.Asin(Mathf.Abs(Vector3.Angle(rhs, rhs_temp))) * rhs_temp.magnitude}, extent:{Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z))}");
                if(outsideBounds)
                {
                    //Debug.Log($"Can't see 1");
                    return false;
                }
                for (int rotation = 0; rotation < 360; rotation += 36 / System.Math.Max(step, 1))
                {
                    beam = Quaternion.AngleAxis(rotation, rhs) * rhs_temp;
                    int numHits = Physics.RaycastNonAlloc(eyesPosition, beam.normalized, m_tempRaycastHits, rhs.magnitude, viewBlockMask);
                    visible = true; 
                    //Dbgl($"Step {step}, Rotation {rotation}: numColliders:{numHits}", true, "Sorter");
                    for (var i = 0; i < numHits; i++)
                    {
                        visible &= allColliders.Contains(m_tempRaycastHits[i].collider);
                    }
                    if (visible)
                    {
                        //Debug.Log("Can see!");
                        //Dbgl($"detected at angle {rotation}, {step}", true, "Sorter");
                        return true;
                    }
                    if (step == 0)
                    {
                        break;
                    }
                }
            }
            //Debug.Log($"Can't see");
            return false;
        }

        private static readonly RaycastHit[] m_tempRaycastHits = new RaycastHit[128];

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
            bool isLookingAtTarget = (bool)Utils.Invoke<MonsterAI>(mob.Instance, "IsLookingAt", position, 10f);
            if (isLookingAtTarget) return true;

            Utils.Invoke<MonsterAI>(mob.Instance, "LookAt", position);
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

        public static List<Character> FindClosestEnemies(Character me, Vector3 position, float maxDistance)
        {
            var characters = new List<Character>();
            var enemyCharactersInRange = characters.Where(p => (BaseAI.IsEnemy(me, p) && Vector3.Distance(p.transform.position, position) < maxDistance)).ToList();
            if (!enemyCharactersInRange.Any())
            {
                return null;
            }
            return (List<Character>)enemyCharactersInRange.OrderBy(c => Vector3.Distance(position, c.transform.position));
        }
    }
}