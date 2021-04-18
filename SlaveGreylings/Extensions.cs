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

        public static GreylingAI.State ToStateEnum(this string s)
        {
            GreylingAI.State state;
            if (Enum.TryParse(s, out state))
            {
                return state;
            }
            throw new ArgumentException($"Unknown State string:{s}");
        }
    }
}
