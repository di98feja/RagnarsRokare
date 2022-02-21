using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class MobAILib
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

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        static class MonsterAI_UpdateAI_Patch
        {
            static bool Prefix(MonsterAI __instance, float dt, ref ZNetView ___m_nview, ref Character ___m_character, ref float ___m_timeSinceHurt, 
                ref float ___m_jumpInterval, ref float ___m_jumpTimer, ref float ___m_randomMoveUpdateTimer, ref bool ___m_alerted)
            {
                if (!___m_nview.IsValid()) return true;
                var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_UniqueId);
                if (string.IsNullOrEmpty(uniqueId)) return true;
                if (!MobManager.IsRegisteredMob(uniqueId)) return true;

                var mobAI = GetOrCreateMob(uniqueId, __instance, ___m_nview);
                if (null == mobAI) return true;
                if (!___m_nview.IsOwner()) return false;
                if (__instance.IsSleeping())
                {
                    Common.Invoke<MonsterAI>(__instance, "UpdateSleep", dt);
                    Common.Dbgl($"{___m_character.GetHoverName()}: Sleep updated", true);
                    return false;
                }

                BaseAI_UpdateAI_ReversePatch.UpdateAI(__instance, dt, ___m_nview, ref ___m_jumpInterval, ref ___m_jumpTimer, ref ___m_randomMoveUpdateTimer, ref ___m_timeSinceHurt, ref ___m_alerted);
                mobAI.UpdateAI(dt);

                return false;
            }

            private static MobAIBase GetOrCreateMob(string uniqueId, MonsterAI instance, ZNetView nview)
            {
                MobAIBase mob;
                if (MobManager.IsAliveMob(uniqueId))
                {
                    mob = MobManager.AliveMobs[uniqueId];
                    if (!mob.HasInstance())
                    {
                        mob = MobManager.CreateMob(uniqueId, instance);
                        MobManager.AliveMobs[uniqueId] = mob;
                        Common.Dbgl($"Replacing old instance of mob '{mob.Character.m_name}', IsOwner:{nview.IsOwner()}", true);
                    }
                    return mob;
                }
                else
                {
                    mob = MobManager.CreateMob(uniqueId, instance);
                }

                if (mob == null)
                {
                    Common.Dbgl($"Failed to create mob {uniqueId}', IsOwner:{nview.IsOwner()}", true);
                    return null;
                }

                Common.Dbgl($"Adding new instance of mob '{mob.Character.GetHoverName()}', IsOwner:{nview.IsOwner()}", true);
                MobManager.AliveMobs.Add(uniqueId, mob);
                return mob;
            }
        }
    }
}
