using HarmonyLib;
using System;
using System.Collections.Generic;

namespace RagnarsRokare.MobAI
{
    public static class EventManager
    {
        public static event EventHandler<RegisteredMobsChangedEventArgs> RegisteredMobsChanged;
        public static event EventHandler ServerShutdown;

        internal static void RegisteredMobsChangedEvent_RPC(long sender, ZPackage pkg)
        {
            bool endOfStream = false;
            var allMobZDOIDs = new List<ZDOID>();
            while (!endOfStream)
            {
                try
                {
                    allMobZDOIDs.Add(pkg.ReadZDOID());
                }
                catch (System.IO.EndOfStreamException)
                {
                    endOfStream = true;
                }
            }
            OnRegisteredMobChanged(new RegisteredMobsChangedEventArgs(allMobZDOIDs));
        }

        internal static void ServerShutdownEvent_RPC(long sender)
        {
            OnServerShutdown(new EventArgs());
        }

        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        static class ZoneSystem_Start_Patch
        {
            static void Postfix()
            {
                ZRoutedRpc.instance.Register<ZPackage>(Constants.Z_RegisteredMobsChangedEvent, RegisteredMobsChangedEvent_RPC);
                ZRoutedRpc.instance.Register(Constants.Z_ServerShutdownEvent, ServerShutdownEvent_RPC);
            }
        }

        private static void OnRegisteredMobChanged(RegisteredMobsChangedEventArgs e)
        {
            RegisteredMobsChanged?.Invoke(null, e);
        }

        private static void OnServerShutdown(EventArgs e)
        {
            ServerShutdown?.Invoke(null, e);
        }
    }

    public class RegisteredMobsChangedEventArgs : EventArgs
    {
        public List<ZDOID> AllMobZDOIDs { get; set; }

        public RegisteredMobsChangedEventArgs(List<ZDOID> allMobZDOIDs)
        {
            this.AllMobZDOIDs = allMobZDOIDs;
        }
    }
}
