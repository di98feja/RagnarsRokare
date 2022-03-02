using System.Reflection;
using UnityEngine;

namespace RagnarsRokare
{
    public class Utils
    {
        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }

        public static string GetPrefabName(string name)
        {
            char[] anyOf = new char[] { '(', ' ' };
            int num = name.IndexOfAny(anyOf);
            string result;
            if (num >= 0)
                result = name.Substring(0, num);
            else
                result = name;
            return result;
        }

        public static float DistanceXZ(Vector3 v0, Vector3 v1)
        {
            float num = v1.x - v0.x;
            float num2 = v1.z - v0.z;
            return Mathf.Sqrt(num * num + num2 * num2);
        }
    }
}