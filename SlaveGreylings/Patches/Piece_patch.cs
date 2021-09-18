using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        //[HarmonyPatch(typeof(Piece), "GetSnapPoints", typeof(List<Transform>))]
        //static class Piece_GetSnapPoints_Patch
        //{
        //    public static void Postfix(Piece __instance, List<Transform> points)
        //    {
        //        var snapPoint = __instance.gameObject.GetComponentsInChildren<Transform>().Where(c => c.name == "spine2").SingleOrDefault();
        //        if (snapPoint != null)
        //        {
        //            Debug.Log("Found snap point spine2!");
        //            points.Add(snapPoint);
        //        }
        //    }
        //}
    }
}