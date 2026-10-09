using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.AlwaysShowPing
{
    /// <summary>
    /// 延迟常显：原版 UI_PingIndicator 仅在大厅（EGameState.Lobby）由
    /// TickPingIndicator 刷新，且其节点通常挂在 Lobby 面板下，离开大厅后随面板
    /// 一起被 HideAll 关掉。本功能在任意阶段持续刷新并强制保持可见（必要时
    /// 将指示器挂到 UI_GameScene 根节点）。
    /// 数据源与原版一致：房主用平均客人延迟，客人用到房主的 Steam P2P ping。
    /// </summary>
    [PatchFeature(
        "延迟常显：离开大厅后仍在右下角实时显示网络延迟（原版仅大厅可见）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class AlwaysShowPingFeature
    {
        [Config("刷新间隔（秒）。原版为 1 秒。", Min = 0.2f, Max = 5f)]
        public static float RefreshInterval = 1f;

        private static void OnDisabled() => AlwaysShowPingLogic.RestoreParent();
    }
}
