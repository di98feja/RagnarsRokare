using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public partial class MobAILib
    {
        [HarmonyPatch(typeof(ZoneSystem), "Update")]
        static class ZoneSystem_Update_Patch
        {
            static float m_AIupdateTimer = 0f;

            static void Postfix(ref ZoneSystem __instance)
            {
                if (ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected)
                {
                    return;
                }
                m_AIupdateTimer += Time.deltaTime;
                if (!(m_AIupdateTimer > 0.5f))
                {
                    return;
                }
                m_AIupdateTimer = 0f;
                var mobZDOs = MobManager.AliveMobs.Values.Select(m => ZDOMan.instance.GetZDO(m.ZDOId));
                if (!mobZDOs.Any()) return;

                foreach (var mob in mobZDOs)
                {
                    bool flag = (bool)typeof(ZoneSystem).GetMethod("CreateLocalZones", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { mob.GetPosition() });
                }
                //Debug.Log($"Player zone:{ZoneSystem.instance.GetZone(Player.m_localPlayer.transform.position)}, mobAIZone:{ZoneSystem.instance.GetZone(mob.GetPosition)}, isZoneLoaded:{ZoneSystem.instance.IsZoneLoaded(ZoneSystem.instance.GetZone(mob.Character.transform.position))}");
                //var mobPositions =  MobManager.AliveMobs.Values.Select(m => m.Character.transform.position);
                //foreach (var mob in mobPositions)
                //{
                //	bool flag = (bool)typeof(ZoneSystem).GetMethod("CreateLocalZones", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { ZNet.instance.GetReferencePosition() });
                //}

                //ZNetScene.CreateDestroyObjects
                //ZDOMan.ReleaseZDOS
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "SendZDOToPeers")]
        static class ZDOMan_SendZDOToPeers_Patch
        {
            static void Prefix(float ___m_sendTimer, float dt)
            {
                if (___m_sendTimer + dt > 0.05f)
                {
                    ZoneWorkloadManager.DistributeOrphanedZones();
                }
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "FindSectorObjects", typeof(Vector2i), typeof(int), typeof(int), typeof(List<ZDO>), typeof(List<ZDO>))]
        static class ZDOMan_FindSectorObjects_Patch
        {
            static void Postfix(ZDOMan __instance, ref List<ZDO> distantSectorObjects)
            {
                if (distantSectorObjects == null) return;
                //Debug.Log($"Filling sectorObjects with orphanedZones");
                foreach (var zone in ZoneWorkloadManager.OrphanedZonesWithAIMobs())
                {
                    typeof(ZDOMan).GetMethod("FindObjects", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ZDOMan.instance, new object[] { zone, distantSectorObjects });
                }
            }
        }

        public static class ZoneWorkloadManager
        {
            public static void DistributeOrphanedZones()
            {
                var orphanedZones = OrphanedZonesWithAIMobs();
                if (!orphanedZones.Any()) return;
                //Debug.Log($"orphanedZones:{string.Join(",", orphanedZones.Select(z => z.ToString()))}");

                var parent = (long)typeof(ZDOMan).GetField("m_myid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ZDOMan.instance);
                var objs = new List<ZDO>();
                foreach (var zone in orphanedZones)
                {
                    ZDOMan.instance.FindSectorObjects(zone, ZoneSystem.instance.m_activeArea, 0, objs);
                    foreach (var obj in objs)
                    {
                        if (obj.m_owner == 0L)
                        {
                            obj.SetOwner(parent);
                        }
                    }
                    objs.Clear();
                }

                //var availablePeers = typeof(ZNet).GetField("m_peers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ZNet.instance) as IEnumerable<ZNetPeer>;

            }

            public static IEnumerable<Vector2i> OrphanedZonesWithAIMobs()
            {
                var mobZDOs = MobManager.AliveMobs.Values.Select(m => ZDOMan.instance.GetZDO(m.ZDOId));
                //Debug.Log($"Num mobs:{mobZDOs.Count()}");
                var allActiveZones = AllActiveZones();
                //Debug.Log($"Num active zones:{allActiveZones.Count()}");
                var orphanedMobs = mobZDOs.Where(m => !allActiveZones.Contains(ZoneSystem.instance.GetZone(m.GetPosition())));
                //Debug.Log($"orphanedMobs:{string.Join(",", orphanedMobs.Select(z => z.GetString(Constants.Z_GivenName)))}");

                foreach (var orphan in orphanedMobs)
                {
                    yield return ZoneSystem.instance.GetZone(orphan.GetPosition());
                }
            }

            public static IEnumerable<Vector2i> AllActiveZones()
            {
                var activeZones = GetActiveZonesAroundPosition(ZNet.instance.GetReferencePosition());
                foreach (var peer in typeof(ZNet).GetField("m_peers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ZNet.instance) as IEnumerable<ZNetPeer>)
                {
                    if (peer.m_refPos == Vector3.zero) continue;
                    activeZones = activeZones.Union(GetActiveZonesAroundPosition(peer.m_refPos));
                }
                return activeZones;
            }

            public static IEnumerable<Vector2i> GetActiveZonesAroundPosition(Vector3 position)
            {
                int areaSize = ZoneSystem.instance.m_activeArea;
                var zone = ZoneSystem.instance.GetZone(position);
                for (int i = zone.y - areaSize; i <= zone.y + areaSize; i++)
                {
                    for (int j = zone.x - areaSize; j <= zone.x + areaSize; j++)
                    {
                        yield return new Vector2i(j, i);
                    }
                }
            }
        }

    }
}