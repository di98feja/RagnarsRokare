using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {

        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        static class Minimap_UpdateDynamicPins_Patch
        {
            private static readonly Dictionary<string, Minimap.PinData> m_mobPins = new Dictionary<string, Minimap.PinData>();
            public static void Postfix()
            {
                try
                {
                    foreach (var mob in MobManager.AliveMobs.Where(m => m.Value.HasInstance()))
                    {
                        if (!mob.Value.NView?.IsValid() ?? false) continue;

                        var pos = mob.Value.Character.transform.position;
                        var name = mob.Value.NView.GetZDO().GetString(Constants.Z_GivenName);
                        if (!m_mobPins.ContainsKey(mob.Key))
                        {
                            var pin = Minimap.instance.AddPin(pos, Minimap.PinType.Icon3, name, false, false);
                            m_mobPins.Add(mob.Key, pin);
                        }
                        else
                        {
                            m_mobPins[mob.Key].m_pos = pos;
                            m_mobPins[mob.Key].m_name = name;
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