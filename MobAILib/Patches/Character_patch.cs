﻿using HarmonyLib;

namespace RagnarsRokare.MobAI
{
    public partial class MobAILib
    {
        [HarmonyPatch(typeof(Character), "Damage")]
        static class Character_Damaged_Patch
        {
            static void Prefix(ref ZNetView ___m_nview, ref HitData hit)
            {
                if (!___m_nview.IsValid() || !___m_nview.IsOwner()) return;
                var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_UniqueId);
                if (string.IsNullOrEmpty(uniqueId)) return;

                if (MobManager.IsAliveMob(uniqueId))
                {
                    var attacker = hit.GetAttacker();
                    if (MobManager.IsAliveMob(uniqueId))
                    {
                        MobManager.AliveMobs[uniqueId].Attacker = attacker;
                    }
                }
            }
        }
    }
}
