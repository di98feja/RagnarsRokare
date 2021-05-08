using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public static class MobManager
    {
        #region MobControllers
        private static readonly Dictionary<string, MobInfo> m_mobControllers = new Dictionary<string, MobInfo>();

        static MobManager()
        {
            foreach (var mobController in GetAllControllableMobTypes())
            {
                try
                {
                    var instance = Activator.CreateInstance(mobController) as IControllableMob;
                    RegisterMobController(instance);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to instanciate MobAIController type:{e.Message}");
                }
            }
        }

        public static void RegisterMobController(IControllableMob mob)
        {
            var mobInfo = mob.GetMobInfo();
            m_mobControllers.Add(mobInfo.Name, mobInfo);
        }

        public static IEnumerable<string> GetRegisteredMobControllers()
        {
            return m_mobControllers.Keys;
        }

        private static IEnumerable<Type> GetAllControllableMobTypes()
        {
            var it = typeof(IControllableMob);
            var asm = Assembly.GetExecutingAssembly();
            return asm.GetLoadableTypes().Where(it.IsAssignableFrom).Where(t => !(t.Equals(it))).ToList();
        }
        #endregion

        #region Mobs
        public static Dictionary<string, MobAIBase> AliveMobs = new Dictionary<string, MobAIBase>();
        private static Dictionary<string, string> MobsRegister = new Dictionary<string, string>();

        public static void RegisterMob(Character character, string uniqueId, string mobController)
        {
            if (string.IsNullOrEmpty(uniqueId)) throw new ArgumentException("UniqueId must not be empty");
            if (!m_mobControllers.ContainsKey(mobController)) throw new ArgumentException($"Unknown mob controller {mobController}");

            if (MobsRegister.ContainsKey(uniqueId))
            {
                MobsRegister[uniqueId] = mobController;
            }
            else
            {
                MobsRegister.Add(uniqueId, mobController);
                SetUniqueId(character, uniqueId);
            }
        }

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

        public static bool IsAliveMob(string id)
        {
            return string.IsNullOrEmpty(id) ? false : AliveMobs.ContainsKey(id);
        }

        public static MobInfo GetMobInfo(string mobName)
        {
            var name = Common.GetPrefabName(mobName);
            return m_mobControllers.ContainsKey(name) ? m_mobControllers[name] : null;
        }

        public static MobAIBase CreateMob(string uniqueId, BaseAI baseAI)
        {
            if (!MobsRegister.ContainsKey(uniqueId)) return null;

            var controllerName = MobsRegister[uniqueId];
            var mobType = m_mobControllers[controllerName].AIType;
            return Activator.CreateInstance(mobType, new object[]{ baseAI }) as MobAIBase;
        }

        private static void SetUniqueId(Character character, string uniqueId)
        {
            var nview = typeof(Character).GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(character) as ZNetView;
            uniqueId = System.Guid.NewGuid().ToString();
            nview.GetZDO().Set(Constants.Z_CharacterId, uniqueId);
        }

        #endregion
    }
}
