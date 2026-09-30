using System;
using System.Reflection;
using HarmonyLib;

namespace DT_Tools.Core
{
    /// <summary>
    /// Harmony 反射基建（Core 层，不认识任何游戏功能）：按名取私有方法并绑定为
    /// 开放实例委托。原先散在 CharacterMapPin/PlayerRadar/SurviveSpeed 四处的
    /// 私有 Bind 副本收敛于此（0.1.15b 审计 F-M10）。
    /// </summary>
    public static class Reflect
    {
        /// <summary>按名取私有实例方法并绑定为开放实例委托（游戏方法缺失时返回 null，调用方自行兜底）。</summary>
        public static T Bind<T>(Type owner, string name, Type[] parameters = null) where T : Delegate
        {
            MethodInfo mi = AccessTools.Method(owner, name, parameters);
            return mi == null ? null : (T)Delegate.CreateDelegate(typeof(T), null, mi);
        }
    }
}
