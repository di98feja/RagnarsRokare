using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
            Debug.Log($"Loading {ModName} v{ModVersion}, Barg Bug Bash!");
            harmony.PatchAll();
            NexusID = Config.Bind<int>("General", "NexusID", 982, "Nexus mod ID for updates");
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        class InventoryGui_UpdateRecipe_Patch
        {
            static void Postfix(ref InventoryGui __instance, KeyValuePair<Recipe, ItemDrop.ItemData> ___m_selectedRecipe, ref Button ___m_craftButton)
            {
                if (Player.m_localPlayer == null) return;
                var recipeInput = ___m_selectedRecipe.Value;
                if (recipeInput != null) return;
                var recipe = ___m_selectedRecipe.Key;
                bool isUpgradable = recipe.m_item.m_itemData.m_shared.m_maxQuality > 1;
                if (isUpgradable) return;

                UITooltip component = ___m_craftButton.GetComponent<UITooltip>();
                if (component.m_text != Localization.instance.Localize("$inventory_full")) return;

                bool canAddItem = Player.m_localPlayer.GetInventory().CanAddItem(recipe.m_item.m_itemData, recipe.m_amount);
                if (!canAddItem) return;

                ___m_craftButton.interactable = true;
                component.m_text = string.Empty;
            }
        }

        private static bool m_forceEmptySlot = false;

        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        class InventoryGui_DoCrafting_Patch
        {
            static void Prefix()
            {
                m_forceEmptySlot = true;
            }
        }

        [HarmonyPatch(typeof(Inventory), "HaveEmptySlot")]
        class Inventory_HaveEmptySlot_Patch
        {
            static void Postfix(ref bool __result)
            {
                if (m_forceEmptySlot)
                {
                    // DoCrafting has silly requirement that inventory has an empty slot
                    // Because this was actually checked in the UpdateRecepie method we can simply fake an empty slot here.
                    __result = true;
                    m_forceEmptySlot = false;
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), "AddItem", argumentTypes: new Type[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string) })]
        class Inventory_AddItem_Patch
        {
            static bool Prefix(ref Inventory __instance, ref ItemDrop.ItemData __result, string name, int stack, int quality, int variant, long crafterID, string crafterName)
            {
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(name);
                if (itemPrefab == null) return true;

                bool canAddItem = __instance.CanAddItem(itemPrefab, stack);
                if (canAddItem)
                {
                    __result = MoveItems(ref __instance, stack, quality, variant, crafterID, crafterName, itemPrefab);
                    return false;
                }
                return true;
            }

            private static ItemDrop.ItemData MoveItems(ref Inventory instance, int stack, int quality, int variant, long crafterID, string crafterName, GameObject itemPrefab)
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
                    Traverse.Create(instance).Method("AddItem", new Type[] { typeof(ItemDrop.ItemData) }).GetValue(component2.m_itemData);
                    result = component2.m_itemData;
                    UnityEngine.Object.Destroy(gameObject);
                }
                return result;
            }
        }

        [HarmonyPatch(typeof(Inventory), "RemoveItem", argumentTypes: new Type[] { typeof(string), typeof(int) })]
        class Inventory_RemoveItem_Patch
        {
            static bool Prefix(ref Inventory __instance, ref List<ItemDrop.ItemData> ___m_inventory, string name, ref int amount)
            {
                var sortedInventoryList = ___m_inventory.OrderBy(i => i.m_stack);
                foreach (ItemDrop.ItemData item in sortedInventoryList)
                {
                    if (item.m_shared.m_name == name)
                    {
                        int num = Mathf.Min(item.m_stack, amount);
                        item.m_stack -= num;
                        amount -= num;
                        if (amount <= 0)
                        {
                            break;
                        }
                    }
                }
                return true;
            }
        }
    }
}