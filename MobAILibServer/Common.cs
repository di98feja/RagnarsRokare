using System.Reflection;

namespace RagnarsRokare.MobAI.Server
{
    public class Common
    {
        public static object Invoke<T>(object instance, string methodName, params object[] argumentList)
        {
            return typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, argumentList);
        }
   }
}