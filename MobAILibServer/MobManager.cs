using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI.Server
{
    internal static class MobManager
    {
        public static Dictionary<string, ZDO> AllMobZDOs = new Dictionary<string, ZDO>();
        private static int UniqueIdHash = Constants.Z_CharacterId.GetStableHashCode();

        public static void RPC_RegisterMob(long sender, string uniqueId, ZDOID zdoId)
        {
            AllMobZDOs.Add(uniqueId, ZDOMan.instance.GetZDO(zdoId));
            Debug.Log($"Added mob {uniqueId}");
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
                AllMobZDOs.Add(mob.GetString(UniqueIdHash), mob);
            }
            Debug.Log($"Loaded {allMobs.Count()} mobs");
        }

    }
}
