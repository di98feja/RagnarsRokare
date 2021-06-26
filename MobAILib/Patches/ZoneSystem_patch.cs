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
				var mob = MobManager.AliveMobs.Values.Where(m => m.HasInstance()).Where(m => m.NView.GetZDO().GetString(Constants.Z_GivenName) == "Einstein").SingleOrDefault();
				if (null == mob) return;

				bool flag = (bool)typeof(ZoneSystem).GetMethod("CreateLocalZones", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { mob.Character.transform.position });
				Debug.Log($"Player zone:{ZoneSystem.instance.GetZone(Player.m_localPlayer.transform.position)}, mobAIZone:{ZoneSystem.instance.GetZone(mob.Character.transform.position)}, isZoneLoaded:{ZoneSystem.instance.IsZoneLoaded(ZoneSystem.instance.GetZone(mob.Character.transform.position))}");
				                //var mobPositions =  MobManager.AliveMobs.Values.Select(m => m.Character.transform.position);
								//foreach (var mob in mobPositions)
								//{
								//	bool flag = (bool)typeof(ZoneSystem).GetMethod("CreateLocalZones", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { ZNet.instance.GetReferencePosition() });
								//}
			}
        }
    }
}