using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

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
                    //Debug.Log($"Poke zone {zone}");
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
                var c = m_tempCurrentObjects.Count;
                //var zdosInSectors = typeof(ZDOMan).GetField("m_objectsBySector", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ZDOMan.instance) as List<ZDO>[];
                foreach (var zone in m_adoptedZones)
                {
                    //int i = (int)Utils.Invoke<ZDOMan>(ZDOMan.instance, "SectorToIndex", zone);
                    //Debug.Log($"zone {zone} is index {i}");
                    //Debug.Log($"ZDOs in sector:{zdosInSectors[i]?.Count ?? -1}");
                    //Debug.Log($"OwnedBy me:{zdosInSectors[i]?.Where(z => z.IsOwner()).Count() ?? -1}");
                    //int numBefore = m_tempCurrentObjects.Count;
                    Utils.Invoke<ZDOMan>(ZDOMan.instance, "FindObjects", zone, m_tempCurrentObjects);
                    //Debug.Log($"Finding objs in {zone}: {m_tempCurrentObjects.Count - numBefore}");
                }

                //Debug.Log($"AdoptedObjects:{m_tempCurrentObjects.Count - c}");
                //m_tempCurrentObjects = m_tempCurrentObjects.Distinct().ToList();
                //Debug.Log($"Distinct AdoptedObjects:{m_tempCurrentObjects.Count - c}");
                Utils.Invoke<ZNetScene>(__instance, "CreateObjects", m_tempCurrentObjects, m_tempCurrentDistantObjects);
                Utils.Invoke<ZNetScene>(__instance, "RemoveObjects", m_tempCurrentObjects, m_tempCurrentDistantObjects);
                return false;
            }
        }
    }
}