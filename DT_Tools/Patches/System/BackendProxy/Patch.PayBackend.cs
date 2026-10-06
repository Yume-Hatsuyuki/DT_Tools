using System;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.BackendProxy
{
    /// <summary>
    /// Define.PAY_BACKEND_URL 后置替换（公共静态属性，nameof 定位）：
    /// 0.1.16b Define.cs:1983（原版按 IsPlaytestApp 在正式 :1989 / 测试 :1993 间切换）。
    /// 配置留空直通原版返回值；填入非空值则整替原版 origin（固定 https://deadlytrick.finalblow.org，
    /// :380/:382），游戏路径 /pay、/pay-dev 原样保留——测试服构建无需感知即可继续走 -dev。
    /// 消费点：TiamatPayClient.CoGrant 每次发放现读（0.1.16b TiamatPayClient.cs:60）。
    /// PlaytestMode 的同属性 prefix（强制正式服常量）先行，本 postfix 后行覆盖，
    /// 顺序由 Harmony 的 prefix→postfix 语义固定，无需 HarmonyPriority。
    /// </summary>
    [HarmonyPatch(typeof(Define), nameof(Define.PAY_BACKEND_URL), MethodType.Getter)]
    internal static class BackendProxyPayBackendPatch
    {
        private static void Postfix(ref string __result)
        {
            if (!Engine.Enabled<BackendProxyFeature>())
                return;

            string prefix = BackendProxyLogic.Normalize(BackendProxyFeature.PayBaseUrl);
            if (prefix == null)
                return;

            string origin = BackendProxyLogic.VanillaPayOrigin;
            if (__result == null || !__result.StartsWith(origin, StringComparison.Ordinal))
                return;

            __result = prefix + __result.Substring(origin.Length);
        }
    }
}
