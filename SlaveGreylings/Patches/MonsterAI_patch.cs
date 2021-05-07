using HarmonyLib;
using RagnarsRokare.MobAI;

namespace RagnarsRokare.MobAI
{
    public partial class SlaveGreylings
    {
        [HarmonyPatch(typeof(MonsterAI), "MakeTame")]
        static class MonsterAI_MakeTame_Patch
        {
            static void Postfix(MonsterAI __instance)
            {
                if (MobManager.IsControllableMob(__instance.name))
                {
                    var mobInfo = MobManager.GetMobInfo(__instance.name);
                    __instance.m_consumeItems.Clear();
                    __instance.m_consumeItems.AddRange(mobInfo.PostTameConsumables);
                    __instance.m_consumeSearchRange = 50;
                }
            }
        }
    }
}
