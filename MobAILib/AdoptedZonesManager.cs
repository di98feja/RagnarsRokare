using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI.ServerPeer
{
    internal static class AdoptedZonesManager
    {
        public static Dictionary<string, ZDOID> AllMobZDOs = new Dictionary<string, ZDOID>();
        private static int IsControlledByMobAILibHash = Constants.Z_IsControlledByMobAILib.GetStableHashCode();
        private static int CharacterIdHash = Constants.Z_CharacterId.GetStableHashCode();
        private static int UniqueIdHash = Constants.Z_UniqueId.GetStableHashCode();

        internal static void RegisterRPCs()
        {
            ZRoutedRpc.instance.Register<string, ZDOID>(Constants.Z_MobRegistered, RPC_RegisterMob);
            ZRoutedRpc.instance.Register<string, ZDOID>(Constants.Z_MobUnRegistered, RPC_UnRegisterMob);
        }

        private static Dictionary<long, AdoptedZones> m_mobZoneToPeerAdoption = new Dictionary<long, AdoptedZones>();

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

            var allMobs = allZdos.Values.Where(z => z.GetBool(IsControlledByMobAILibHash));
            foreach (var mob in allMobs)
            {
                string uniqueId = mob.GetString(UniqueIdHash);
                string characterId = mob.GetString(CharacterIdHash);
                string mobId = string.IsNullOrEmpty(uniqueId) ? characterId : uniqueId;
                Debug.Log($"MobId {mobId}, ZDOid:{mob.m_uid}");
                if (AllMobZDOs.ContainsKey(mobId))
                {
                    Debug.Log($"Duplicate Mob Id:{mobId}:{mob.m_uid}. Already registered on {mob.m_uid}");
                }
                else
                {
                    AllMobZDOs.Add(mobId, mob.m_uid);
                }
                Debug.Log($"{mob.GetString(Constants.Z_GivenName)} loaded");
            }
            Debug.Log($"Loaded {allMobs.Count()} mobs");
        }

        public static AdoptedZones GetAdoptedZones(long peerId)
        {
            if (m_mobZoneToPeerAdoption.ContainsKey(peerId))
            {
                return m_mobZoneToPeerAdoption[peerId];
            }
            else
            {
                return new AdoptedZones();
            }
        }

        /// <summary>
        /// Redistribute mob zones among all peers
        /// Mob zones inside peer active areas is not distributed.
        /// This method is only called by the peer acting as server
        /// </summary>
        internal static void ResetAdoptedZones()
        {
            foreach (var az in m_mobZoneToPeerAdoption.Values)
            {
                az.Reset();
            }
            var allPeers = ZNet.instance.GetPeers();
            if (!allPeers.Any() && ZNet.instance.IsDedicated()) return;

            var mobZonesToAdopt = CreateSetOfMobZones();
            //Debug.Log($"{mobZonesToAdopt.Count} mob zones up for adoption({string.Join("|", mobZonesToAdopt)})");
            var reverseMap = BuildReverseMapping();

            RemoveActiveZones(mobZonesToAdopt, ZNet.instance.GetReferencePosition(), reverseMap);
            foreach (var peer in allPeers)
            {
                RemoveActiveZones(mobZonesToAdopt, peer.m_refPos, reverseMap);
            }
            RemoveDeadZones(mobZonesToAdopt, reverseMap);
            RemoveAlreadyAdoptedZones(ref mobZonesToAdopt, reverseMap);

            var peerIds = allPeers.Select(p => p.m_uid).ToList();
            if (!ZNet.instance.IsDedicated())
            {
                peerIds.Add(ZDOMan.instance.GetMyID());
            }

            var newPeers = peerIds.Where(p => !m_mobZoneToPeerAdoption.ContainsKey(p));
            foreach (var peer in newPeers)
            {
                m_mobZoneToPeerAdoption.Add(peer, new AdoptedZones());
            }

            foreach (var zone in mobZonesToAdopt)
            {
                var peerWithLeastAdoptedZones = m_mobZoneToPeerAdoption.OrderBy(z => z.Value.CurrentZones.Count).First().Key;
                m_mobZoneToPeerAdoption[peerWithLeastAdoptedZones].AddZone(zone);
            }

            foreach (var peer in peerIds)
            {
                Debug.Log($"Sending Peer ({ZNet.instance.GetPeer(peer)?.m_playerName ?? "Myself"}) {m_mobZoneToPeerAdoption[peer].CurrentZones.Count} adopted zones");
                ZRoutedRpc.instance.InvokeRoutedRPC(peer, Constants.Z_AdoptedZonesEvent, string.Join("|", m_mobZoneToPeerAdoption[peer].CurrentZones));
            }
        }

        private static void RemoveDeadZones(HashSet<Vector2i> mobZonesToAdopt, Dictionary<Vector2i, long> reverseMap)
        {
            foreach (var zone in reverseMap.Keys)
            {
                if (!mobZonesToAdopt.Contains(zone))
                {
                    m_mobZoneToPeerAdoption[reverseMap[zone]].RemoveZone(zone);
                }
            }
        }

        private static void RemoveAlreadyAdoptedZones(ref HashSet<Vector2i> mobZonesToAdopt, Dictionary<Vector2i, long> reverseMap)
        {
            foreach (var zone in reverseMap.Keys)
            {
                if (mobZonesToAdopt.Contains(zone))
                {
                    mobZonesToAdopt.Remove(zone);
                }
            }
        }

        private static void RemoveActiveZones(HashSet<Vector2i> mobZonesToAdopt, Vector3 refPos, Dictionary<Vector2i,long> reverseMap)
        {
            Vector2i peerCenterZone = ZoneSystem.instance.GetZone(refPos);
            foreach (Vector2i zone in GetActiveArea(peerCenterZone))
            {
                if (reverseMap.ContainsKey(zone))
                {
                    m_mobZoneToPeerAdoption[reverseMap[zone]].RemoveZone(zone);
                }
                if (mobZonesToAdopt.Contains(zone))
                {
                    mobZonesToAdopt.Remove(zone);
                }
            }
        }

        private static IEnumerable<Vector2i> GetActiveArea(Vector2i peerCenterZone)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    yield return new Vector2i(peerCenterZone.x + x, peerCenterZone.y + y);
                }
            }
        }

        /// <summary>
        /// This is all zones with an active mob OUTSIDE of any player (peer) active zones
        /// </summary>
        public static IEnumerable<Vector2i> CreateSetOfAllAdoptedZones()
        {
            return m_mobZoneToPeerAdoption.SelectMany(z => z.Value.CurrentZones);
        }

        /// <summary>
        /// This is all zones where there is an active mob
        /// </summary>
        public static HashSet<Vector2i> CreateSetOfMobZones()
        {
            var allMobs = GetAllMobZDOs();
            var mobZones = new HashSet<Vector2i>();
            foreach (var mob in allMobs)
            {
                var zone = ZoneSystem.instance.GetZone(mob.GetPosition());
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
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

        private static Dictionary<Vector2i, long> BuildReverseMapping()
        {
            return m_mobZoneToPeerAdoption.SelectMany(kvp => kvp.Value.CurrentZones
            .Select(z => new KeyValuePair<Vector2i, long>(z, kvp.Key))).ToDictionary(x => x.Key, x => x.Value);
        }

        public class AdoptedZones
        {
            public List<Vector2i> CurrentZones { get; set; } = new List<Vector2i>();
            public List<Vector2i> AddedZones { get; set; } = new List<Vector2i>();
            public List<Vector2i> RemovedZones { get; set; } = new List<Vector2i>();

            public void Reset()
            {
                AddedZones.Clear();
                RemovedZones.Clear();
            }

            public bool HasZone(Vector2i z)
            {
                return CurrentZones.Contains(z);
            }

            public void AddZone(Vector2i z)
            {
                if (HasZone(z)) return;
                CurrentZones.Add(z);
                AddedZones.Add(z);
            }

            public void RemoveZone(Vector2i z)
            {
                if (!HasZone(z)) return;
                CurrentZones.Remove(z);
                RemovedZones.Add(z);
            }
        }
    }
}
