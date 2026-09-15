using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.Dev
{
    /// <summary>
    /// IsPlaytestApp 恒 true；PAY/INVENTORY URL 固定正式服常量。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "IsPlaytestApp",
        description: "测试模式：按测试服逻辑运行（例如可更少人数开局），支付与库存仍走正式服。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class PlaytestModeFeature
    {
        [HarmonyPatch(typeof(Define), nameof(Define.IsPlaytestApp), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixIsPlaytestApp(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Define), nameof(Define.PAY_BACKEND_URL), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixPayBackendUrl(ref string __result)
        {
            __result = Define.PAY_BACKEND_URL_MAIN;
            return false;
        }

        [HarmonyPatch(typeof(Define), nameof(Define.INVENTORY_CHECK_URL), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixInventoryCheckUrl(ref string __result)
        {
            __result = Define.INVENTORY_CHECK_URL_MAIN;
            return false;
        }
    }
}
