using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.SlaveGreylings
{
    [RequireComponent(typeof(AudioSource))]
    [HarmonyPatch(typeof(PlayerController), "Awake")]
    public static class PlayerController_Awake_Patch
    {
        static void Postfix(ZNetView ___m_nview)
        {
            if (___m_nview.IsValid())
            {
                ___m_nview.Register<Vector3>(Constants.Z_CallHomeCommand, RPC_CallHome);
            }
        }

        public static void RPC_CallHome(long sender, Vector3 position)
        {
            PlayClipAt(SlaveGreylings.CallHomeSfx, position);
        }

        private static void PlayClipAt(AudioClip clip, Vector3 pos)
        {
            if (clip == null) return;
            GameObject tempGO = new GameObject("TempAudio");
            tempGO.transform.position = pos; 
            AudioSource aSource = tempGO.AddComponent<AudioSource>();
            aSource.clip = clip;
            aSource.reverbZoneMix = 0.1f;
            aSource.maxDistance = 200f;
            aSource.spatialBlend = 1.0f;
            aSource.rolloffMode = AudioRolloffMode.Linear;
            aSource.Play();
            MonoBehaviour.Destroy(tempGO, clip.length);
        }
    }

    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    static class PlayerController_FixedUpdate_Patch
    {
        private static float m_callHomeKeyTimer = 0f;
        private static float m_callHomeKeyDelay = 5.0f;
        private static KeyCode m_callHomeKey;

        static PlayerController_FixedUpdate_Patch()
        {
            var configValue = CommonConfig.CallHomeCommandKey.Value;
            if (Enum.TryParse(configValue, out KeyCode key))
            {
                m_callHomeKey = key;
            }
            else
            {
                m_callHomeKey = KeyCode.Home;
            }
        }

        static void Postfix(ZNetView ___m_nview)
        {
            if (Time.time - m_callHomeKeyTimer < m_callHomeKeyDelay) return;
            if (Input.GetKey(m_callHomeKey))
            {
                m_callHomeKeyTimer = Time.time;
                ___m_nview.InvokeRPC(ZNetView.Everybody, Constants.Z_CallHomeCommand, Player.m_localPlayer.transform.position);

                var charsInRange = new List<Character>();
                var m_nview = typeof(Character).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic);
                Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 200, charsInRange);
                foreach (var character in charsInRange)
                {
                    var nview = m_nview.GetValue(character) as ZNetView;
                    var uniqueId = nview.GetZDO().GetString(Constants.Z_CharacterId);
                    if (MobManager.IsControlledMob(uniqueId))
                    {
                        MobManager.Mobs[uniqueId].Follow(Player.m_localPlayer);
                    }
                }
            }
        }
    }
}
