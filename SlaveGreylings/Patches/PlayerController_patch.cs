using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SlaveGreylings.Patches
{
    [RequireComponent(typeof(AudioSource))]
    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    static class PlayerController_FixedUpdate_Patch
    {
        private static float m_callHomeKeyTimer = 0f;
        private static float m_callHomeKeyDelay = 5.0f;
        static void Postfix()
        {
            if (Time.time - m_callHomeKeyTimer < m_callHomeKeyDelay) return;
            if (Input.GetKey(KeyCode.Home))
            {
                m_callHomeKeyTimer = Time.time;
                CallHome();
            }
        }

        private static void CallHome()
        {
            var player = Player.m_localPlayer;
            PlayClipAt(SlaveGreylings.CallHomeSfx, player.transform.position);
            var charsInRange = new List<Character>();
            var m_nview = typeof(Character).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic);
            Character.GetCharactersInRange(player.transform.position, 1000, charsInRange);
            foreach (var character in charsInRange)
            {
                var nview = m_nview.GetValue(character) as ZNetView;
                var uniqueId = nview.GetZDO().GetString(Constants.Z_CharacterId);
                if (MobManager.IsControlledMob(uniqueId))
                {
                    MobManager.Mobs[uniqueId].Follow(player);
                }
            }
        }

        private static void PlayClipAt(AudioClip clip, Vector3 pos)
        {
            if (clip == null) return;
            GameObject tempGO = new GameObject("TempAudio"); 
            tempGO.transform.position = pos; // 
            AudioSource aSource = tempGO.AddComponent<AudioSource>(); 
            aSource.clip = clip;
            aSource.reverbZoneMix = 0.1f;
            aSource.Play(); 
            MonoBehaviour.Destroy(tempGO, clip.length); 
        }
    }
}
