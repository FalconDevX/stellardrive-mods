using System;
using System.Collections.Generic;
using Core.Services;

namespace Reentry
{
    internal static class ServiceCache
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        internal static T Get<T>()
        {
            if (Services.TryGetValue(typeof(T), out object cached))
            {
                if (cached is UnityEngine.Object unityObject && unityObject != null)
                {
                    return (T)cached;
                }
                Services.Remove(typeof(T));
            }
            T service = ServiceLocator.GetService<T>();
            if (service != null)
            {
                Services[typeof(T)] = service;
            }
            return service;
        }

        internal static void Reset() => Services.Clear();
    }
}
