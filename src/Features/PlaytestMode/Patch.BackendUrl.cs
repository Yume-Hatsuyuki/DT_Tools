// 作用：为测试模式挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.PlaytestMode
{
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
