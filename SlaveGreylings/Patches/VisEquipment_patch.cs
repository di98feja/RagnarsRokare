using HarmonyLib;
using RagnarsRokare.MobAI;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(VisEquipment), "AttachItem")]
        static class VisEquipment_AttachItem_Patch
        {
            public static bool Prefix(VisEquipment __instance, int itemHash, int variant, Transform joint, ref GameObject __result, ref SkinnedMeshRenderer ___m_bodyModel, bool enableEquipEffects = true)
            {
                if (!MobManager.IsControllableMob(__instance.name)) return true;

                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemHash);
                if (itemPrefab == null)
                {
                    __result = null;
                    return false;
                }
                if (itemPrefab.transform.childCount == 0)
                {
                    __result = null;
                    return false;
                }

                Transform child = null;
                for (int i = 0; i < itemPrefab.transform.childCount; i++)
                {
                    child = itemPrefab.transform.GetChild(i);
                    if (child.gameObject.name == "attach" || child.gameObject.name == "attach_skin")
                    {
                        break;
                    }
                }

                if (null == child)
                {
                    child = itemPrefab.transform.GetChild(0);
                }

                var gameObject = child.gameObject;
                if (gameObject == null)
                {
                    __result = null;
                    return false;
                }
                GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
                gameObject2.SetActive(value: true);
                Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>();
                for (int i = 0; i < componentsInChildren.Length; i++)
                {
                    componentsInChildren[i].enabled = false;
                }
                gameObject2.transform.SetParent(joint);
                gameObject2.transform.localPosition = Vector3.zero;
                gameObject2.transform.localRotation = Quaternion.identity;

                __result = gameObject2;
                return false;
            }
        }
    }
}
