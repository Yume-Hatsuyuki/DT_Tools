using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.PlaytestMode
{
    /// <summary>
    /// 库存校验 URL 整替为正式服常量（即便处于测试模式也不走 dev 后端）。
    /// </summary>
    /// <remarks>
    /// 公共静态属性（nameof 定位）：0.1.18c INVENTORY_CHECK_URL Define.cs:1993；
    /// 常量 INVENTORY_CHECK_URL_MAIN Define.cs:384。
    ///
    /// 0.1.18c：Define 中从未存在 PAY_BACKEND_URL 属性（TiamatPayClient 与免费货币 HTTP grant 两版本均无），
    /// 故本文件只保留 INVENTORY_CHECK_URL 的 getter 补丁。
    /// </remarks>
    [HarmonyPatch]
    internal static class PlaytestModeBackendUrlPatch
    {
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
