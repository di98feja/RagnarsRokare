using HarmonyLib;

namespace RagnarsRokare.MobAI.Server
{
    public partial class MobAILibServer
    {
        [HarmonyPatch(typeof(Game), "Shutdown")]
        static class Game_Shutdown_Patch
        {
            static void Prefix()
            {
                if (ZNet.instance.IsServer())
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZNetView.Everybody, Constants.Z_ServerShutdownEvent);
                }
            }
        }
   }
}