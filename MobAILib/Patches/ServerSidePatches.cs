using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace RagnarsRokare.MobAI.ServerPeer
{
    public partial class MobAILibServer
    {
        internal static Type m_zdoPeerType = typeof(ZDOMan).Assembly.GetType("ZDOMan+ZDOPeer");

        /// <summary>
        /// Include adpoted zones when creating Ghost zones
        /// This method is only called when the client is acting as server.
        /// </summary>
        [HarmonyPatch(typeof(ZoneSystem), "CreateGhostZones")]
        static class ZoneSystem_CreateGhostZones_Patch
        {
            static void Postfix(ZoneSystem __instance)
            {
                foreach (var peer in ZNet.instance.GetPeers().Where(p => !p.m_server))
                {
                    var peerAdoptedZones = AdoptedZonesManager.GetAdoptedZones(peer.m_uid);
                    foreach (var zone in peerAdoptedZones.CurrentZones)
                    {
                        if (!(bool)Common.Invoke<ZoneSystem>(__instance, "IsZoneGenerated", zone))
                        {
                            Debug.Log($"Spawning zone {zone} as Ghost");
                            Common.Invoke<ZoneSystem>(__instance, "SpawnZone", zone, ZoneSystem.SpawnMode.Ghost, null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gather nearby ZDOs from peer itself AND its adopted Mobs.
        /// Release nearby ZDO if owned by peer if no longer in their active zone OR not in an adopted zone.
        /// Take ownership of ZDO if it is in this peer active area OR in an adopted zone.
        /// This method is only called when the client is acting as server.
        /// ReleaseNearbyZDOs is started in a different thread since it can potentially take a long time.
        /// </summary>
        [HarmonyPatch(typeof(ZDOMan), "ReleaseZDOS")]
        static class ZDOMan_ReleaseZDOS_Patch
        {
            static bool Prefix(ref float ___m_releaseZDOTimer, float dt)
            {
                ___m_releaseZDOTimer += dt;
                if (!(___m_releaseZDOTimer > 2f))
                {
                    return false;
                }
                if (m_threadIsWorking)
                {
                    Debug.LogWarning($"To many AI zones, Server cannot keep up!");
                    return false;
                }
                ___m_releaseZDOTimer = 0f;
                AdoptedZonesManager.ResetAdoptedZones();

                var thread = new Thread(new ThreadStart(ReleaseNearbyZDOsAsync));
                thread.Start();
                ReleaseNearbyZDOsAsync();
                return false;
            }

            static bool m_threadIsWorking = false;
            private static void ReleaseNearbyZDOsAsync()
            {
                m_threadIsWorking = true;
                try
                {
                    ReleaseNearbyZDOS(ZNet.instance.GetReferencePosition(), ZDOMan.instance.GetMyID());
                    Type t = m_zdoPeerType;
                    var peers = typeof(ZDOMan).GetField("m_peers", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(ZDOMan.instance) as IEnumerable<object>;

                    foreach (var peer in peers)
                    {
                        var p = m_zdoPeerType.GetField("m_peer").GetValue(peer) as ZNetPeer;
                        ReleaseNearbyZDOS(p.m_refPos, p.m_uid);
                    }
                }
                finally
                {
                    m_threadIsWorking = false;
                }
            }

            static void ReleaseNearbyZDOS(Vector3 refPosition, long uid)
            {
                Vector2i zone = ZoneSystem.instance.GetZone(refPosition);
                var adoptedZones = AdoptedZonesManager.GetAdoptedZones(uid);
                //Debug.Log($"{uid} have  added {adoptedZones.AddedZones.Count} zones, removed {adoptedZones.RemovedZones.Count} to a total of {adoptedZones.CurrentZones.Count}");
                foreach (var adoptedZone in adoptedZones.AddedZones)
                {
                    var addedAdoptedObjects = new List<ZDO>();
                    Common.Invoke<ZDOMan>(ZDOMan.instance, "FindObjects", adoptedZone, addedAdoptedObjects);
                    foreach (ZDO zdo in addedAdoptedObjects)
                    {
                        zdo.SetOwner(uid);
                    }
                }
                foreach (var adoptedZone in adoptedZones.RemovedZones)
                {
                    var removedAdoptedObjects = new List<ZDO>();
                    Common.Invoke<ZDOMan>(ZDOMan.instance, "FindObjects", adoptedZone, removedAdoptedObjects);
                    foreach (ZDO zdo in removedAdoptedObjects)
                    {
                        zdo.SetOwner(0L);
                    }
                }
                List<ZDO> m_tempNearObjects = Traverse.Create(ZDOMan.instance).Field("m_tempNearObjects").GetValue<List<ZDO>>();
                m_tempNearObjects.Clear();
                ZDOMan.instance.FindSectorObjects(zone, ZoneSystem.instance.m_activeArea, 0, m_tempNearObjects, null);
                foreach (ZDO zdo in m_tempNearObjects)
                {
                    if (!zdo.m_persistent) continue;

                    var zdoSector = zdo.GetSector();

                    if (zdo.m_owner == uid)
                    {
                        if (!ZNetScene.instance.InActiveArea(zdoSector, zone))
                        {
                            zdo.SetOwner(0L);
                        }
                    }
                    else if (zdo.m_owner == 0L || !new Traverse(ZDOMan.instance).Method("IsInPeerActiveArea", new object[] { zdoSector, zdo.m_owner }).GetValue<bool>())
                    {
                        if (ZNetScene.instance.InActiveArea(zdoSector, zone))
                        {
                            zdo.SetOwner(uid);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add the adopted zones to the list of ZDOs that is beeing sent to the peers
        /// This method is only called when the client is acting as server.
        /// </summary>
        [HarmonyPatch(typeof(ZDOMan), "CreateSyncList")]
        static class ZDOMan_CreateSyncList_Patch
        {
            static bool Prefix(object peer, ref List<ZDO> toSync, ref List<ZDO> ___m_tempToSyncDistant)
            {
                if (ZNet.instance.IsServer())
                {
                    var p = m_zdoPeerType.GetField("m_peer").GetValue(peer) as ZNetPeer;
                    Vector3 refPos = p.GetRefPos();
                    Vector2i zone = ZoneSystem.instance.GetZone(refPos);
                    ___m_tempToSyncDistant.Clear();
                    ZDOMan.instance.FindSectorObjects(zone, ZoneSystem.instance.m_activeArea, ZoneSystem.instance.m_activeDistantArea, toSync, ___m_tempToSyncDistant);
                    foreach (var az in AdoptedZonesManager.GetAdoptedZones(p.m_uid).CurrentZones)
                    {
                        Common.Invoke<ZDOMan>(ZDOMan.instance, "FindObjects", zone, toSync);
                    }
                    Common.Invoke<ZDOMan>(ZDOMan.instance, "ServerSortSendZDOS", toSync, refPos, peer);
                    toSync.AddRange(___m_tempToSyncDistant);
                    Common.Invoke<ZDOMan>(ZDOMan.instance, "AddForceSendZdos", peer, toSync);
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// After loading the game data, init mobs
        /// This method is only called when the client is acting as server.
        /// </summary>
        [HarmonyPatch(typeof(ZDOMan), "Load")]
        static class ZDOMan_Load_Patch
        {
            static void Postfix(ref ZDOMan __instance)
            {
                AdoptedZonesManager.LoadMobs(__instance);
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "ShutDown")]
        static class ZDOMan_ShutDown_Patch
        {
            static void Postfix(ref ZDOMan __instance)
            {
                AdoptedZonesManager.UnloadMobs();
            }
        }
    }
}