using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Dev.PlaytestMode
{
    /// <summary>
    /// 库存校验 URL 整替为正式服常量（即便处于测试模式也不走 dev 后端）。
    /// 公共静态属性（nameof 定位）：0.1.17a INVENTORY_CHECK_URL Define.cs:1991；
    /// 常量 INVENTORY_CHECK_URL_MAIN Define.cs:384。
    ///
    /// 0.1.17a：Define.PAY_BACKEND_URL 属性已删除（TiamatPayClient / 免费货币 HTTP grant 移除），
    /// 故本文件不再包含 PAY_BACKEND_URL 的 Prefix。
    /// </summary>
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
