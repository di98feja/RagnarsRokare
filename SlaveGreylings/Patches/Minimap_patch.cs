using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {

        private static List<ZDOID> m_allMobZDOIDs = new List<ZDOID>();

        private void RegisteredMobsChanged(object sender, RegisteredMobsChangedEventArgs e)
        {
            Debug.Log("Got RegisteredMobsChangedEvent to Minimap_patch");
            m_allMobZDOIDs.Clear();
            m_allMobZDOIDs.AddRange(e.AllMobZDOIDs);
            Debug.Log($"Minimap now track {m_allMobZDOIDs.Count} mobs");
        }

        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        static class Minimap_UpdateDynamicPins_Patch
        {
            private static readonly Dictionary<ZDOID, Minimap.PinData> m_mobPins = new Dictionary<ZDOID, Minimap.PinData>();
            public static void Postfix()
            {
                try
                {
                    foreach (var zid in m_allMobZDOIDs)
                    {
                        var zdo = ZDOMan.instance.GetZDO(zid);
                        if (!(zdo?.IsValid() ?? false)) continue;
                        var pos = zdo.GetPosition();
                        var name = zdo.GetString(Constants.Z_GivenName);

                        if (!m_mobPins.ContainsKey(zid))
                        {
                            var pin = Minimap.instance.AddPin(pos, Minimap.PinType.Icon3, name, false, false);
                            m_mobPins.Add(zid, pin);
                        }
                        else
                        {
                            m_mobPins[zid].m_pos = pos;
                            m_mobPins[zid].m_name = name;
                        }
                    }
                    var idsToRemove = m_mobPins.Where(m => !m_allMobZDOIDs.Any(z => z == m.Key)).Select(m => (m.Key, m.Value)).ToArray();
                    foreach (var pin in idsToRemove)
                    {
                        m_mobPins.Remove(pin.Key);
                        Minimap.instance.RemovePin(pin.Value);
                        Minimap.instance.AddPin(pin.Value.m_pos, Minimap.PinType.Death, pin.Value.m_name, true, false);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e.Message);
                }
            }
        }
    }
}