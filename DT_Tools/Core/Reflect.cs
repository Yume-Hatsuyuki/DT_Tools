using System;
using System.Reflection;
using HarmonyLib;

namespace DT_Tools.Core
{
    public static class Reflect
    {
        public static T Bind<T>(Type owner, string name, Type[] parameters = null) where T : Delegate
        {
            MethodInfo mi = AccessTools.Method(owner, name, parameters);
            return mi == null ? null : (T)Delegate.CreateDelegate(typeof(T), null, mi);
        }
    }
}
