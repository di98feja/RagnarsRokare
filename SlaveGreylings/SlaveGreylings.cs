using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SlaveGreylings
{
    [BepInPlugin("RagnarsRokare.SlaveGreylings", "SlaveGreylings", "0.1")]
    public class SlaveGreylings : BaseUnityPlugin
    {
        private static readonly bool isDebug = true;

        public static ConfigEntry<float> dropRange;
        public static ConfigEntry<float> containerRange;
        public static ConfigEntry<string> fuelDisallowTypes;
        public static ConfigEntry<string> oreDisallowTypes;
        public static ConfigEntry<string> toggleKey;
        public static ConfigEntry<string> toggleString;
        public static ConfigEntry<bool> refuelStandingTorches;
        public static ConfigEntry<bool> refuelWallTorches;
        public static ConfigEntry<bool> refuelFirePits;
        public static ConfigEntry<bool> isOn;
        public static ConfigEntry<bool> modEnabled;

        private static SlaveGreylings context;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(SlaveGreylings).Namespace + " " : "") + str);
        }
        private void Awake()
        {
            context = this;
            dropRange = Config.Bind<float>("General", "DropRange", 5f, "The maximum range to pull dropped fuel");
            containerRange = Config.Bind<float>("General", "ContainerRange", 5f, "The maximum range to pull fuel from containers");
            fuelDisallowTypes = Config.Bind<string>("General", "FuelDisallowTypes", "RoundLog,FineWood", "Types of item to disallow as fuel (i.e. anything that is consumed), comma-separated.");
            oreDisallowTypes = Config.Bind<string>("General", "OreDisallowTypes", "RoundLog,FineWood", "Types of item to disallow as ore (i.e. anything that is transformed), comma-separated).");
            toggleString = Config.Bind<string>("General", "ToggleString", "Auto Fuel: {0}", "Text to show on toggle. {0} is replaced with true/false");
            toggleKey = Config.Bind<string>("General", "ToggleKey", "", "Key to toggle behaviour. Leave blank to disable the toggle key. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html");
            refuelStandingTorches = Config.Bind<bool>("General", "RefuelStandingTorches", true, "Refuel standing torches");
            refuelWallTorches = Config.Bind<bool>("General", "RefuelWallTorches", true, "Refuel wall torches");
            refuelFirePits = Config.Bind<bool>("General", "RefuelFirePits", true, "Refuel fire pits");
            isOn = Config.Bind<bool>("General", "IsOn", true, "Behaviour is currently on or not");
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");

            if (!modEnabled.Value)
                return;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private static string GetPrefabName(string name)
        {
            char[] anyOf = new char[] { '(', ' ' };
            int num = name.IndexOfAny(anyOf);
            string result;
            if (num >= 0)
                result = name.Substring(0, num);
            else
                result = name;
            return result;
        }

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        static class MonsterAI_UpdateAI_Patch
        {
            static MonsterAI_UpdateAI_Patch()
            {
                m_assignment = new Dictionary<int, MaxStack<Smelter>>();
                m_assigned = new Dictionary<int, bool>();
                m_container1 = new Dictionary<int, Container>();
                m_container2 = new Dictionary<int, Container>();
                m_container3 = new Dictionary<int, Container>();
                m_container4 = new Dictionary<int, Container>();
                m_container5 = new Dictionary<int, Container>();
                m_fetchitems = new Dictionary<int, List<ItemDrop>>();
            }
            public static Dictionary<int, MaxStack<Smelter>> m_assignment;

            public static Dictionary<int, bool>      m_assigned;
            public static Dictionary<int, Container> m_container1;
            public static Dictionary<int, Container> m_container2;
            public static Dictionary<int, Container> m_container3;
            public static Dictionary<int, Container> m_container4;
            public static Dictionary<int, Container> m_container5;
            public static Dictionary<int, List<ItemDrop>> m_fetchitems;

            private static Character m_attacker = null;

            static bool Prefix(MonsterAI __instance, float dt, ref ZNetView ___m_nview, ref Character ___m_character, ref float ___m_fleeIfLowHealth,
                ref float ___m_timeSinceHurt, ref string ___m_aiStatus)
            {
                if (!___m_nview.IsOwner())
                {
                    return false;
                }
                Dbgl("Owned ok");
                if (!___m_character.IsTamed())
                {
                    return true;
                }
                Dbgl("Tamed ok");
                if (__instance.IsSleeping())
                {
                    var UpdateSleep_method = __instance.GetType().GetMethod("UpdateSleep", BindingFlags.NonPublic | BindingFlags.Instance);
                    UpdateSleep_method.Invoke(__instance, new object[] { dt });
                    return false;
                }
                Dbgl("Sleep ok");
                if (!m_assignment.ContainsKey(__instance.GetInstanceID()))
                {
                    m_assignment.Add(__instance.GetInstanceID(), new MaxStack<Smelter>(4));
                    m_assigned.Add(__instance.GetInstanceID(), false);
                }
                Dbgl("GetInstanceID ok");
                ___m_aiStatus = "";
                Humanoid humanoid = ___m_character as Humanoid;
                //typeof(MonsterAI).GetMethod("UpdateTarget", BindingFlags.NonPublic |BindingFlags.Instance).Invoke(__instance, new object[] { humanoid, dt });
                if (___m_character.GetHealthPercentage() < ___m_fleeIfLowHealth && ___m_timeSinceHurt < 20f && m_attacker != null)
                {
                    typeof(MonsterAI).GetMethod("Flee", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { dt, m_attacker.transform.position });
                    ___m_aiStatus = "Low health, flee";
                    return false;
                }
                Dbgl("Flee ok");
                //if (m_assignment != null && AvoidFire(dt, m_assignment.transform.position))
                //{
                //    m_aiStatus = "Avoiding fire";
                //    return;
                //}
                //Dbgl("Fire ok");
                var UpdateConsumeItem_method = typeof(MonsterAI).GetMethod("UpdateConsumeItem", BindingFlags.NonPublic | BindingFlags.Instance);
                if (!__instance.IsAlerted() && (bool)UpdateConsumeItem_method.Invoke(__instance, new object[] { humanoid, dt }))
                {
                    ___m_aiStatus = "Consume item";
                    return false;
                }
                Dbgl("Consume ok");
                if ((bool)__instance.GetFollowTarget())
                {
                    typeof(MonsterAI).GetMethod("Follow", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { __instance.GetFollowTarget(), dt });
                    ___m_aiStatus = "Follow";
                    return false;
                }
                Dbgl("Follow ok");
                if (!m_assigned[__instance.GetInstanceID()])
                {
                    foreach (Collider collider in Physics.OverlapSphere(___m_character.transform.position, 50, LayerMask.GetMask(new string[] { "piece" })))
                    {
                        Smelter smelter = collider.transform.parent?.gameObject?.GetComponent<Smelter>();
                        //Debug.Log($"{smelter}");
                        if (smelter?.GetComponent<ZNetView>()?.IsValid() != true)
                            continue;
                        if (smelter?.transform?.position != null && !m_assignment[__instance.GetInstanceID()].Contains(smelter))
                        {
                            m_assignment[__instance.GetInstanceID()].Push(smelter);
                            m_assigned[__instance.GetInstanceID()] = true;
                            ___m_aiStatus = string.Concat("Doing assignment");

                            return false;
                        }

                    }
                    m_assignment[__instance.GetInstanceID()].Clear();
                }
                Dbgl("Unassigned ok");
                if (m_assigned[__instance.GetInstanceID()])
                {
                    typeof(MonsterAI).GetMethod("MoveTo", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { dt, m_assignment[__instance.GetInstanceID()].Peek().transform.position, 0, false });
                    if (Vector3.Distance(___m_character.transform.position, m_assignment[__instance.GetInstanceID()].Pop().transform.position) < 4)
                    {
                        m_assigned[__instance.GetInstanceID()] = false;
                    }
                    return false;
                }
                Dbgl("Assigned ok");

                ___m_aiStatus = string.Concat("Random movement");
                typeof(MonsterAI).GetMethod("IdleMovement", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { dt });
                return false;
                Dbgl("Random Movement ok");
            }

            //protected bool AvoidFire(float dt, Vector3 targetPosition)
            //{
            //    EffectArea effectArea2 = EffectArea.IsPointInsideArea(base.transform.position, EffectArea.Type.Fire, 3f);
            //    if ((bool)effectArea2)
            //    {
            //        if (targetPosition != null && (bool)EffectArea.IsPointInsideArea(targetPosition, EffectArea.Type.Fire))
            //        {
            //            RandomMovementArroundPoint(dt, effectArea2.transform.position, effectArea2.GetRadius() + 3f + 1f, IsAlerted());
            //            return true;
            //        }
            //        RandomMovementArroundPoint(dt, effectArea2.transform.position, (effectArea2.GetRadius() + 3f) * 1.5f, IsAlerted());
            //        return true;
            //    }
            //    return false;
            //}
        }
    


        [HarmonyPatch(typeof(Character), "Awake")]
        static class Character_Awake_Patch
        {
            static void Postfix(Character __instance)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    Debug.Log($"A {__instance.name} just spawned!");
                    __instance.gameObject.AddComponent<Tameable>();
                    //__instance.gameObject.AddComponent<SlaveAI>();

                    var tameable = __instance.gameObject.GetComponent<Tameable>();
                    tameable.m_fedDuration = 500;
                    tameable.m_tamingTime = 1000;
                    tameable.m_commandable = true;
                    var ai = __instance.GetBaseAI() as MonsterAI;
                    ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "SilverNecklace").Single());
                    ai.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Ruby").Single());
                }
            }
        }

        //[HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        //static class Fireplace_UpdateFireplace_Patch
        //{
        //    static void Postfix(Fireplace __instance, ZNetView ___m_nview)
        //    {
        //        if (!Player.m_localPlayer || !isOn.Value || !___m_nview.IsOwner() || (__instance.name.Contains("groundtorch") && !refuelStandingTorches.Value) || (__instance.name.Contains("walltorch") && !refuelWallTorches.Value) || (__instance.name.Contains("fire_pit") && !refuelFirePits.Value))
        //            return;

        //        int maxFuel = (int)(__instance.m_maxFuel - Mathf.Ceil(___m_nview.GetZDO().GetFloat("fuel", 0f)));

        //        List<Container> nearbyContainers = GetNearbyContainers(__instance.transform.position);

        //        Vector3 position = __instance.transform.position + Vector3.up;
        //        foreach (Collider collider in Physics.OverlapSphere(position, dropRange.Value, LayerMask.GetMask(new string[] { "item" })))
        //        {
        //            if (collider?.attachedRigidbody)
        //            {
        //                ItemDrop item = collider.attachedRigidbody.GetComponent<ItemDrop>();
        //                //Dbgl($"nearby item name: {item.m_itemData.m_dropPrefab.name}");

        //                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
        //                    continue;

        //                string name = GetPrefabName(item.gameObject.name);

        //                if (item.m_itemData.m_shared.m_name == __instance.m_fuelItem.m_itemData.m_shared.m_name && maxFuel > 0)
        //                {

        //                    if (fuelDisallowTypes.Value.Split(',').Contains(name))
        //                    {
        //                        //Dbgl($"ground has {item.m_itemData.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"auto adding fuel {name} from ground");

        //                    int amount = Mathf.Min(item.m_itemData.m_stack, maxFuel);
        //                    maxFuel -= amount;

        //                    for (int i = 0; i < amount; i++)
        //                    {
        //                        if (item.m_itemData.m_stack <= 1)
        //                        {
        //                            if (___m_nview.GetZDO() == null)
        //                                Destroy(item.gameObject);
        //                            else
        //                                ZNetScene.instance.Destroy(item.gameObject);
        //                            ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                            break;

        //                        }

        //                        item.m_itemData.m_stack--;
        //                        ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                        Traverse.Create(item).Method("Save").GetValue();
        //                    }
        //                }
        //            }
        //        }

        //        foreach (Container c in nearbyContainers)
        //        {
        //            if (__instance.m_fuelItem && maxFuel > 0)
        //            {
        //                ItemDrop.ItemData fuelItem = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
        //                if (fuelItem != null)
        //                {
        //                    maxFuel--;
        //                    if (fuelDisallowTypes.Value.Split(',').Contains(fuelItem.m_dropPrefab.name))
        //                    {
        //                        //Dbgl($"container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name}, taking one");

        //                    ___m_nview.InvokeRPC("AddFuel", new object[] { });

        //                    c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);
        //                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
        //                    typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
        //                }
        //            }
        //        }
        //    }
        //}

        //[HarmonyPatch(typeof(Smelter), "FixedUpdate")]
        //static class Smelter_FixedUpdate_Patch
        //{
        //    static void Postfix(Smelter __instance, ZNetView ___m_nview)
        //    {
        //        if (!Player.m_localPlayer || !isOn.Value || !___m_nview.IsOwner())
        //            return;

        //        int maxOre = __instance.m_maxOre - Traverse.Create(__instance).Method("GetQueueSize").GetValue<int>();
        //        int maxFuel = __instance.m_maxFuel - Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel", 0f));


        //        List<Container> nearbyContainers = GetNearbyContainers(__instance.transform.position);

        //        Vector3 position = __instance.transform.position + Vector3.up;
        //        foreach (Collider collider in Physics.OverlapSphere(position, dropRange.Value, LayerMask.GetMask(new string[] { "item" })))
        //        {
        //            if (collider?.attachedRigidbody)
        //            {
        //                ItemDrop item = collider.attachedRigidbody.GetComponent<ItemDrop>();
        //                //Dbgl($"nearby item name: {item.m_itemData.m_dropPrefab.name}");

        //                if (item?.GetComponent<ZNetView>()?.IsValid() != true)
        //                    continue;

        //                string name = GetPrefabName(item.gameObject.name);

        //                foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
        //                {
        //                    if (item.m_itemData.m_shared.m_name == itemConversion.m_from.m_itemData.m_shared.m_name && maxOre > 0)
        //                    {

        //                        if (oreDisallowTypes.Value.Split(',').Contains(name))
        //                        {
        //                            //Dbgl($"container at {c.transform.position} has {item.m_itemData.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
        //                            continue;
        //                        }

        //                        Dbgl($"auto adding ore {name} from ground");

        //                        int amount = Mathf.Min(item.m_itemData.m_stack, maxOre);
        //                        maxOre -= amount;

        //                        for (int i = 0; i < amount; i++)
        //                        {
        //                            if (item.m_itemData.m_stack <= 1)
        //                            {
        //                                if (___m_nview.GetZDO() == null)
        //                                    Destroy(item.gameObject);
        //                                else
        //                                    ZNetScene.instance.Destroy(item.gameObject);
        //                                ___m_nview.InvokeRPC("AddOre", new object[] { name });
        //                                break;
        //                            }

        //                            item.m_itemData.m_stack--;
        //                            ___m_nview.InvokeRPC("AddOre", new object[] { name });
        //                            Traverse.Create(item).Method("Save").GetValue();
        //                        }
        //                    }
        //                }

        //                if (__instance.m_fuelItem && item.m_itemData.m_shared.m_name == __instance.m_fuelItem.m_itemData.m_shared.m_name && maxFuel > 0)
        //                {

        //                    if (fuelDisallowTypes.Value.Split(',').Contains(name))
        //                    {
        //                        //Dbgl($"ground has {item.m_itemData.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"auto adding fuel {name} from ground");

        //                    int amount = Mathf.Min(item.m_itemData.m_stack, maxFuel);
        //                    maxFuel -= amount;

        //                    for (int i = 0; i < amount; i++)
        //                    {
        //                        if (item.m_itemData.m_stack <= 1)
        //                        {
        //                            if (___m_nview.GetZDO() == null)
        //                                Destroy(item.gameObject);
        //                            else
        //                                ZNetScene.instance.Destroy(item.gameObject);
        //                            ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                            break;

        //                        }

        //                        item.m_itemData.m_stack--;
        //                        ___m_nview.InvokeRPC("AddFuel", new object[] { });
        //                        Traverse.Create(item).Method("Save").GetValue();
        //                    }
        //                }
        //            }
        //        }

        //        foreach (Container c in nearbyContainers)
        //        {
        //            foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
        //            {
        //                ItemDrop.ItemData oreItem = c.GetInventory().GetItem(itemConversion.m_from.m_itemData.m_shared.m_name);

        //                if (oreItem != null && maxOre > 0)
        //                {
        //                    maxOre--;
        //                    if (oreDisallowTypes.Value.Split(',').Contains(oreItem.m_dropPrefab.name))
        //                        continue;

        //                    Dbgl($"container at {c.transform.position} has {oreItem.m_stack} {oreItem.m_dropPrefab.name}, taking one");

        //                    ___m_nview.InvokeRPC("AddOre", new object[] { oreItem.m_dropPrefab?.name });
        //                    c.GetInventory().RemoveItem(itemConversion.m_from.m_itemData.m_shared.m_name, 1);
        //                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
        //                    typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
        //                }
        //            }

        //            if (__instance.m_fuelItem && maxFuel > 0)
        //            {
        //                ItemDrop.ItemData fuelItem = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
        //                if (fuelItem != null)
        //                {
        //                    maxFuel--;
        //                    if (fuelDisallowTypes.Value.Split(',').Contains(fuelItem.m_dropPrefab.name))
        //                    {
        //                        //Dbgl($"container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
        //                        continue;
        //                    }

        //                    Dbgl($"container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name}, taking one");

        //                    ___m_nview.InvokeRPC("AddFuel", new object[] { });

        //                    c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);
        //                    typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
        //                    typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
        //                }
        //            }
        //        }
        //    }
        //}

        //[HarmonyPatch(typeof(Console), "InputText")]
        //static class InputText_Patch
        //{
        //    static bool Prefix(Console __instance)
        //    {
        //        if (!modEnabled.Value)
        //            return true;
        //        string text = __instance.m_input.text;
        //        if (text.ToLower().Equals("autofuel reset"))
        //        {
        //            context.Config.Reload();
        //            context.Config.Save();

        //            Traverse.Create(__instance).Method("AddString", new object[] { text }).GetValue();
        //            Traverse.Create(__instance).Method("AddString", new object[] { "AutoFuel config reloaded" }).GetValue();
        //            return false;
        //        }
        //        return true;
        //    }
        //}
    }
}