﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public static class MobManager
    {
        #region MobControllers
        private static readonly Dictionary<string, MobAIInfo> m_mobAIs = new Dictionary<string, MobAIInfo>();

        static MobManager()
        {
            foreach (var mobController in GetAllMobAITypes())
            {
                try
                {
                    var instance = Activator.CreateInstance(mobController) as IControllableMob;
                    RegisterMobAI(instance);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to instanciate MobAIController type:{e.Message}");
                }
            }
        }

        /// <summary>
        /// Get a list of all available MobAI types
        /// </summary>
        /// <returns>A list of available MobAI type names to use in RegisterMob</returns>
        public static IEnumerable<string> GetRegisteredMobAIs()
        {
            return m_mobAIs.Keys;
        }

        private static void RegisterMobAI(IControllableMob mob)
        {
            var mobInfo = mob.GetMobAIInfo();
            m_mobAIs.Add(mobInfo.Name, mobInfo);
        }

        private static IEnumerable<Type> GetAllMobAITypes()
        {
            var it = typeof(IControllableMob);
            var asm = Assembly.GetExecutingAssembly();
            return asm.GetLoadableTypes().Where(it.IsAssignableFrom).Where(t => !(t.Equals(it))).ToList();
        }
        #endregion

        #region Mobs
        public static Dictionary<string, MobAIBase> AliveMobs = new Dictionary<string, MobAIBase>();
        private static Dictionary<string, (string controller, string config)> MobsRegister = new Dictionary<string, (string controller, string config)>();

        /// <summary>
        /// Register a Character to use a certain mobAI.
        /// If the given uniqueId already exists its mobAI and config is replaced.
        /// </summary>
        /// <param name="character">The Character component</param>
        /// <param name="uniqueId">An identifier string for this specific mob. Must be unique among all other mobs.</param>
        /// <param name="mobAIName">The name of the mobAI to use</param>
        /// <param name="configAsJson">The JSON serialized config specific to the given mobAI. For example WorkerAI must have a WorkerAIConfig</param>
        public static void RegisterMob(Character character, string uniqueId, string mobAIName, string configAsJson)
        {
            if (string.IsNullOrEmpty(uniqueId)) throw new ArgumentException("UniqueId must not be empty");
            if (!m_mobAIs.ContainsKey(mobAIName)) throw new ArgumentException($"Unknown mob controller {mobAIName}");

            if (MobsRegister.ContainsKey(uniqueId))
            {
                MobsRegister[uniqueId] = (mobAIName, configAsJson);
            }
            else
            {
                MobsRegister.Add(uniqueId, (mobAIName, configAsJson));
                SetUniqueId(character, uniqueId);
            }
        }

        /// <summary>
        /// Unregister mob from using mobAI
        /// </summary>
        /// <param name="uniqueId">The uniqueId used to register mob</param>
        public static void UnregisterMob(string uniqueId)
        {
            if (AliveMobs.ContainsKey(uniqueId))
            {
                AliveMobs.Remove(uniqueId);
            }
            if (MobsRegister.ContainsKey(uniqueId))
            {
                MobsRegister.Remove(uniqueId);
            }
        }

        /// <summary>
        /// Check if there is a registered mob with the given uniqueId
        /// </summary>
        /// <param name="uniqueId">A string unique among all mobs</param>
        /// <returns>True if a registered mob was found</returns>
        public static bool IsRegisteredMob(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return false;
            return MobsRegister.ContainsKey(uniqueId);
        }

        /// <summary>
        /// Check if there is an active mob by the given uniqueId
        /// </summary>
        /// <param name="uniqueId">A string unique among all mobs</param>
        /// <returns>True if there is a mob with an active MobAI with the given uniqueId</returns>
        public static bool IsAliveMob(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return false;
            if (!AliveMobs.ContainsKey(uniqueId)) return false;
            return AliveMobs[uniqueId] != null;
        }

        internal static MobAIBase CreateMob(string uniqueId, BaseAI baseAI)
        {
            if (!MobsRegister.ContainsKey(uniqueId)) return null;

            var controllerName = MobsRegister[uniqueId].controller;
            var configString = MobsRegister[uniqueId].config;
            var mobType = m_mobAIs[controllerName].AIType;
            return Activator.CreateInstance(mobType, new object[]{ baseAI, configString}) as MobAIBase;
        }

        private static void SetUniqueId(Character character, string uniqueId)
        {
            var nview = typeof(Character).GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(character) as ZNetView;
            nview.GetZDO().Set(Constants.Z_CharacterId, uniqueId);
        }

        #endregion
    }
}