using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI.ServerPeer
{
    internal static class AdoptedZonesManager
    {
        public static Dictionary<string, ZDOID> AllMobZDOs = new Dictionary<string, ZDOID>();
        private static int UniqueIdHash = Constants.Z_CharacterId.GetStableHashCode();

        internal static void RegisterRPCs()
        {
            ZRoutedRpc.instance.Register<string, ZDOID>(Constants.Z_MobRegistered, RPC_RegisterMob);
            ZRoutedRpc.instance.Register<string, ZDOID>(Constants.Z_MobUnRegistered, RPC_UnRegisterMob);
        }

        private static Dictionary<long, IList<Vector2i>> m_mobZoneToPeerAdoption = new Dictionary<long, IList<Vector2i>>();

        public static IEnumerable<ZDO> GetAllMobZDOs()
        {
            List<string> deadZDOs = new List<string>();
            List<ZDO> aliveZDOs = new List<ZDO>();
            foreach (var mob in AllMobZDOs)
            {
                var mobZdo = ZDOMan.instance.GetZDO(mob.Value);
                if (mobZdo == null || !mobZdo.IsValid())
                {

                    deadZDOs.Add(mob.Key);
                    Debug.Log("Removed one dead Mob");
                    continue;
                }
                aliveZDOs.Add(mobZdo);
            }
            foreach (var mob in deadZDOs)
            {
                AllMobZDOs.Remove(mob);
            }
            return aliveZDOs;
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
                Debug.Log($"{mob.GetString(Constants.Z_GivenName)} loaded");
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
        /// This implementation differs from the dedicated server
        /// This method is only called by the peer acting as server
        /// </summary>
        internal static void ResetAdoptedZones()
        {
            m_mobZoneToPeerAdoption.Clear();
            var allPeers = ZNet.instance.GetPeers();
            var mobZonesToAdopt = CreateSetOfMobZones();
            //Debug.Log($"{mobZonesToAdopt.Count} mob zones up for adoption({string.Join("|", mobZonesToAdopt)})");

            RemoveActiveZones(mobZonesToAdopt, ZNet.instance.GetReferencePosition());
            foreach (var peer in allPeers)
            {
                RemoveActiveZones(mobZonesToAdopt, peer.m_refPos);
            }

            if (mobZonesToAdopt.Count == 0) return;

            int peerIndex = 0;
            var peerIds = allPeers.Select(p => p.m_uid).ToList();
            peerIds.Add(ZDOMan.instance.GetMyID());

            foreach (var zone in mobZonesToAdopt)
            {
                var currentPeer = peerIds.ElementAt(peerIndex++);
                if (!m_mobZoneToPeerAdoption.ContainsKey(currentPeer))
                {
                    m_mobZoneToPeerAdoption.Add(currentPeer, new List<Vector2i>());
                }
                m_mobZoneToPeerAdoption[currentPeer].Add(zone);
                if (peerIndex >= allPeers.Count())
                {
                    peerIndex = 0;
                }
            }
            foreach (var peer in peerIds)
            {
                Debug.Log($"Sending Peer ({ZNet.instance.GetPeer(peer)?.m_playerName ?? "Myself"}) {m_mobZoneToPeerAdoption[peer].Count} adopted zones");
                ZRoutedRpc.instance.InvokeRoutedRPC(peer, Constants.Z_AdoptedZonesEvent, string.Join("|", m_mobZoneToPeerAdoption[peer]));
            }
        }

        private static void RemoveActiveZones(HashSet<Vector2i> mobZonesToAdopt, Vector3 refPos)
        {
            Vector2i peerCenterZone = ZoneSystem.instance.GetZone(refPos);
            var commonZones = mobZonesToAdopt.Where(z => ZNetScene.instance.InActiveArea(z, peerCenterZone)).ToArray();

            foreach (var commonZone in commonZones)
            {
                mobZonesToAdopt.Remove(commonZone);
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
