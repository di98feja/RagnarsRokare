using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.MobAI.Server
{
    public partial class MobAILibServer
    {
        /// <summary>
        /// Create and Destroy objects in mob zones
        /// Only include center zone and skip distant objects
        /// </summary>
        //[HarmonyPatch(typeof(ZNetScene), "CreateDestroyObjects")]
        //public class CreateDestroyObjects_Patch
        //{
        //    private static bool Prefix(ZNetScene __instance)
        //    {
        //        List<ZDO> m_tempCurrentObjects = new List<ZDO>();
        //        List<ZDO> m_tempCurrentDistantObjects = new List<ZDO>();
        //        foreach (var zone in MobManager.CreateSetOfAllAdoptedZones())
        //        {
        //            Traverse.Create(ZDOMan.instance).Method("FindObjects", zone, m_tempCurrentObjects);
        //        }

        //        m_tempCurrentDistantObjects = m_tempCurrentDistantObjects.Distinct().ToList();
        //        m_tempCurrentObjects = m_tempCurrentObjects.Distinct().ToList();
        //        Traverse.Create(__instance).Method("CreateObjects", m_tempCurrentObjects, m_tempCurrentDistantObjects).GetValue();
        //        Traverse.Create(__instance).Method("RemoveObjects", m_tempCurrentObjects, m_tempCurrentDistantObjects).GetValue();
        //        return false;
        //    }
        //}


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
					MobManager.ResetAdoptedZones();
                }

				Vector2i zone = ZoneSystem.instance.GetZone(refPosition);
				var adoptedZones = MobManager.GetAdoptedZones(uid);
				Debug.Log($"{uid} have adopted {adoptedZones.Count()} zones");
				List<ZDO> m_tempNearObjects = Traverse.Create(__instance).Field("m_tempNearObjects").GetValue<List<ZDO>>();

				m_tempNearObjects.Clear();
				__instance.FindSectorObjects(zone, ZoneSystem.instance.m_activeArea, 0, m_tempNearObjects, null);
				foreach (var adoptedZone in adoptedZones)
                {
					__instance.FindSectorObjects(adoptedZone, ZoneSystem.instance.m_activeArea, 0, m_tempNearObjects, null);
				}
				foreach (ZDO zdo in m_tempNearObjects)
				{
					if (!zdo.m_persistent) continue;

					var zdoSector = zdo.GetSector();

					if (zdo.m_owner == uid)
					{
						if (!ZNetScene.instance.InActiveArea(zdoSector, zone) && !adoptedZones.Any(s => s == zdoSector))
						{
							zdo.SetOwner(0L);
						}
					}
					else if (zdo.m_owner == 0L || !new Traverse(__instance).Method("IsInPeerActiveArea", new object[] { zdoSector, zdo.m_owner }).GetValue<bool>())
					{
						if (ZNetScene.instance.InActiveArea(zdoSector, zone) || adoptedZones.Any(s => s == zdoSector))
                        {
							zdo.SetOwner(uid);
						}
					}
				}
				return false;
			}
		}
	}
}