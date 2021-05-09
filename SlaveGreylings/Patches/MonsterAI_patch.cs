using HarmonyLib;
using RagnarsRokare.MobAI;

namespace RagnarsRokare.SlaveGreylings
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        static class MonsterAI_MakeTame_Patch
        {
            static void Postfix(MonsterAI __instance)
            {
                if (MobConfigManager.IsControllableMob(__instance.name))
                {
                    var mobInfo = MobConfigManager.GetMobConfig(__instance.name);
                    __instance.m_consumeItems.Clear();
                    __instance.m_consumeItems.AddRange(mobInfo.PostTameConsumables);
                    __instance.m_consumeSearchRange = 50;
                }
            }
        }
    }
}
