using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare_DodgeOnDoubleTap
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    [BepInProcess("valheim.exe")]
    public class DodgeOnDoubleTap : BaseUnityPlugin
    {
        public const string ModId = "RagnarsRokare.DodgeOnDoubleTap";
        public const string ModName = "RagnarsRökare DodgeOnDoubleTapMod";
        public const string ModVersion = "0.6";

        private readonly Harmony harmony = new Harmony(ModId);
        public static ConfigEntry<int> DodgeTapHoldMax;
        public static ConfigEntry<int> DodgeDoubleTapDelay;
        public static ConfigEntry<int> NexusID;
        public static ConfigEntry<string> OnOffKey;
        public static ConfigEntry<bool> IsEnabled;

        private static KeyCode m_onOffKey;


        void Awake()
        {
            Debug.Log($"Loading {ModName} v{ModVersion}, lets get rolling!");
            harmony.PatchAll();
            DodgeTapHoldMax = Config.Bind("General", "DodgeTapHoldMax", 200);
            DodgeDoubleTapDelay = Config.Bind("General", "DodgeDoubleTapDelay", 300);
            NexusID = Config.Bind<int>("General", "NexusID", 871, "Nexus mod ID for updates");
            OnOffKey = Config.Bind<string>("General", "OnOffKey", "End", "Key to toggle the mod on or off");
            IsEnabled = Config.Bind<bool>("General", "IsEnabled", true, "Tells if the mod is currently on or off");
            OnOffKey.SettingChanged += (s, a) => { SetOnOfKey(); };
            SetOnOfKey();
        }

        private static void SetOnOfKey()
        {
            var configValue = OnOffKey.Value;
            if (Enum.TryParse(configValue, out KeyCode key))
            {
                m_onOffKey = key;
            }
            else
            {
                m_onOffKey = KeyCode.End;
            }
            ZInput.Initialize();
            var buttons = typeof(ZInput).GetField("m_buttons", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ZInput.instance) as Dictionary<string, ZInput.ButtonDef>;
            if (buttons.ContainsKey("RR_DTDogdeOnOff"))
            {
                buttons.Remove("RR_DTDogdeOnOff");
            }
            ZInput.instance.AddButton("RR_DTDogdeOnOff", m_onOffKey);
        }

        public enum DodgeDirection { None, Forward, Backward, Left, Right };
        public static DodgeDirection DodgeDir { get; set; } = DodgeDirection.None;

        public static Vector3 GamepadDodgeDir { get; set; } = Vector3.zero;

        [HarmonyPatch(typeof(Player), "SetControls")]
        class SetControls_Patch
        {
            static void Postfix(ref float ___m_queuedDodgeTimer, ref Vector3 ___m_queuedDodgeDir, Vector3 ___m_lookDir)
            {
                if (!IsEnabled.Value)
                {
                    DodgeDir = DodgeDirection.None;
                    GamepadDodgeDir = Vector3.zero;
                    return;
                }

                if (DodgeDir == DodgeDirection.None && GamepadDodgeDir.magnitude < 0.1f) return;

                ___m_queuedDodgeTimer = 0.5f;

                var dodgeDir = ___m_lookDir;
                dodgeDir.y = 0f;
                dodgeDir.Normalize();
                if (GamepadDodgeDir.magnitude > 0)
                {
                    dodgeDir = Quaternion.AngleAxis(Vector3.SignedAngle(GamepadDodgeDir, new Vector3(0, 0, -1), Vector3.up), Vector3.up) * dodgeDir;
                    GamepadDodgeDir = Vector3.zero;
                }
                else
                {
                    if (DodgeDir == DodgeDirection.Backward) dodgeDir = -dodgeDir;
                    else if (DodgeDir == DodgeDirection.Left) dodgeDir = Quaternion.AngleAxis(-90, Vector3.up) * dodgeDir;
                    else if (DodgeDir == DodgeDirection.Right) dodgeDir = Quaternion.AngleAxis(90, Vector3.up) * dodgeDir;
                }
                ___m_queuedDodgeDir = dodgeDir;
                DodgeDir = DodgeDirection.None;
            }
        }

        [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
        static class FixedUpdate_Patch
        {
            private static float m_onOffKeyTimer = 0f;
            private static float m_onOffKeyDelay = 1.0f;


            private static DateTime? m_forwardLastTapRegistered = DateTime.Now;
            private static DateTime? m_backwardLastTapRegistered = DateTime.Now;
            private static DateTime? m_leftLastTapRegistered = DateTime.Now;
            private static DateTime? m_rightLastTapRegistered = DateTime.Now;

            private static DateTime m_forwardLastTapCheck = DateTime.Now;
            private static DateTime m_backwardLastTapCheck = DateTime.Now;
            private static DateTime m_leftLastTapCheck = DateTime.Now;
            private static DateTime m_rightLastTapCheck = DateTime.Now;

            private static float m_forwardPressTimer = 0;
            private static float m_backwardPressTimer = 0;
            private static float m_leftPressTimer = 0;
            private static float m_rightPressTimer = 0;

            private static DateTime? m_GamepadLastTapRegistered = DateTime.Now;
            private static DateTime m_GamepadLastTapCheck = DateTime.Now;
            private static float m_GamepadPressTimer = 0;
            private static Vector3 m_GamepadFirstDir = Vector3.zero;
            private static Vector3 m_GamepadSecondDir = Vector3.zero;

            static bool Prefix(PlayerController __instance, ZNetView ___m_nview)
            {
                if ((bool)___m_nview && !___m_nview.IsOwner())
                {
                    return true;
                }
                if (!(bool)Traverse.Create(__instance).Method("TakeInput").GetValue())
                {
                    return true;
                }
                if ((bool)Traverse.Create(__instance).Method("InInventoryEtc").GetValue())
                {
                    return true;
                }

                if (Time.time - m_onOffKeyTimer > m_onOffKeyDelay && ZInput.GetButton("RR_DTDogdeOnOff"))
                {
                    m_onOffKeyTimer = Time.time;
                    IsEnabled.Value = !IsEnabled.Value;
                }

                if (ZInput.GetButton("Forward"))
                {
                    DetectTap(true, (float)(DateTime.Now - m_forwardLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_forwardPressTimer);
                    m_forwardLastTapCheck = DateTime.Now;
                }
                else
                {
                    var isTap = DetectTap(false, (float)(DateTime.Now - m_forwardLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_forwardPressTimer);
                    m_forwardLastTapCheck = DateTime.Now;
                    CheckForDoubleTapDodge(isTap, ref m_forwardLastTapRegistered, DodgeDirection.Forward);
                }
                if (ZInput.GetButton("Backward"))
                {
                    DetectTap(true, (float)(DateTime.Now - m_backwardLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_backwardPressTimer);
                    m_backwardLastTapCheck = DateTime.Now;
                }
                else
                {
                    bool isTap = DetectTap(false, (float)(DateTime.Now - m_backwardLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_backwardPressTimer);
                    m_backwardLastTapCheck = DateTime.Now;
                    CheckForDoubleTapDodge(isTap, ref m_backwardLastTapRegistered, DodgeDirection.Backward);
                }
                if (ZInput.GetButton("Left"))
                {
                    DetectTap(true, (float)(DateTime.Now - m_leftLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_leftPressTimer);
                    m_leftLastTapCheck = DateTime.Now;
                }
                else
                {
                    bool isTap = DetectTap(false, (float)(DateTime.Now - m_leftLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_leftPressTimer);
                    m_leftLastTapCheck = DateTime.Now;
                    CheckForDoubleTapDodge(isTap, ref m_leftLastTapRegistered, DodgeDirection.Left);
                }
                if (ZInput.GetButton("Right"))
                {
                    DetectTap(true, (float)(DateTime.Now - m_rightLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_rightPressTimer);
                    m_rightLastTapCheck = DateTime.Now;
                }
                else
                {
                    bool isTap = DetectTap(false, (float)(DateTime.Now - m_rightLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_rightPressTimer);
                    m_rightLastTapCheck = DateTime.Now;
                    CheckForDoubleTapDodge(isTap, ref m_rightLastTapRegistered, DodgeDirection.Right);
                }

                // Gamepad
                var v = new Vector3(ZInput.GetJoyLeftStickX(), 0, ZInput.GetJoyLeftStickY());
                if (v.magnitude > 0.9f)
                {
                    DetectTap(true, (float)(DateTime.Now - m_GamepadLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_GamepadPressTimer);
                    m_GamepadLastTapCheck = DateTime.Now;
                    var milliesSinceLastTap = (DateTime.Now - m_GamepadLastTapRegistered)?.TotalMilliseconds ?? DodgeDoubleTapDelay.Value;
                    if (milliesSinceLastTap < DodgeDoubleTapDelay.Value)
                    {
                        m_GamepadSecondDir = v;
                    }
                    else
                    {
                        m_GamepadFirstDir = v;
                    }
                }
                else
                {
                    bool isTap = DetectTap(false, (float)(DateTime.Now - m_GamepadLastTapCheck).TotalMilliseconds, DodgeTapHoldMax.Value, ref m_GamepadPressTimer);
                    m_GamepadLastTapCheck = DateTime.Now;
                    CheckForDoubleTapDodge(isTap, ref m_GamepadLastTapRegistered);
                }
                return true;
            }

            private static void CheckForDoubleTapDodge(bool isTap, ref DateTime? lastTapRegistered, DodgeDirection dodgeDirection)
            {
                if (isTap)
                {
                    var milliesSinceLastTap = (DateTime.Now - lastTapRegistered)?.TotalMilliseconds ?? DodgeDoubleTapDelay.Value;
                    if (milliesSinceLastTap < DodgeDoubleTapDelay.Value)
                    {
                        DodgeDir = dodgeDirection;
                    }
                    else
                    {
                        lastTapRegistered = null;
                    }

                    if (lastTapRegistered == null)
                    {
                        lastTapRegistered = DateTime.Now;
                    }
                }
            }

            private static void CheckForDoubleTapDodge(bool isTap, ref DateTime? lastTapRegistered)
            {
                if (isTap)
                {
                    var milliesSinceLastTap = (DateTime.Now - lastTapRegistered)?.TotalMilliseconds ?? DodgeDoubleTapDelay.Value;
                    if (milliesSinceLastTap < DodgeDoubleTapDelay.Value && Vector3.Dot(m_GamepadFirstDir, m_GamepadSecondDir) > 0.95)
                    {
                        GamepadDodgeDir = m_GamepadSecondDir;
                    }
                    else
                    {
                        lastTapRegistered = null;
                    }

                    if (lastTapRegistered == null)
                    {
                        lastTapRegistered = DateTime.Now;
                    }
                }
            }

            private static bool DetectTap(bool isPressed, float timeSinceLastCheck, float maxPressTime, ref float pressTimer)
            {
                if (isPressed)
                {
                    pressTimer += timeSinceLastCheck;
                    return false;
                }
                else if (pressTimer > 0)
                {
                    bool isTap = pressTimer < maxPressTime;
                    pressTimer = 0;
                    return isTap;
                }
                return false;
            }
        }
    }
}
