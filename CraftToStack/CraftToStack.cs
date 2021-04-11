using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.CraftToStack
{
	[BepInPlugin(ModId, ModName, ModVersion)]
	[BepInProcess("valheim.exe")]
	public class CraftToStack : BaseUnityPlugin
	{
		public const string ModId = "RagnarsRokare.CraftToStack";
		public const string ModName = "RagnarsRökare CraftToStackMod";
		public const string ModVersion = "0.1";

		private readonly Harmony harmony = new Harmony(ModId);
		public static ConfigEntry<int> NexusID;

		void Awake()
		{
			Debug.Log($"Loading {ModName} v{ModVersion}, lets get rolling!");
			harmony.PatchAll();
			NexusID = Config.Bind<int>("General", "NexusID", -1, "Nexus mod ID for updates");
		}

		[HarmonyPatch(typeof(Inventory), "AddItem")]
		class Inventory_AddItem_Patch
		{
			static bool Prefix(ref Inventory __instance, ref ItemDrop.ItemData __result, string name, int stack, int quality, int variant, long crafterID, string crafterName)
            {
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(name);
                if (itemPrefab == null)
                {
                    return true;
                }
                ItemDrop component = itemPrefab.GetComponent<ItemDrop>();
                if (component == null)
                {
                    return true;
                }
                bool fillTopFirst = (bool)Traverse.Create(__instance).Method("TopFirst").GetValue(component.m_itemData);
                bool hasEmptySlot = ((Vector2i)Traverse.Create(__instance).Method("FindEmptySlot").GetValue(fillTopFirst)).x == -1;
                if (component.m_itemData.m_shared.m_maxStackSize > 1)
                {
                    var existingStack = Traverse.Create(__instance).Method("FindFreeStackItem").GetValue(name, quality) as ItemDrop.ItemData;
                    if (existingStack == null) return true;
                    int roomLeftInStack = existingStack.m_shared.m_maxStackSize - existingStack.m_stack;
                    bool allFitInStack = roomLeftInStack >= stack;
                    if (allFitInStack || hasEmptySlot)
                    {
                        __result = MoveItems(ref __instance, name, stack, quality, variant, crafterID, crafterName, itemPrefab);
                        return false;
                    }
                }
                return true;
            }

            private static ItemDrop.ItemData MoveItems(ref Inventory instance, string name, int stack, int quality, int variant, long crafterID, string crafterName, GameObject itemPrefab)
            {
                ItemDrop.ItemData result = null;
                int num = stack;
                while (num > 0)
                {
                    ZNetView.m_forceDisableInit = true;
                    GameObject gameObject = UnityEngine.Object.Instantiate(itemPrefab);
                    ZNetView.m_forceDisableInit = false;
                    ItemDrop component2 = gameObject.GetComponent<ItemDrop>();
                    if (component2 == null)
                    {
                        UnityEngine.Object.Destroy(gameObject);
                        return null;
                    }
                    int num2 = Mathf.Min(num, component2.m_itemData.m_shared.m_maxStackSize);
                    num -= num2;
                    component2.m_itemData.m_stack = num2;
                    component2.m_itemData.m_quality = quality;
                    component2.m_itemData.m_variant = variant;
                    component2.m_itemData.m_durability = component2.m_itemData.GetMaxDurability();
                    component2.m_itemData.m_crafterID = crafterID;
                    component2.m_itemData.m_crafterName = crafterName;
                    Traverse.Create(instance).Method("AddItem").GetValue(component2.m_itemData);
                    result = component2.m_itemData;
                    UnityEngine.Object.Destroy(gameObject);
                }
                return result;
            }
        }
	}
}