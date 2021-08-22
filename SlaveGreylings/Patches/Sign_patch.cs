using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
        static class Sign_GetHoverText_Patch
        {
            static void Postfix(Sign __instance, ref string __result)
            {
                if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: false))
                {
                    return;
                }

                if (!Enum.TryParse(CommonConfig.UpdateSignFromContainerKey.Value, out KeyCode key))
                {
                    key = KeyCode.Insert;
                }

                __result +=  $"\n[<color=yellow><b>{key}</b></color>] Update from container";
            }
        }

        internal static void UpdateSignFromContainer()
        {
            var hoveringCollider = typeof(Player).GetField("m_hovering", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer) as GameObject;
            if (!(bool)hoveringCollider) return;

            var sign = hoveringCollider.GetComponentInParent<Sign>();
            if (!(bool)sign) return;

            var container = Common.FindClosestContainer(sign.transform.position, 1.5f);
            if (!(bool)container) return;

            var inventory = string.Join(",", container.GetInventory().GetAllItems().Select(i => i.m_shared.m_name).Distinct());
            string translatedList = Localization.instance.Localize(inventory);
            sign.SetText(translatedList.Substring(0, Math.Min(translatedList.Length, 50)));
        }
    }
}