using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using System.Linq;
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
            Debug.LogWarning($"AliveMobs:{MobManager.AliveMobs.Count}:{string.Join(",",MobManager.AliveMobs.Values.Select(m => m.HasInstance() ? m.NView?.GetZDO()?.GetString(Constants.Z_GivenName) : "no instance")) ?? "unknown"}");
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
        private static float m_KeyDelay = 3.0f;
        private static float m_callHomeKeyTimer = 0f;
        private static KeyCode m_callHomeKey;
        private static float m_updateSignFromContainerKeyTimer = 0f;
        private static KeyCode m_updateSignFromContainerKey;

        static PlayerController_FixedUpdate_Patch()
        {
            if (Enum.TryParse(CommonConfig.CallHomeCommandKey.Value, out KeyCode key))
            {
                m_callHomeKey = key;
            }
            else
            {
                m_callHomeKey = KeyCode.Home;
            }
            if (Enum.TryParse(CommonConfig.UpdateSignFromContainerKey.Value, out key))
            {
                m_updateSignFromContainerKey = key;
            }
            else
            {
                m_updateSignFromContainerKey = KeyCode.Insert;
            }
        }

        static void Postfix(ZNetView ___m_nview)
        {
            if (Time.time - m_callHomeKeyTimer > m_KeyDelay && Input.GetKey(m_callHomeKey))
            {
                Common.Dbgl($"CallHome command", "SlaveGreylings");
                m_callHomeKeyTimer = Time.time;
                ___m_nview.InvokeRPC(ZNetView.Everybody, Constants.Z_CallHomeCommand, Player.m_localPlayer.transform.position);

                var charsInRange = new List<Character>();
                var m_nview = typeof(Character).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic);
                Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 190, charsInRange);
                foreach (var character in charsInRange)
                {
                    var nview = m_nview.GetValue(character) as ZNetView;
                    var uniqueId = nview.GetZDO().GetString(Constants.Z_CharacterId);
                    if (MobManager.IsAliveMob(uniqueId))
                    {
                        MobManager.AliveMobs[uniqueId].Follow(Player.m_localPlayer);
                    }
                }
            }
            else if (Time.time - m_updateSignFromContainerKeyTimer > m_KeyDelay && Input.GetKey(m_updateSignFromContainerKey))
            {
                Common.Dbgl($"UpdateSignFormContainer command", "SlaveGreylings");
                m_updateSignFromContainerKeyTimer = Time.time;
                SlaveGreylings.UpdateSignFromContainer();
            }
        }
    }
}
