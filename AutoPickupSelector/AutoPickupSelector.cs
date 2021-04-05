using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RagnarsRokare_AutoPickupSelector
{
    [BepInPlugin("RagnarsRokare.AutoPickupSelector", "RagnarsRökare AutoPickupSelector Mod", "0.2.0")]
    [BepInProcess("valheim.exe")]
    public class RagnarsRokare : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("RagnarsRokare.AutoPickupSelector");
        public static ConfigEntry<string> AutoPickupBlockList;
        public static ConfigEntry<int> NexusID;

        void Awake()
        {
            AutoPickupBlockList = Config.Bind("General", "AutoPickupBlockList", string.Empty);
            NexusID = Config.Bind<int>("General", "NexusID", 868, "Nexus mod ID for updates");
            harmony.PatchAll();
        }

        internal static IEnumerable<ItemDrop> GetFilteredItemList()
        {
            return ObjectDB.instance.m_items
                .Select(i => i.GetComponent<ItemDrop>())
                .Where(i => i.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material || i.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophie)
                .Where(i => i.m_itemData.m_shared.m_icons.Length > 0);
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        class ObjectDB_Awake_Patch
        {
            static void Postfix()
            {
                if (ObjectDB.instance != null)
                {
                    var blockedItemNames = AutoPickupBlockList.Value.Split(';');
                    foreach (var item in GetFilteredItemList())
                    {
                        item.m_autoPickup = !blockedItemNames.Any(v => v == item.name);
                    }
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
                    .Join(delimiter:";");
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnOpenTrophies))]
        class OnOpenTrophies_Patch
        {
            static void Postfix(ref float ___m_trophieListSpace, ref float ___m_trophieListBaseSize, ref RectTransform ___m_trophieListRoot, ref List<GameObject> ___m_trophyList, ref GameObject ___m_trophieElementPrefab, ref UnityEngine.UI.Scrollbar ___m_trophyListScroll)
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
                    if (text2.EndsWith(" trophy"))
                    {
                        text2 = text2.Remove(text2.Length - 7);
                    }
                    rectTransform.Find("icon_bkg/icon").GetComponent<Image>().sprite = component.m_itemData.GetIcon();
                    rectTransform.Find("name").GetComponent<Text>().text = text2;
                    rectTransform.Find("description").GetComponent<Text>().text = GetLootStateString(component);

                    gameObject.AddComponent<UIInputHandler>();
                    UIInputHandler componentInChildren = gameObject.GetComponent<UIInputHandler>();
                    componentInChildren.m_onLeftDown = (Action<UIInputHandler>)Delegate.Combine(componentInChildren.m_onLeftDown, new Action<UIInputHandler>((handler) =>
                    {
                        component.m_autoPickup = !component.m_autoPickup;
                        rectTransform.Find("description").GetComponent<Text>().text = GetLootStateString(component);
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
