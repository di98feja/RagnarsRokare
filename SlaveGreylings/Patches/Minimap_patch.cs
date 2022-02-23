using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {

        private static List<ZDO> m_allMobZDOs = new List<ZDO>();

        internal static void RegisteredMobsChangedEvent_RPC(long sender, ZPackage pkg)
        {
            Debug.Log("Got RegisteredMobsChangedEvent to Minimap_patch");
            m_allMobZDOs.Clear();
            bool endOfStream = false;
            while (!endOfStream)
            {
                try
                {
                    m_allMobZDOs.Add(ZDOMan.instance.GetZDO(pkg.ReadZDOID()));
                }
                catch (System.IO.EndOfStreamException)
                { 
                    endOfStream = true;
                }
            }
            Debug.Log($"Minimap now track {m_allMobZDOs.Count} mobs");
        }

        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        static class ZoneSystem_Start_Patch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register<ZPackage>(Constants.Z_RegisteredMobsChangedEvent, RegisteredMobsChangedEvent_RPC);
            }
        }

        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        static class Minimap_UpdateDynamicPins_Patch
        {
            private static readonly Dictionary<string, Minimap.PinData> m_mobPins = new Dictionary<string, Minimap.PinData>();
            public static void Postfix()
            {
                try
                {
                    foreach (var zdo in m_allMobZDOs)
                    {
                        var pos = zdo.GetPosition();
                        var name = zdo.GetString(Constants.Z_GivenName);
                        var key = zdo.GetString(Constants.Z_UniqueId);
                        if (!m_mobPins.ContainsKey(key))
                        {
                            var pin = Minimap.instance.AddPin(pos, Minimap.PinType.Icon3, name, false, false);
                            m_mobPins.Add(key, pin);
                        }
                        else
                        {
                            m_mobPins[key].m_pos = pos;
                            m_mobPins[key].m_name = name;
                        }
                    }
                    var idsToRemove = m_mobPins.Where(m => MobManager.AliveMobs.Any(a => (!a.Value.HasInstance()) && (a.Key == m.Key))).Select(m => (m.Key, m.Value)).ToArray();
                    foreach (var pin in idsToRemove)
                    {
                        m_mobPins.Remove(pin.Key);
                        Minimap.instance.RemovePin(pin.Value);
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