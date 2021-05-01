using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RagnarsRokare.SlaveGreylings
{
    public static class Extensions
    {
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

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

        public static ZNetView GetNView<T>(T obj)
        {
            return typeof(T).GetField("m_nview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj) as ZNetView;
        }

        public static string GetUniqueId(this Piece p)
        {
            return GetNView(p)?.GetZDO().GetString(RagnarsRokare.Constants.Z_UniqueId);
        }
    }
}
