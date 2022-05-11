using BepInEx;
using HarmonyLib;
using RagnarsRokare.MobAI;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace RagnarsRokare.SlaveGreylings
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    public partial class SlaveGreylings : BaseUnityPlugin
    {
        public const string ModId = "RagnarsRokare.SlaveGreylings";
        public const string ModName = "RagnarsRökare SlaveGreylings";
        public const string ModVersion = "0.8.5";

        private static readonly bool isDebug = true;
        
        public static AudioClip CallHomeSfx { get; private set; }

        private void Awake()
        {
            var requiredVersion = new System.Version(0, 3);
            var mobAILibVersion = new System.Version(typeof(MobAILib).Assembly.GetName().Version.Major, typeof(MobAILib).Assembly.GetName().Version.Minor);
            if (mobAILibVersion.CompareTo(requiredVersion) != 0)
            {
                Debug.LogError($"Wrong version of MobAILib. Required:{requiredVersion}, actual:{mobAILibVersion}");
                return;
            }
            CommonConfig.Init(Config);
            GreylingsConfig.Init(Config);
            BruteConfig.Init(Config);
            GreydwarfConfig.Init(Config);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            StartCoroutine(nameof(PreloadSFX));
            EventManager.RegisteredMobsChanged += RegisteredMobsChanged;
        }

        private IEnumerator PreloadSFX()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace(@"\", "/");
            var path = $"file:///{exeDir}/sfx/CallHome.wav";
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.WAV))
            {
                yield return www.SendWebRequest();

                if (www.isHttpError || www.isNetworkError)
                {
                    Debug.LogWarning(www.error);
                }
                else
                {
                    CallHomeSfx = DownloadHandlerAudioClip.GetContent(www);
                }
            }
        }

        private static string GetOrCreateUniqueId(ZNetView ___m_nview)
        {
            var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_UniqueId);
            if (string.IsNullOrEmpty(uniqueId))
            {
                uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_CharacterId);
                if (string.IsNullOrEmpty(uniqueId))
                {
                    uniqueId = System.Guid.NewGuid().ToString();
                }
                ___m_nview.GetZDO().Set(Constants.Z_UniqueId, uniqueId);
            }
            return uniqueId;
        }

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(SlaveGreylings).Namespace + " " : "") + str);
        }
    }
}