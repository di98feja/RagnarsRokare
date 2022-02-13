using HarmonyLib;

namespace RagnarsRokare.MobAI.Server
{
    public partial class MobAILibServer
    {
        [HarmonyPatch(typeof(Character), "Awake")]
        static class Character_Awake_Patch
        {
            static void Postfix(Character __instance, ref ZNetView ___m_nview)
            {
                if (!___m_nview.IsValid()) return;
                ___m_nview.Register<string, ZDOID>(Constants.Z_MobRegistered, MobManager.RPC_RegisterMob);
                ___m_nview.Register<string, ZDOID>(Constants.Z_MobUnRegistered, MobManager.RPC_UnRegisterMob);
            }
        }
    }
}
