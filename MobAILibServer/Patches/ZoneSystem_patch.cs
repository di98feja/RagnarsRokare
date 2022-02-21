using HarmonyLib;

namespace RagnarsRokare.MobAI.Server
{
    public partial class MobAILibServer
    {
        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        static class ZoneSystem_Start_Patch
        {
            static void Postfix()
            {
                AdoptedZonesManager.RegisterRPCs();
            }
        }
   }
}