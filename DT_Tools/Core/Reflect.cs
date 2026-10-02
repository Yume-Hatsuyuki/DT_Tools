using System;
using System.Reflection;
using HarmonyLib;

namespace DT_Tools.Core
{
    /// <summary>
    /// Harmony 反射基建（Core 层，不认识任何游戏功能）：按名取私有方法并绑定为
    /// 开放实例委托。原先散在 CharacterMapPin/PlayerRadar/SurviveSpeed 四处的
    /// 私有 Bind 副本收敛于此（0.1.16b 审计 F-M10）。
    ///
    /// 为什么缓存开放实例委托而不是 Traverse：HarmonyX 的 Traverse 无 MethodInfo 重载，
    /// 且 Traverse 绑定目标实例、跨实例不能复用——逐帧热路径统一「缓存 MemberInfo +
    /// CreateDelegate（或本方法）」，一次定位进程内生效。
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
