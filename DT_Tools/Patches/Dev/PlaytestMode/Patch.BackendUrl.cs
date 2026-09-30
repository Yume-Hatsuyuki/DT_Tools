using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Dev.PlaytestMode
{
    /// <summary>
    /// 支付/库存 URL 整替为正式服常量（同一主题：即便处于测试模式也不走 dev 后端）。
    /// 公共静态属性（nameof 定位）：0.1.15b PAY_BACKEND_URL Define.cs:1989、
    /// INVENTORY_CHECK_URL Define.cs:2001；常量 PAY_BACKEND_URL_MAIN :380、
    /// INVENTORY_CHECK_URL_MAIN :384（0.1.15b 起getter 内联同值字符串，语义不变）。
    /// </summary>
    [HarmonyPatch]
    internal static class PlaytestModeBackendUrlPatch
    {
        [HarmonyPatch(typeof(Define), nameof(Define.PAY_BACKEND_URL), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixPayBackendUrl(ref string __result)
        {
            if (!Engine.Enabled<PlaytestModeFeature>())
                return true;

            __result = Define.PAY_BACKEND_URL_MAIN;
            return false;
        }

        [HarmonyPatch(typeof(Define), nameof(Define.INVENTORY_CHECK_URL), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixInventoryCheckUrl(ref string __result)
        {
            if (!Engine.Enabled<PlaytestModeFeature>())
                return true;

            __result = Define.INVENTORY_CHECK_URL_MAIN;
            return false;
        }
    }
}
