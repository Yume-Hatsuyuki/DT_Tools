using HarmonyLib;

namespace DT_Tools.Patches.DevMode
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   Define::get_IsPlaytestApp()
    ///   Define::get_PAY_BACKEND_URL()
    ///   Define::get_INVENTORY_CHECK_URL()
    ///
    /// <b>原版效果</b>：
    ///   IsPlaytestApp：Steam AppID == 4395210 时为 true，否则 false。
    ///   PAY_BACKEND_URL / INVENTORY_CHECK_URL：随 IsPlaytestApp 在正式服与 pay-dev 间切换。
    ///
    /// <b>修改后效果</b>：
    ///   IsPlaytestApp 固定 true（如 LOBBY_MIN_PLAYER 走 0 分支）。
    ///   两个 URL 固定为正式服地址（与 Define 中非 Playtest 分支字符串一致）。
    ///
    /// <b>修改方式</b>：
    ///   三个 Getter 均 Prefix，写回 __result 后 return false。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "Enable_IsPlaytestApp",
        "测试模式：按测试服逻辑运行（例如可更少人数开局），支付与库存仍走正式服。",
        author: "梦初雪")]
    internal static class Patch_IsPlaytestApp
    {
        // 与 Define 正式服分支字面量一致
        private const string OfficialPayBackend = "https://deadlytrick.finalblow.org/pay";
        private const string OfficialInventoryCheck = "https://deadlytrick.finalblow.org/pay/v1/inventory/check";

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
            __result = OfficialPayBackend;
            return false;
        }

        [HarmonyPatch(typeof(Define), nameof(Define.INVENTORY_CHECK_URL), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixInventoryCheckUrl(ref string __result)
        {
            __result = OfficialInventoryCheck;
            return false;
        }
    }
}
