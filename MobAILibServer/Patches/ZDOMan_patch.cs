using HarmonyLib;

namespace RagnarsRokare.MobAI.Server
{
    public partial class MobAILibServer
    {
        [HarmonyPatch(typeof(ZDOMan), "Load")]
        static class ZDOMan_Load_Patch
        {
            static void Postfix(ref ZDOMan __instance)
            {
                MobManager.LoadMobs(__instance);
            }
        }
    }
}
