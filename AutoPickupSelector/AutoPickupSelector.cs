using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RagnarsRokare_AutoPickupSelector
{
    [BepInPlugin("RagnarsRokare.AutoPickupSelector", "RagnarsRökare AutoPickupSelector Mod", "0.5.0")]
    public class RagnarsRokare : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("RagnarsRokare.AutoPickupSelector");
        public static ConfigEntry<int> NexusID;
        public static List<ConfigEntry<bool>> ItemCategories;
        public static ConfigEntry<string> AutoPickupBlockList;

        void Awake()
        {
            AutoPickupBlockList = Config.Bind("General", "AutoPickupBlockList", string.Empty);
            NexusID = Config.Bind<int>("General", "NexusID", 868, "Nexus mod ID for updates");
            ItemCategories = GetAvailableCategories();
            harmony.PatchAll();
        }

        private List<ConfigEntry<bool>> GetAvailableCategories()
        {
            // https://stackoverflow.com/a/972323
            var values = Enum.GetValues(typeof(ItemDrop.ItemData.ItemType));
            var categories = new List<ConfigEntry<bool>>();

            for (int i = 0; i < values.GetLength(0); i++)
            {
                categories.Add(Config.Bind("Available categories", values.GetValue(i).ToString(), IsCategoryDefault((ItemDrop.ItemData.ItemType)values.GetValue(i)), ""));
            }

            return categories;
        }

        private bool IsCategoryDefault(ItemDrop.ItemData.ItemType category)
        {
            var defaultCategories = new ItemDrop.ItemData.ItemType[] {
                ItemDrop.ItemData.ItemType.Material ,ItemDrop.ItemData.ItemType.Consumable
                ,ItemDrop.ItemData.ItemType.Trophy
                ,ItemDrop.ItemData.ItemType.Misc
                ,ItemDrop.ItemData.ItemType.Utility
                ,ItemDrop.ItemData.ItemType.Fish };

            return defaultCategories.Contains(category);
        }

        private static IEnumerable<ItemDrop.ItemData.ItemType> BuildCategoryList()
        {
            for (int i = 0; i < ItemCategories.Count; i++)
            {
                if (ItemCategories[i].Value && Enum.TryParse<ItemDrop.ItemData.ItemType>(ItemCategories[i].Definition.Key, out var result))
                {
                    yield return result;
                }
            }
        }

        internal static IEnumerable<ItemDrop> GetFilteredItemList()
        {
            var categories = BuildCategoryList();
            var filteredItemList = new List<ItemDrop>();
            // a traditional for loop is x2 faster than linq here
            for (int i = 0; i < ObjectDB.instance.m_items.Count; i++)
            {
                if (!categories.Contains(ObjectDB.instance.m_items[i].GetComponent<ItemDrop>().m_itemData.m_shared.m_itemType))
                    continue;
                if (!IsKnownItem(ObjectDB.instance.m_items[i].GetComponent<ItemDrop>()))
                    continue;
                if (ObjectDB.instance.m_items[i].GetComponent<ItemDrop>().m_itemData.m_shared.m_icons.Length < 1)
                    continue;

                filteredItemList.Add(ObjectDB.instance.m_items[i].GetComponent<ItemDrop>());
            }
            return filteredItemList;
        }

        private static bool IsKnownItem(ItemDrop i)
        {
            if (Player.m_localPlayer == null) return true;
            if ((Traverse.Create(Player.m_localPlayer).Field("m_knownMaterial").GetValue() as HashSet<string>).Contains(i.m_itemData.m_shared.m_name))
            {
                return true;
            }
            if ((Traverse.Create(Player.m_localPlayer).Field("m_trophies").GetValue() as HashSet<string>).Contains(i.m_itemData.m_shared.m_name))
            {
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        class ObjectDB_Awake_Patch
        {
            static void Postfix()
            {
                if (ObjectDB.instance is null)
                    return;

                var blockedItemNames = AutoPickupBlockList.Value.Split(';');
                var items = GetFilteredItemList();
                foreach (var item in items)
                {
                    item.m_autoPickup = !blockedItemNames.Any(v => v == item.name);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCloseTrophies))]
        class OnCloseTrophies_Patch
        {
            static void Postfix()
            {
                AutoPickupBlockList.Value = GetFilteredItemList()
                    .Where(i => i.m_autoPickup == false)
                    .Select(i => i.name)
                    .Join(delimiter: ";");
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnOpenTrophies))]
        class OnOpenTrophies_Patch
        {
            static void Postfix(ref GameObject ___m_trophiesPanel, ref float ___m_trophieListSpace, ref float ___m_trophieListBaseSize, ref RectTransform ___m_trophieListRoot, ref List<GameObject> ___m_trophyList, ref GameObject ___m_trophieElementPrefab, ref UnityEngine.UI.Scrollbar ___m_trophyListScroll)
            {
                //m_trophieListSpace: 180
                //m_trophieListBaseSize: 650
                //m_trophieListRoot: 1260x900

                foreach (GameObject trophy in ___m_trophyList)
                {
                    Destroy(trophy);
                }
                ___m_trophyList.Clear();
                ___m_trophyList.AddRange(CreateItemTiles(___m_trophieElementPrefab, ___m_trophieListRoot, ___m_trophieListSpace, ___m_trophieListBaseSize));
                ___m_trophyListScroll.value = 1f;

                // modify the close button to say "save"
                var trophiesFrame = ___m_trophiesPanel.transform.Find("TrophiesFrame");
                var Closebutton = trophiesFrame.Find("Closebutton");
                Closebutton.GetComponentInChildren<TMP_Text>().text = Localization.instance.Localize("save");
            }

            private static IEnumerable<GameObject> CreateItemTiles(GameObject elementPrefab, RectTransform itemListRoot, float tileWidth, float tileBaseSize)
            {
                var itemTiles = new List<GameObject>();
                if (Player.m_localPlayer == null)
                {
                    return itemTiles;
                }

                float num = 0f;
                int columnCount = 0;
                int rowCount = 0;
                int xMargin = 0;
                int yMargin = -10;

                foreach (var component in GetFilteredItemList().OrderBy(i => i.m_itemData.m_shared.m_itemType))
                {
                    GameObject gameObject = Instantiate(elementPrefab, itemListRoot);

                    gameObject.SetActive(value: true);
                    RectTransform rectTransform = gameObject.transform as RectTransform;
                    rectTransform.anchoredPosition = new Vector2(columnCount == 0 ? xMargin : (float)columnCount * tileWidth, rowCount == 0 ? yMargin : (float)rowCount * (0f - tileWidth));
                    num = Mathf.Min(num, rectTransform.anchoredPosition.y - tileWidth);
                    string text2 = Localization.instance.Localize(component.m_itemData.m_shared.m_name);

                    // this is useless
                    // if (text2.EndsWith(" trophy"))
                    // {
                    //     text2 = text2.Remove(text2.Length - 7);
                    // }

                    rectTransform.Find("icon_bkg/icon").GetComponent<Image>().sprite = component.m_itemData.GetIcon();
                    rectTransform.Find("name").GetComponent<TMP_Text>().text = text2;
                    rectTransform.Find("description").GetComponent<TMP_Text>().text = GetLootStateString(component);

                    gameObject.AddComponent<UIInputHandler>();
                    UIInputHandler componentInChildren = gameObject.GetComponent<UIInputHandler>();
                    componentInChildren.m_onLeftDown = (Action<UIInputHandler>)Delegate.Combine(componentInChildren.m_onLeftDown, new Action<UIInputHandler>((handler) =>
                    {
                        component.m_autoPickup = !component.m_autoPickup;
                        rectTransform.Find("description").GetComponent<TMP_Text>().text = GetLootStateString(component);
                    }));

                    itemTiles.Add(gameObject);
                    //Debug.Log($"Added item:{component.name} at {rectTransform.anchoredPosition.x}, {rectTransform.anchoredPosition.y}");
                    columnCount++;
                    if ((columnCount + 1) * tileWidth > itemListRoot.rect.width)
                    {
                        columnCount = 0;
                        rowCount++;
                    }
                }

                float size = Mathf.Max(tileBaseSize, 0f - num);
                itemListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

                return itemTiles;
            }

            private static string GetLootStateString(ItemDrop component)
            {
                string yesString = Localization.instance.Localize("yes");
                string noString = Localization.instance.Localize("no");
                var state = component.m_autoPickup ? yesString : noString;
                return $"Auto pickup: {state}";
            }
        }
    }
}
