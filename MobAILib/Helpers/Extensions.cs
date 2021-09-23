using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RagnarsRokare.MobAI
{
    public static class Extensions
    {
        public static Collider[] GetAllColliders(this Pickable p)
        {
            var allColliders = p.GetComponents<Collider>();
            allColliders.AddRangeToArray(p.GetComponentsInChildren<Collider>());
            allColliders.AddRangeToArray(p.transform.GetComponentsInChildren<Collider>());
            Debug.Log($"Got {allColliders.Length} Colliders from {p.GetHoverName()}");
            return allColliders;
        }

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

            int index = new System.Random().Next(list.Count());
            return list.ElementAt(index);
        }

        public static string GetUniqueId(this Piece p)
        {
            return Common.GetNView(p)?.GetZDO().GetString(RagnarsRokare.Constants.Z_UniqueId);
        }

        public static Container GetContainer(this Piece p)
        {
            var container = p.gameObject.GetComponent<Container>();
            if ((bool)container)
            {
                return container;
            }

            container = p.gameObject.GetComponentInChildren<Container>();
            if ((bool)container)
            {
                return container;
            }

            //container = p.gameObject.GetComponentInParent<Container>();
            //if ((bool)container)
            //{
            //    return container;
            //}

            return null;
        }

        public static StaticTarget GetStaticTarget(this Container p)
        {
            var target = p.gameObject.GetComponent<StaticTarget>();
            if ((bool)target)
            {
                return target;
            }

            target = p.gameObject.GetComponentInParent<StaticTarget>();
            if ((bool)target)
            {
                return target;
            }

            target = p.gameObject.GetComponentInChildren<StaticTarget>();
            if ((bool)target)
            {
                return target;
            }


            return null;
        }


        public static IEnumerable<string> SplitBySqBrackets(this string input)
        {
            if (string.IsNullOrEmpty(input)) yield break;

            var counter = new Stack<int>();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '[')
                {
                    counter.Push(i);
                }
                else if (input[i] == ']')
                {
                    var start = counter.Pop();
                    if (counter.Count == 0)
                    {
                        yield return input.Substring(start + 1, i - start - 1);
                    }
                }
            }
        }

        public static ItemDrop GetItemByName(this ObjectDB objectDB, string itemName)
        {
            return objectDB.m_items.SingleOrDefault(i => Common.GetPrefabName(i.name) == itemName)?.GetComponent<ItemDrop>();
        }
    }
}
