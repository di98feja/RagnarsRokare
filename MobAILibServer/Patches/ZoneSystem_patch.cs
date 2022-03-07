using HarmonyLib;
using System;

namespace RagnarsRokare.MobAI.Server
{
    public partial class MobAILibServer
    {
        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        static class ZoneSystem_Start_Patch
        {
            static void Postfix()
            {
                if (ZNet.instance.IsServer())
                {
                    AdoptedZonesManager.RegisterRPCs();
                    ZRoutedRpc.instance.m_onNewPeer = (Action<long>)Delegate.Combine(ZRoutedRpc.instance.m_onNewPeer, (Action<long>)((l) => 
                    {
                        AdoptedZonesManager.FireMobRegisterChangedEvent();
                    }));
                }
            }
        }
   }
}