using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ServerPasswordHelper
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin(ModId, ModName, ModVersion)]
    public class SlaveGreylings : BaseUnityPlugin
    {
        public const string ModId = "RagnarsRokare.ServerPasswordHelper";
        public const string ModName = "RagnarsRökare ServerPasswordHelper";
        public const string ModVersion = "0.3";

        public static ConfigEntry<string> lastServerIPAddress;
        public static ConfigEntry<int> NexusID;

        private void Awake()
        {
            lastServerIPAddress = Config.Bind<string>("General", "LastUsedServerIPAdress", "", "The last used IP adress of server");
            NexusID = Config.Bind<int>("General", "NexusID", 862, "Nexus mod ID for updates");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        static class ZNet_OnNewConnection_Patch
        {
            private static RectTransform m_connectingDialog;
            private static RectTransform m_passwordDialog;
            private static ZRpc m_tempPasswordRPC;
            private static ZNet m_instance;

            static void Postfix(ZNet __instance, ref ZNetPeer peer, ref RectTransform ___m_connectingDialog, ref RectTransform ___m_passwordDialog)
            {
                peer.m_rpc.Register<bool>("ClientHandshake", RPC_ClientHandshake);
                m_connectingDialog = ___m_connectingDialog;
                m_passwordDialog = ___m_passwordDialog;
                m_instance = __instance;
            }
            private static void RPC_ClientHandshake(ZRpc rpc, bool needPassword)
            {
                m_connectingDialog.gameObject.SetActive(value: false);
                if (needPassword)
                {
                    var args = Environment.GetCommandLineArgs();
                    string prefilledPassword = string.Empty;
                    for (int i = 0; i < args.Length; i++)
                    {
                        if ((args[i].ToLower() == "pwd" || args[i].ToLower() == "+password") && args.Length > i+1)
                        {
                            prefilledPassword = args[i + 1];
                        }
                    }
                    if (string.IsNullOrEmpty(prefilledPassword))
                    {
                        m_passwordDialog.gameObject.SetActive(value: true);
                        InputField componentInChildren = m_passwordDialog.GetComponentInChildren<InputField>();
                        componentInChildren.ActivateInputField();
                        m_passwordDialog.GetComponentInChildren<InputFieldSubmit>().m_onSubmit = (pwd) =>
                        {
                            if (m_tempPasswordRPC.IsConnected())
                            {
                                m_passwordDialog.gameObject.SetActive(value: false);
                                Traverse.Create(m_instance).Method("SendPeerInfo", new object[] { m_tempPasswordRPC, pwd }).GetValue();
                                m_tempPasswordRPC = null;
                            }
                        };
                        m_tempPasswordRPC = rpc;
                    }
                    else
                    {
                        Debug.Log("Autofill password");
                        m_passwordDialog.gameObject.SetActive(value: false);
                        Traverse.Create(m_instance).Method("SendPeerInfo", new object[] { rpc, prefilledPassword }).GetValue();
                    }
                }
                else
                {
                    Traverse.Create(m_instance).Method("SendPeerInfo", new object[] { rpc }).GetValue();
                }
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "OnJoinIPOpen")]
        static class FejdStartup_OnJoinIPStartup_Patch
        {
            static void Postfix(FejdStartup __instance, ref InputField ___m_joinIPAddress)
            {
                ___m_joinIPAddress.text = lastServerIPAddress.Value;
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "OnJoinIPConnect")]
        static class FejdStartup_OnJoinIPConnect_Patch
        {
            static void Prefix(FejdStartup __instance, ref InputField ___m_joinIPAddress)
            {
                lastServerIPAddress.Value = ___m_joinIPAddress.text;
            }
        }
    }
}
