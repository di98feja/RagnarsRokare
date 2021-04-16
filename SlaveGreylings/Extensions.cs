using HarmonyLib;
using System;

namespace SlaveGreylings
{
    public static class Extensions
    {
        public static Tameable Tameable(this MonsterAI instance)
        {
            if (instance == null) throw new ArgumentException("Instance is missing");
            return Traverse.Create(instance).Field("m_tameable").GetValue(instance) as Tameable;
        }
    }
}
