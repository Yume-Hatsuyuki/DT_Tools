using System;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.BackendProxy
{
    /// <summary>
    /// Define.INVENTORY_CHECK_URL 后置替换（公共静态属性，nameof 定位）：
    /// 0.1.16b Define.cs:1995（原版 = 支付后端 origin + /pay[dev]/v1/inventory/check，正式 :384 / 测试 :386）。
    /// 与 PayBackend 同一机制：整替原版 origin、游戏路径（含 /pay、/v1/…）原样保留。
    /// 消费点：CosmeticSightingReporter.CoSend 每批上报现读（0.1.16b CosmeticSightingReporter.cs:170）。
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
