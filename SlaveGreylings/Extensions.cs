using System;
using System.Reflection;

namespace SlaveGreylings
{
    public static class Extensions
    {
        public static Tameable Tameable(this MonsterAI instance)
        {
            if (instance == null) throw new ArgumentException("Instance is missing");
            return instance.GetType().GetField("m_tamable", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance) as Tameable;
        }
    }
}
