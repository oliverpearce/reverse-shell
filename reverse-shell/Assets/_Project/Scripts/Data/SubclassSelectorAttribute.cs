using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Put on a [SerializeReference] field or list to get a type-picker dropdown in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SubclassSelectorAttribute : PropertyAttribute { }

    public static class ManagedReferenceUtility
    {
        /// <summary>Replaces repeated references in the list with independent copies.</summary>
        public static void MakeUnique<T>(IList<T> list, HashSet<object> seen) where T : class
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null) continue;
                if (seen.Add(item)) continue;
                var clone = (T)JsonUtility.FromJson(JsonUtility.ToJson(item), item.GetType());
                list[i] = clone;
                seen.Add(clone);
            }
        }
    }
}
