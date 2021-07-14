using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using static Minimap;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Minimap), "UpdateDynamicPins")]
        static class Minimap_UpdateDynamicPins_Patch
        {
            private static Dictionary<string, PinData> m_mobPins = new Dictionary<string, PinData>();
            public static void Postfix()
            {
                foreach (var mob in MobManager.AliveMobs.Where(m => m.Value.HasInstance()))
                {
                    var pos = mob.Value.Character.transform.position;
                    var name = mob.Value.NView.GetZDO().GetString(Constants.Z_GivenName);
                    if (!m_mobPins.ContainsKey(mob.Key))
                    {
                        var pin = Minimap.instance.AddPin(pos, PinType.Icon3, name, false, false);
                        m_mobPins.Add(mob.Key, pin);
                    }
                    else
                    {
                        m_mobPins[mob.Key].m_pos = pos;
                        m_mobPins[mob.Key].m_name = name;
                    }
                }
                var pinsToRemove = m_mobPins.Where(m => !MobManager.AliveMobs.ContainsKey(m.Key));
                foreach (var pin in pinsToRemove)
                {
                    Minimap.instance.RemovePin(pin.Value);
                    m_mobPins.Remove(pin.Key);
                }
            }
        }
    }
}