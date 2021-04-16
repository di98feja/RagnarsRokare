using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(BaseAI), "UpdateAI")]
        class BaseAI_UpdateAI_ReversePatch
        {
            [HarmonyReversePatch]
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void UpdateAI(BaseAI instance, float dt, ZNetView m_nview, ref float m_jumpInterval, ref float m_jumpTimer,
                ref float m_randomMoveUpdateTimer, ref float m_timeSinceHurt, ref bool m_alerted)
            {
                if (m_nview.IsOwner())
                {
                    instance.UpdateTakeoffLanding(dt);
                    if (m_jumpInterval > 0f)
                    {
                        m_jumpTimer += dt;
                    }
                    if (m_randomMoveUpdateTimer > 0f)
                    {
                        m_randomMoveUpdateTimer -= dt;
                    }
                    typeof(BaseAI).GetMethod("UpdateRegeneration", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { dt });
                    m_timeSinceHurt += dt;
                }
                else
                {
                    m_alerted = m_nview.GetZDO().GetBool("alert");
                }
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        static class MonsterAI_MakeTame_Patch
        {
            static void Postfix(MonsterAI __instance)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    __instance.m_consumeItems.Clear();
                    __instance.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Resin").FirstOrDefault());
                    __instance.m_consumeSearchRange = 50;
                }
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        static class MonsterAI_UpdateAI_Patch
        {

            static MonsterAI_UpdateAI_Patch()
            {
            }

            static bool Prefix(MonsterAI __instance, float dt, ref ZNetView ___m_nview, ref Character ___m_character, ref float ___m_fleeIfLowHealth,
                ref float ___m_timeSinceHurt, ref Vector3 ___arroundPointTarget, ref float ___m_jumpInterval, ref float ___m_jumpTimer,
                ref float ___m_randomMoveUpdateTimer, ref bool ___m_alerted, ref Tameable ___m_tamable)
            {
                if (!___m_nview.IsOwner())
                {
                    return false;
                }
                if (!___m_character.IsTamed())
                {
                    return true;
                }
                if (!__instance.name.Contains("Greyling"))
                {
                    return true;
                }
                if (__instance.IsSleeping())
                {
                    Invoke(__instance, "UpdateSleep", new object[] { dt });
                    Dbgl($"{___m_character.GetHoverName()}: Sleep updated");
                    return false;
                }

                BaseAI_UpdateAI_ReversePatch.UpdateAI(__instance, dt, ___m_nview, ref ___m_jumpInterval, ref ___m_jumpTimer, ref ___m_randomMoveUpdateTimer, ref ___m_timeSinceHurt, ref ___m_alerted);
                string mobId = InitInstanceIfNeeded(__instance);
                MobManager.Mobs[mobId].UpdateAI(__instance, dt);

                return false;
            }

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

            private static bool FindRandomNearbyAssignment(int instanceId, Vector3 greylingPosition)
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

            private static Container FindRandomNearbyContainer(Vector3 greylingPosition, MaxStack<Container> knownContainers)
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

            private static object Invoke(MonsterAI instance, string methodName, object[] argumentList)
            {
                return typeof(MonsterAI).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
            }

            private static string InitInstanceIfNeeded(MonsterAI instance)
            {
                var character = Traverse.Create(instance).Field("m_character").GetValue(instance) as Character;
                var nview = Traverse.Create(character).Field("m_nview").GetValue(character) as ZNetView;
                var uniqueId = nview.GetZDO().GetString(Constants.Z_CharacterId);

                if (!MobManager.IsControlledMob(uniqueId))
                {
                    var mob = new GreylingAI();
                    MobManager.Mobs.Add(uniqueId, mob);
                }
                return uniqueId;
            }


            private static string GetPrefabName(string name)
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
}
