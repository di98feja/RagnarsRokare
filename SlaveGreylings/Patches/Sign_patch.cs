using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(Sign), "Interact")]
        static class Sign_Interact_Patch
        {
            public static void Prefix(Sign __instance, bool hold)
            {
                Debug.LogWarning("Sign interact");
                if (hold)
                {
                    Debug.LogWarning("HOLD!");
                    var closestContainer = Common.FindClosestContainer(__instance.transform.position, 1.0f);
                    var contents = string.Join(",", closestContainer.GetInventory().GetAllItems().Select(i => i.m_shared.m_name));
                    TextInput.instance.m_textField.text = contents;
                }
            }
            //public static Button CreateButton(Button buttonPrefab, Canvas canvas, Vector2 cornerTopRight, Vector2 cornerBottomLeft)
            //{
            //    var button = Object.Instantiate(buttonPrefab, Vector3.zero, Quaternion.identity) as Button;
            //    var rectTransform = button.GetComponent<RectTransform>();
            //    rectTransform.SetParent(canvas.transform);
            //    rectTransform.anchorMax = cornerTopRight;
            //    rectTransform.anchorMin = cornerBottomLeft;
            //    rectTransform.offsetMax = Vector2.zero;
            //    rectTransform.offsetMin = Vector2.zero;
            //    return button;
            //}
        }
    }
}