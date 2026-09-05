using HarmonyLib;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Boolean Define::get_IsPlaytestApp()
    ///           System.String Define::get_PAY_BACKEND_URL()
    ///           System.String Define::get_INVENTORY_CHECK_URL()
    /// 测试模式：强制启用 IsPlaytestApp，但支付/库存始终走正式服接口
    /// 
    /// 原逻辑：
    ///   IsPlaytestApp  → Steam AppID == 4395210 才为 true
    ///   PAY_BACKEND_URL / INVENTORY_CHECK_URL 会随 IsPlaytestApp 切到 pay-dev
    /// 
    /// 补丁逻辑：
    ///   IsPlaytestApp 始终返回 true（获得原版 Playtest 行为，如 LOBBY_MIN_PLAYER = 0）
    ///   两个 URL 强制返回正式服地址，不走 dev
    /// </summary>
    [HarmonyPatch]
    [PatchConfig("Enable_Patch_ForcePlaytestApp", "测试模式：强制 IsPlaytestApp = true。\n大厅最少人数设置为 0，支付与库存仍使用正式服接口。", author: "梦初雪")]
    internal static class Patch_ForcePlaytestApp
    {
        private const string OfficialPayBackend = "https://deadlytrick.finalblow.org/pay";
        private const string OfficialInventoryCheck = "https://deadlytrick.finalblow.org/pay/v1/inventory/check";

        [HarmonyPatch(typeof(Define), nameof(Define.IsPlaytestApp), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixIsPlaytestApp(ref bool __result)
        {
            __result = true;
            return false; // 原方法已被完整替代，不再执行原方法
        }

        [HarmonyPatch(typeof(Define), nameof(Define.PAY_BACKEND_URL), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixPayBackendUrl(ref string __result)
        {
            __result = OfficialPayBackend;
            return false; // 原方法已被完整替代，不再执行原方法
        }

        [HarmonyPatch(typeof(Define), nameof(Define.INVENTORY_CHECK_URL), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixInventoryCheckUrl(ref string __result)
        {
            __result = OfficialInventoryCheck;
            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}