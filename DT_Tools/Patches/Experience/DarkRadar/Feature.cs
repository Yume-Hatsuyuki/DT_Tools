using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.DarkRadar
{
    /// <summary>
    /// 黑幕情报共享：让非黑幕（White 好人 / Black 持刀者）在平板地图上也能看到
    /// 原本仅黑幕（Dark = MasterMind）可见的两类破坏任务标记：
    ///   - 凶器刷新位置：SabotageWeapon（黑幕侧收到 ScWeapon(39) 单播）
    ///   - 已武装可破坏电闸：SabotageFusebox（黑幕侧收到 ScFusebox(38) 单播）
    ///
    /// 原版 SendSabotageMission 只单播给 MasterMind；但武器架 OpenArmory 状态与电闸
    /// MissionType 均经 S_INIT_MAP / S_MODIFY_DEVICE 全员广播——任何客户端的设备缓存里
    /// 都有数据，缺的只是 AddCommonPin 这一步。黑幕侧逻辑完全不动，其原版单播照常工作。
    /// </summary>
    [PatchFeature(
        "黑幕情报共享：白方/持刀者在平板地图上也能看到凶器刷新位置和可破坏电闸标记（原版仅黑幕 Dark 可见）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class DarkRadarFeature
    {
        [Config("巡检间隔（秒）。", Min = 0.5f, Max = 30f)]
        public static float CheckInterval = 2f;

        private static void OnEnabled() => DarkRadarState.ResetClock();

        private static void OnDisabled() => DarkRadarLogic.ClearAll();
    }
}
