using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RagnarsRokare.MobAI
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

        public static T RandomOrDefault<T>(this IEnumerable<T> list)
        {
            if (list == null || !list.Any()) return list.FirstOrDefault();

            int index = new Random().Next(list.Count());
            return list.ElementAt(index);
        }

        public static string GetUniqueId(this Piece p)
        {
            return Common.GetNView(p)?.GetZDO().GetString(RagnarsRokare.Constants.Z_UniqueId);
        }
    }
}
