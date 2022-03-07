using HarmonyLib;
using RagnarsRokare.MobAI.Server;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public partial class MobAILib
    {
        internal static List<Vector2i> m_adoptedZones = new List<Vector2i>();

        internal static void AdoptedZoneEvent_RPC(long sender, string zones)
        {
            m_adoptedZones.Clear();
            if (string.IsNullOrEmpty(zones)) return;
            
            foreach (string z in zones.Split('|'))
            {
                int x = int.Parse(z.Split(',').First());
                int y = int.Parse(z.Split(',').Last());
                var zone = new Vector2i(x, y);
                m_adoptedZones.Add(zone);
            }
            //Debug.Log($"Adopted zones:{string.Join("|", m_adoptedZones)}");
        }

        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        static class ZoneSystem_Start_Patch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register<string>(Constants.Z_AdoptedZonesEvent, AdoptedZoneEvent_RPC);
            }
        }

        /// <summary>
        /// Incude adopted zones when keeping local zones alive
        /// </summary>
        [HarmonyPatch(typeof(ZoneSystem), "CreateLocalZones")]
        static class ZoneSystem_CreateLocalZones_Patch
        {
            static void Postfix(ZoneSystem __instance, ref float ___m_updateTimer)
            {
                if (___m_updateTimer > 0f) return;
                foreach (var zone in m_adoptedZones)
                {
                    Utils.Invoke<ZoneSystem>(__instance, "PokeLocalZone", zone);
                }
            }
        }

        /// <summary>
        /// Include adopted zones when creating and destroying local game objects
        /// </summary>
        [HarmonyPatch(typeof(ZNetScene), "CreateDestroyObjects")]
        public class CreateDestroyObjects_Patch
        {
            private static bool Prefix(ZNetScene __instance)
            {
                List<ZDO> m_tempCurrentObjects = new List<ZDO>();
                List<ZDO> m_tempCurrentDistantObjects = new List<ZDO>();
                Vector2i playerZone = ZoneSystem.instance.GetZone(ZNet.instance.GetReferencePosition());
                ZDOMan.instance.FindSectorObjects(playerZone, ZoneSystem.instance.m_activeArea, ZoneSystem.instance.m_activeDistantArea, m_tempCurrentObjects, m_tempCurrentDistantObjects);
                //Debug.Log($"Playerzone objs:{m_tempCurrentObjects.Count}");
                foreach (var zone in m_adoptedZones)
                {
                    Utils.Invoke<ZDOMan>(ZDOMan.instance, "FindObjects", zone, m_tempCurrentObjects);
                }

                m_tempCurrentObjects = m_tempCurrentObjects.Distinct().ToList();
                //Debug.Log($"m_tempCurrentObjects:{m_tempCurrentObjects.Count}");
                Utils.Invoke<ZNetScene>(__instance, "CreateObjects", m_tempCurrentObjects, m_tempCurrentDistantObjects);
                Utils.Invoke<ZNetScene>(__instance, "RemoveObjects", m_tempCurrentObjects, m_tempCurrentDistantObjects);
                return false;
            }
        }
    }
}