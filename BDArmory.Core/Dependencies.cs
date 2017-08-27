using System;
using System.Collections.Generic;

namespace BDArmory.Core
{
    public static  class Dependencies
    {
        private static readonly Dictionary<Type, object> Systems = new Dictionary<Type, object>();
        public static void Register<T,TN>()
        {
            if (!Systems.ContainsKey(typeof(T)))
            {

                 Systems.Add(typeof(T), Activator.CreateInstance<TN>());
            }
        }

        public static void Register<T>(object obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException("Registering null constant");
            }
            if (!Systems.ContainsKey(typeof(T)))
            {
                Systems.Add(typeof(T), obj);
            }
        }

        public static T Get<T>() where T : class
        {
            Type type = typeof(T);
            Object instance;
            Systems.TryGetValue(type, out instance);

            if (instance == null)
            {
                instance = Activator.CreateInstance<T>();
                Systems.Add(type, instance);
            }

            return instance as T;
        }

        public static bool Exist<T>() where T : class
        {
            return Systems.ContainsKey(typeof(T));
        }
    }
}
