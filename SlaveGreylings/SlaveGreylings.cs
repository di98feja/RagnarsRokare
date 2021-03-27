using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SlaveGreylings
{
    public class SlaveAI : MonsterAI
    {
        public Smelter m_assignment;
        public Smelter m_lastassignment;
        public Smelter m_oldassignment;
        public bool m_assigned = false;
        public Container m_container1;
        public Container m_container2;
        public Container m_container3;
        public Container m_container4;
        public Container m_container5;
        public List<ItemDrop> m_fetchitems;

        private string m_aiStatus;
        private Character m_attacker = null;

        private void UpdateTarget(Humanoid humanoid, float dt)
        {
        }

        protected override void UpdateAI(float dt)
        {
            if (!m_nview.IsOwner())
            {
                return;
            }
            if (!m_character.IsTamed())
            {
                base.UpdateAI(dt);
            }
            if (IsSleeping())
            {
                var UpdateSleep_method = this.GetType().GetMethod("UpdateSleep", BindingFlags.NonPublic | BindingFlags.Instance);
                UpdateSleep_method.Invoke(this, new object[] { dt });
                return;
            }
            m_aiStatus = "";
            Humanoid humanoid = m_character as Humanoid;
            UpdateTarget(humanoid, dt);
            if (m_character.GetHealthPercentage() < m_fleeIfLowHealth && m_timeSinceHurt < 20f && m_attacker != null)
            {
                Flee(dt, m_attacker.transform.position);
                m_aiStatus = "Low health, flee";
                return;
            }
            //if (m_assignment != null && AvoidFire(dt, m_assignment.transform.position))
            //{
            //    m_aiStatus = "Avoiding fire";
            //    return;
            //}
            //Debug.Log($"IsNofBurning");
            var UpdateConsumeItem_method = typeof(MonsterAI).GetMethod("UpdateConsumeItem", BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log($"{UpdateConsumeItem_method}");
            if (!IsAlerted() && (bool)UpdateConsumeItem_method.Invoke(this, new object[] { humanoid, dt }))
            {
                m_aiStatus = "Consume item";
                return;
            }
            if ((bool)GetFollowTarget())
            {
                Follow(GetFollowTarget(), dt);
                m_aiStatus = "Follow";
                return;
            }

            if (!m_assigned && m_character.IsTamed())
            {
                foreach (Collider collider in Physics.OverlapSphere(m_character.transform.position, 50, LayerMask.GetMask(new string[] { "piece" })))
                {
                    Smelter smelter = collider.transform.parent?.gameObject?.GetComponent<Smelter>();
                    //Debug.Log($"{smelter}");
                    if (smelter?.GetComponent<ZNetView>()?.IsValid() != true)
                        continue;
                    if (smelter?.transform?.position != null && smelter != m_assignment && smelter != m_lastassignment && smelter != m_oldassignment)
                    {
                        m_oldassignment = m_lastassignment;
                        m_lastassignment = m_assignment;
                        m_assignment = smelter;
                        m_assigned = true;
                        return;
                    }

                }
                m_oldassignment = null;
                m_lastassignment = null;
                m_assignment = null;
            }

            if (m_assigned)
            {
                //SetFollowTarget(m_assignment.gameObject);
                MoveTo(dt, m_assignment.transform.position, 0 ,IsAlerted());
                if (Vector3.Distance(m_character.transform.position, m_assignment.transform.position) < 4)
                    m_assigned = false;
                Debug.Log($"{Vector3.Distance(m_character.transform.position, m_assignment.transform.position)}");
                return;
            }


            m_aiStatus = string.Concat("Random movement");
            IdleMovement(dt);
        }

        protected bool AvoidFire(float dt, Vector3 targetPosition)
        {
            EffectArea effectArea2 = EffectArea.IsPointInsideArea(base.transform.position, EffectArea.Type.Fire, 3f);
            if ((bool)effectArea2)
            {
                if (targetPosition != null && (bool)EffectArea.IsPointInsideArea(targetPosition, EffectArea.Type.Fire))
                {
                    RandomMovementArroundPoint(dt, effectArea2.transform.position, effectArea2.GetRadius() + 3f + 1f, IsAlerted());
                    return true;
                }
                RandomMovementArroundPoint(dt, effectArea2.transform.position, (effectArea2.GetRadius() + 3f) * 1.5f, IsAlerted());
                return true;
            }
            return false;
        }
    }

    [BepInPlugin("RagnarsRokare.SlaveGreylings", "SlaveGreylings", "0.1")]
    public class SlaveGreylings : BaseUnityPlugin
    {
        private static readonly bool isDebug = false;

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

        public static List<Container> GetNearbyContainers(Vector3 center)
        {
            try
            {
                List<Container> containers = new List<Container>();

                foreach (Collider collider in Physics.OverlapSphere(center, Mathf.Max(containerRange.Value, 0), LayerMask.GetMask(new string[] { "piece" })))
                {
                    Container container = collider.transform.parent?.parent?.gameObject?.GetComponent<Container>();
                    if (container?.GetComponent<ZNetView>()?.IsValid() != true)
                        continue;
                    if (container?.transform?.position != null && (container.name.StartsWith("piece_chest") || container.name.StartsWith("Container")) && container.GetInventory() != null)
                    {
                        containers.Add(container);
                    }
                }
                return containers;
            }
            catch
            {
                return new List<Container>();
            }
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
                    __instance.gameObject.AddComponent<SlaveAI>();
                    __instance.gameObject.

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

        [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        static class Fireplace_UpdateFireplace_Patch
        {
            static void Postfix(Fireplace __instance, ZNetView ___m_nview)
            {
                if (!Player.m_localPlayer || !isOn.Value || !___m_nview.IsOwner() || (__instance.name.Contains("groundtorch") && !refuelStandingTorches.Value) || (__instance.name.Contains("walltorch") && !refuelWallTorches.Value) || (__instance.name.Contains("fire_pit") && !refuelFirePits.Value))
                    return;

                int maxFuel = (int)(__instance.m_maxFuel - Mathf.Ceil(___m_nview.GetZDO().GetFloat("fuel", 0f)));

                List<Container> nearbyContainers = GetNearbyContainers(__instance.transform.position);

                Vector3 position = __instance.transform.position + Vector3.up;
                foreach (Collider collider in Physics.OverlapSphere(position, dropRange.Value, LayerMask.GetMask(new string[] { "item" })))
                {
                    if (collider?.attachedRigidbody)
                    {
                        ItemDrop item = collider.attachedRigidbody.GetComponent<ItemDrop>();
                        //Dbgl($"nearby item name: {item.m_itemData.m_dropPrefab.name}");

                        if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                            continue;

                        string name = GetPrefabName(item.gameObject.name);

                        if (item.m_itemData.m_shared.m_name == __instance.m_fuelItem.m_itemData.m_shared.m_name && maxFuel > 0)
                        {

                            if (fuelDisallowTypes.Value.Split(',').Contains(name))
                            {
                                //Dbgl($"ground has {item.m_itemData.m_dropPrefab.name} but it's forbidden by config");
                                continue;
                            }

                            Dbgl($"auto adding fuel {name} from ground");

                            int amount = Mathf.Min(item.m_itemData.m_stack, maxFuel);
                            maxFuel -= amount;

                            for (int i = 0; i < amount; i++)
                            {
                                if (item.m_itemData.m_stack <= 1)
                                {
                                    if (___m_nview.GetZDO() == null)
                                        Destroy(item.gameObject);
                                    else
                                        ZNetScene.instance.Destroy(item.gameObject);
                                    ___m_nview.InvokeRPC("AddFuel", new object[] { });
                                    break;

                                }

                                item.m_itemData.m_stack--;
                                ___m_nview.InvokeRPC("AddFuel", new object[] { });
                                Traverse.Create(item).Method("Save").GetValue();
                            }
                        }
                    }
                }

                foreach (Container c in nearbyContainers)
                {
                    if (__instance.m_fuelItem && maxFuel > 0)
                    {
                        ItemDrop.ItemData fuelItem = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                        if (fuelItem != null)
                        {
                            maxFuel--;
                            if (fuelDisallowTypes.Value.Split(',').Contains(fuelItem.m_dropPrefab.name))
                            {
                                //Dbgl($"container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
                                continue;
                            }

                            Dbgl($"container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name}, taking one");

                            ___m_nview.InvokeRPC("AddFuel", new object[] { });

                            c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);
                            typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
                            typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Smelter), "FixedUpdate")]
        static class Smelter_FixedUpdate_Patch
        {
            static void Postfix(Smelter __instance, ZNetView ___m_nview)
            {
                if (!Player.m_localPlayer || !isOn.Value || !___m_nview.IsOwner())
                    return;

                int maxOre = __instance.m_maxOre - Traverse.Create(__instance).Method("GetQueueSize").GetValue<int>();
                int maxFuel = __instance.m_maxFuel - Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel", 0f));


                List<Container> nearbyContainers = GetNearbyContainers(__instance.transform.position);

                Vector3 position = __instance.transform.position + Vector3.up;
                foreach (Collider collider in Physics.OverlapSphere(position, dropRange.Value, LayerMask.GetMask(new string[] { "item" })))
                {
                    if (collider?.attachedRigidbody)
                    {
                        ItemDrop item = collider.attachedRigidbody.GetComponent<ItemDrop>();
                        //Dbgl($"nearby item name: {item.m_itemData.m_dropPrefab.name}");

                        if (item?.GetComponent<ZNetView>()?.IsValid() != true)
                            continue;

                        string name = GetPrefabName(item.gameObject.name);

                        foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
                        {
                            if (item.m_itemData.m_shared.m_name == itemConversion.m_from.m_itemData.m_shared.m_name && maxOre > 0)
                            {

                                if (oreDisallowTypes.Value.Split(',').Contains(name))
                                {
                                    //Dbgl($"container at {c.transform.position} has {item.m_itemData.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
                                    continue;
                                }

                                Dbgl($"auto adding ore {name} from ground");

                                int amount = Mathf.Min(item.m_itemData.m_stack, maxOre);
                                maxOre -= amount;

                                for (int i = 0; i < amount; i++)
                                {
                                    if (item.m_itemData.m_stack <= 1)
                                    {
                                        if (___m_nview.GetZDO() == null)
                                            Destroy(item.gameObject);
                                        else
                                            ZNetScene.instance.Destroy(item.gameObject);
                                        ___m_nview.InvokeRPC("AddOre", new object[] { name });
                                        break;
                                    }

                                    item.m_itemData.m_stack--;
                                    ___m_nview.InvokeRPC("AddOre", new object[] { name });
                                    Traverse.Create(item).Method("Save").GetValue();
                                }
                            }
                        }

                        if (__instance.m_fuelItem && item.m_itemData.m_shared.m_name == __instance.m_fuelItem.m_itemData.m_shared.m_name && maxFuel > 0)
                        {

                            if (fuelDisallowTypes.Value.Split(',').Contains(name))
                            {
                                //Dbgl($"ground has {item.m_itemData.m_dropPrefab.name} but it's forbidden by config");
                                continue;
                            }

                            Dbgl($"auto adding fuel {name} from ground");

                            int amount = Mathf.Min(item.m_itemData.m_stack, maxFuel);
                            maxFuel -= amount;

                            for (int i = 0; i < amount; i++)
                            {
                                if (item.m_itemData.m_stack <= 1)
                                {
                                    if (___m_nview.GetZDO() == null)
                                        Destroy(item.gameObject);
                                    else
                                        ZNetScene.instance.Destroy(item.gameObject);
                                    ___m_nview.InvokeRPC("AddFuel", new object[] { });
                                    break;

                                }

                                item.m_itemData.m_stack--;
                                ___m_nview.InvokeRPC("AddFuel", new object[] { });
                                Traverse.Create(item).Method("Save").GetValue();
                            }
                        }
                    }
                }

                foreach (Container c in nearbyContainers)
                {
                    foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
                    {
                        ItemDrop.ItemData oreItem = c.GetInventory().GetItem(itemConversion.m_from.m_itemData.m_shared.m_name);

                        if (oreItem != null && maxOre > 0)
                        {
                            maxOre--;
                            if (oreDisallowTypes.Value.Split(',').Contains(oreItem.m_dropPrefab.name))
                                continue;

                            Dbgl($"container at {c.transform.position} has {oreItem.m_stack} {oreItem.m_dropPrefab.name}, taking one");

                            ___m_nview.InvokeRPC("AddOre", new object[] { oreItem.m_dropPrefab?.name });
                            c.GetInventory().RemoveItem(itemConversion.m_from.m_itemData.m_shared.m_name, 1);
                            typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
                            typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
                        }
                    }

                    if (__instance.m_fuelItem && maxFuel > 0)
                    {
                        ItemDrop.ItemData fuelItem = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
                        if (fuelItem != null)
                        {
                            maxFuel--;
                            if (fuelDisallowTypes.Value.Split(',').Contains(fuelItem.m_dropPrefab.name))
                            {
                                //Dbgl($"container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
                                continue;
                            }

                            Dbgl($"container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name}, taking one");

                            ___m_nview.InvokeRPC("AddFuel", new object[] { });

                            c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);
                            typeof(Container).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { });
                            typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            static bool Prefix(Console __instance)
            {
                if (!modEnabled.Value)
                    return true;
                string text = __instance.m_input.text;
                if (text.ToLower().Equals("autofuel reset"))
                {
                    context.Config.Reload();
                    context.Config.Save();

                    Traverse.Create(__instance).Method("AddString", new object[] { text }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] { "AutoFuel config reloaded" }).GetValue();
                    return false;
                }
                return true;
            }
        }
    }
}