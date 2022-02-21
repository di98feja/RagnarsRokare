using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI.Server
{
    internal static class AdoptedZonesManager
    {
        public static Dictionary<string, ZDOID> AllMobZDOs = new Dictionary<string, ZDOID>();
        private static int UniqueIdHash = Constants.Z_UniqueId.GetStableHashCode();

        internal static void RegisterRPCs()
        {
            ZRoutedRpc.instance.Register<string, ZDOID>(Constants.Z_MobRegistered, RPC_RegisterMob);
            ZRoutedRpc.instance.Register<string, ZDOID>(Constants.Z_MobUnRegistered, RPC_UnRegisterMob);
        }

        private static Dictionary<long, IList<Vector2i>> m_mobZoneToPeerAdoption = new Dictionary<long, IList<Vector2i>>();

        public static IEnumerable<ZDO> GetAllMobZDOs()
        {
            foreach (var mob in AllMobZDOs)
            {
                var mobZdo = ZDOMan.instance.GetZDO(mob.Value);
                if (mobZdo == null)
                {
                    AllMobZDOs.Remove(mob.Key);
                    continue;
                }
                yield return mobZdo;
            }
        }

        public static void RPC_RegisterMob(long sender, string uniqueId, ZDOID zdoId)
        {
            Debug.Log($"RPC_RegisterMob");
            if (AllMobZDOs.ContainsKey(uniqueId)) return;
            AllMobZDOs.Add(uniqueId, zdoId);
            Debug.Log($"Added mob {uniqueId}:{zdoId}");
        }

        public static void RPC_UnRegisterMob(long sender, string uniqueId, ZDOID zdoId)
        {
            if (AllMobZDOs.ContainsKey(uniqueId))
            {
                AllMobZDOs.Remove(uniqueId);
                Debug.Log($"Removed mob {uniqueId}");
            }
        }

        internal static void LoadMobs(ZDOMan zdoMan)
        {
            var allZdos = zdoMan.GetType().GetField("m_objectsByID", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(zdoMan) as Dictionary<ZDOID, ZDO>;
            if (allZdos == null)
            {
                Debug.LogError("MobAILib.Server.MobManager.LoadMobs: Failed to get m_objectsByID");
                return;
            }

            var allMobs = allZdos.Values.Where(z => !string.IsNullOrEmpty(z.GetString(UniqueIdHash)));
            foreach (var mob in allMobs)
            {
                AllMobZDOs.Add(mob.GetString(UniqueIdHash), mob.m_uid);
            }
            Debug.Log($"Loaded {allMobs.Count()} mobs");
        }

        public static IEnumerable<Vector2i> GetAdoptedZones(long peerId)
        {
            if (m_mobZoneToPeerAdoption.ContainsKey(peerId))
            {
                return m_mobZoneToPeerAdoption[peerId];
            }
            else
            {
                return Enumerable.Empty<Vector2i>();
            }
        }

        /// <summary>
        /// Redistribute mob zones among all peers (not server)
        /// Mob zones inside peer active areas is not distributed.
        /// </summary>
        internal static void ResetAdoptedZones()
        {
            m_mobZoneToPeerAdoption.Clear();
            var allPeers = ZNet.instance.GetPeers().Where(p => !p.m_server);
            if (!allPeers.Any()) return;

            var mobZonesToAdopt = CreateSetOfMobZones();
            Debug.Log($"{mobZonesToAdopt.Count} mob zones up for adoption({string.Join("|", mobZonesToAdopt)})");
            foreach (var peer in allPeers)
            {
                Vector2i peerCenterZone = ZoneSystem.instance.GetZone(peer.m_refPos);
                Debug.Log($"Peer {peer.m_uid} is in {peerCenterZone}");
                var commonZones = mobZonesToAdopt.Where(z => ZNetScene.instance.InActiveArea(z, peerCenterZone)).ToArray();

                foreach (var commonZone in commonZones)
                {
                    mobZonesToAdopt.Remove(commonZone);
                    Debug.Log($"{commonZone} already cared for");
                }
            }
            if (mobZonesToAdopt.Count == 0) return;

            int peerIndex = 0;
            foreach (var zone in mobZonesToAdopt)
            {
                var currentPeer = allPeers.ElementAt(peerIndex++);
                if (!m_mobZoneToPeerAdoption.ContainsKey(currentPeer.m_uid))
                {
                    m_mobZoneToPeerAdoption.Add(currentPeer.m_uid, new List<Vector2i>());
                }
                m_mobZoneToPeerAdoption[currentPeer.m_uid].Add(zone);
                Debug.Log($"{currentPeer.m_uid} adopted {zone}");
                if (peerIndex == allPeers.Count())
                {
                    peerIndex = 0;
                }
            }
            foreach (var peer in allPeers)
            {
                Debug.Log($"Sending Peer ({peer.m_uid}) {m_mobZoneToPeerAdoption[peer.m_uid].Count} adopted zones");
                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, Constants.Z_AdoptedZonesEvent, string.Join("|", m_mobZoneToPeerAdoption[peer.m_uid]));
            }
        }

        /// <summary>
        /// This is all zones with an active mob OUTSIDE of any player (peer) active zones
        /// </summary>
        public static IEnumerable<Vector2i> CreateSetOfAllAdoptedZones()
        {
            return m_mobZoneToPeerAdoption.SelectMany(z => z.Value);
        }

        /// <summary>
        /// This is all zones where there is an active mob
        /// </summary>
        public static HashSet<Vector2i> CreateSetOfMobZones()
        {
            var allMobs = GetAllMobZDOs();
            var mobZones =  new HashSet<Vector2i>();
            foreach (var mob in allMobs)
            {
                var zone = ZoneSystem.instance.GetZone(mob.GetPosition());
                for (int x = -1; x <=1; x++)
                {
                    for (int y = -1; y <=1; y++)
                    {
                        var z = new Vector2i(zone.x + x, zone.y + y);
                        if (!mobZones.Contains(z))
                        {
                            mobZones.Add(z);
                        }
                    }
                }
            }
            return mobZones;
        }
    }
}
