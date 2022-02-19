using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI.Server
{
    public partial class MobAILibServer
    {
        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        static class ZoneSystem_Start_Patch
        {
            static void Postfix()
            {
                AdoptedZonesManager.RegisterRPCs();
            }
        }

        /// <summary>
        /// Include adpoted zones when creating Ghost zones
        /// </summary>
        [HarmonyPatch(typeof(ZoneSystem), "CreateGhostZones")]
        static class ZoneSystem_CreateGhostZones_Patch
        {
            static void Postfix(ZoneSystem __instance)
            {
                foreach (var peer in ZNet.instance.GetPeers().Where(p => !p.m_server))
                {
                    var peerAdoptedZones = AdoptedZonesManager.GetAdoptedZones(peer.m_uid);
                    foreach (var zone in peerAdoptedZones)
                    {
                        if (!(bool)Invoke<ZoneSystem>(__instance, "IsZoneGenerated", zone))
                        {
                            Debug.Log($"Spawning zone {zone} as Ghost");
                            Invoke<ZoneSystem>(__instance, "SpawnZone", zone, ZoneSystem.SpawnMode.Ghost, null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gather nearby ZDOs from peer itself AND its adopted Mobs.
        /// Release nearby ZDO if owned by peer if no longer in their active zone OR not in an adopted zone.
        /// Take ownership of ZDO if it is in this peer active area OR in an adopted zone.
        /// </summary>
        [HarmonyPatch(typeof(ZDOMan), "ReleaseNearbyZDOS")]
		public static class ZDOMan_ReleaseNearbyZDOS_Patch
		{
			static bool Prefix(ZDOMan __instance, ref Vector3 refPosition, ref long uid)
			{
				long serverId = Traverse.Create(__instance).Field("m_myid").GetValue<long>();
				if (serverId == uid)
                {
					// Server is first in line, reset adopted zones.
					AdoptedZonesManager.ResetAdoptedZones();
                }

				Vector2i zone = ZoneSystem.instance.GetZone(refPosition);
				var adoptedZones = AdoptedZonesManager.GetAdoptedZones(uid);
				Debug.Log($"{uid} have adopted {adoptedZones.Count()} zones");
				List<ZDO> m_tempNearObjects = Traverse.Create(__instance).Field("m_tempNearObjects").GetValue<List<ZDO>>();

				m_tempNearObjects.Clear();
				__instance.FindSectorObjects(zone, ZoneSystem.instance.m_activeArea, 0, m_tempNearObjects, null);
				foreach (var adoptedZone in adoptedZones)
                {
					__instance.FindSectorObjects(adoptedZone, ZoneSystem.instance.m_activeArea, 0, m_tempNearObjects, null);
				}
                int zdoCount = 0;
				foreach (ZDO zdo in m_tempNearObjects)
				{
					if (!zdo.m_persistent) continue;

					var zdoSector = zdo.GetSector();

					if (zdo.m_owner == uid)
					{
                        zdoCount++;
						if (!ZNetScene.instance.InActiveArea(zdoSector, zone) && !adoptedZones.Any(s => s == zdoSector))
						{
							zdo.SetOwner(0L);
                            zdoCount--;
						}
					}
					else if (zdo.m_owner == 0L || !new Traverse(__instance).Method("IsInPeerActiveArea", new object[] { zdoSector, zdo.m_owner }).GetValue<bool>())
					{
						if (ZNetScene.instance.InActiveArea(zdoSector, zone) || adoptedZones.Any(s => s == zdoSector))
                        {
							zdo.SetOwner(uid);
                            zdoCount++;
						}
					}
				}
                Debug.Log($"Client {uid} now owns {zdoCount} ZDOs");
				return false;
			}
		}

        /// <summary>
        /// Add the adopted zones to the list of ZDOs that is beeing sent to the peers
        /// </summary>
        [HarmonyPatch(typeof(ZDOMan), "CreateSyncList")]
        static class ZDOMan_CreateSyncList_Patch
        {
            static bool Prefix(object peer, ref List<ZDO> toSync, ref List<ZDO> ___m_tempToSyncDistant)
            {
                if (ZNet.instance.IsServer())
                {
                    Type zdoPeerType = typeof(ZDOMan).Assembly.GetType("ZDOMan+ZDOPeer");
                    var p = zdoPeerType.GetField("m_peer").GetValue(peer) as ZNetPeer;
                    Vector3 refPos = p.GetRefPos();
                    Vector2i zone = ZoneSystem.instance.GetZone(refPos);
                    ___m_tempToSyncDistant.Clear();
                    ZDOMan.instance.FindSectorObjects(zone, ZoneSystem.instance.m_activeArea, ZoneSystem.instance.m_activeDistantArea, toSync, ___m_tempToSyncDistant);
                    foreach (var az in AdoptedZonesManager.GetAdoptedZones(p.m_uid))
                    {
                        Invoke<ZDOMan>(ZDOMan.instance, "FindObjects", zone, toSync);
                    }
                    Invoke<ZDOMan>(ZDOMan.instance, "ServerSortSendZDOS", toSync, refPos, peer);
                    toSync.AddRange(___m_tempToSyncDistant);
                    Invoke<ZDOMan>(ZDOMan.instance, "AddForceSendZdos", peer, toSync);
                    return false;
                }
                return true;
            }
        }

    }
}