using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(BaseAI), "UpdateAI")]
        class BaseAI_UpdateAI_ReversePatch
        {
            [HarmonyReversePatch]
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void UpdateAI(BaseAI instance, float dt, ZNetView m_nview, ref float m_jumpInterval, ref float m_jumpTimer,
                ref float m_randomMoveUpdateTimer, ref float m_timeSinceHurt, ref bool m_alerted)
            {
                if (m_nview.IsOwner())
                {
                    instance.UpdateTakeoffLanding(dt);
                    if (m_jumpInterval > 0f)
                    {
                        m_jumpTimer += dt;
                    }
                    if (m_randomMoveUpdateTimer > 0f)
                    {
                        m_randomMoveUpdateTimer -= dt;
                    }
                    typeof(BaseAI).GetMethod("UpdateRegeneration", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { dt });
                    m_timeSinceHurt += dt;
                }
                else
                {
                    m_alerted = m_nview.GetZDO().GetBool("alert");
                }
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        static class MonsterAI_MakeTame_Patch
        {
            static void Postfix(MonsterAI __instance)
            {
                if (__instance.name.Contains("Greyling"))
                {
                    __instance.m_consumeItems.Clear();
                    __instance.m_consumeItems.Add(ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "Resin").FirstOrDefault());
                    __instance.m_consumeSearchRange = 50;
                }
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        static class MonsterAI_UpdateAI_Patch
        {

            static MonsterAI_UpdateAI_Patch()
            {
            }

            static bool Prefix(MonsterAI __instance, float dt, ref ZNetView ___m_nview, ref Character ___m_character, ref float ___m_fleeIfLowHealth,
                ref float ___m_timeSinceHurt, ref Vector3 ___arroundPointTarget, ref float ___m_jumpInterval, ref float ___m_jumpTimer,
                ref float ___m_randomMoveUpdateTimer, ref bool ___m_alerted, ref Tameable ___m_tamable)
            {
                if (!___m_nview.IsOwner())
                {
                    return false;
                }
                if (!___m_character.IsTamed())
                {
                    return true;
                }
                if (!__instance.name.Contains("Greyling"))
                {
                    return true;
                }
                if (__instance.IsSleeping())
                {
                    Invoke(__instance, "UpdateSleep", new object[] { dt });
                    Dbgl($"{___m_character.GetHoverName()}: Sleep updated");
                    return false;
                }

                BaseAI_UpdateAI_ReversePatch.UpdateAI(__instance, dt, ___m_nview, ref ___m_jumpInterval, ref ___m_jumpTimer, ref ___m_randomMoveUpdateTimer, ref ___m_timeSinceHurt, ref ___m_alerted);
                string mobId = InitInstanceIfNeeded(__instance);
                MobManager.Mobs[mobId].UpdateAI(__instance, dt);

                return false;
            }

            private static object Invoke(MonsterAI instance, string methodName, object[] argumentList)
            {
                return typeof(MonsterAI).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
            }

            private static string InitInstanceIfNeeded(MonsterAI instance)
            {
                var nview = typeof(BaseAI).GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance) as ZNetView;
                //Debug.Log($"Field exists:{Traverse.Create(typeof(BaseAI)).Field("m_character").FieldExists()}");
                //var nview = Traverse.Create<BaseAI>().Field("m_nview").GetValue<ZNetView>(instance);
                var uniqueId = nview.GetZDO().GetString(Constants.Z_CharacterId);

                if (!MobManager.IsControlledMob(uniqueId))
                {
                    var mob = new GreylingAI();
                    MobManager.Mobs.Add(uniqueId, mob);
                }
                return uniqueId;
            }
        }
    }
}
