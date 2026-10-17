using System;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.BackendProxy
{
    /// <summary>
    /// Define.INVENTORY_CHECK_URL 后置替换（公共静态属性，nameof 定位）：
    /// 0.1.18c Define.cs:1993（原版 = 支付后端 origin + /pay[dev]/v1/inventory/check，正式 :384 / 测试 :386）。
    /// 整替原版 origin、游戏路径（含 /pay、/v1/…）原样保留。
    /// 消费点：CosmeticSightingReporter 每批上报现读。
    /// 已核对：两个版本的免费货币都不走 HTTP grant（走 Steam 掉落）；本补丁仅覆盖装扮目击/库存校验。
    /// </summary>
    [HarmonyPatch(typeof(Define), nameof(Define.INVENTORY_CHECK_URL), MethodType.Getter)]
    internal static class BackendProxyInventoryCheckPatch
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
