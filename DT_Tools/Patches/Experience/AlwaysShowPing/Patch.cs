using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.AlwaysShowPing
{
    /// <summary>
    /// 替换 TickPingIndicator 的大厅限定逻辑（private，字符串定位：
    /// 0.1.17a UI_GameScene.cs:1935）。开启时由本补丁全阶段刷新并保活指示器；
    /// 关闭时放行原版（仅 Lobby 刷新）。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameScene), "TickPingIndicator")]
    internal static class AlwaysShowPingTickPatch
    {
        private static bool Prefix(UI_GameScene __instance)
        {
            if (!Engine.Enabled<AlwaysShowPingFeature>())
                return true;

            AlwaysShowPingLogic.Tick(__instance);
            return false;
        }
    }
}
