using System;
using System.Collections.Generic;
using System.Linq;
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

        public static T RandomOrDefault<T>(this IEnumerable<T> list)
        {
            if (list == null || !list.Any()) return list.FirstOrDefault();

            int index = new Random().Next(list.Count());
            return list.ElementAt(index);
        }
    }
}
