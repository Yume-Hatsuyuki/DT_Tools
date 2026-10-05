using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.AutoRejoin
{
    /// <summary>
    /// 断线自动重连（整合自独立插件 DT_AutoRejoin v1.0.0，行为与原版逐条一致）：
    /// 1. 自动重连：网络断线（ErrorNetworkUnstable）被踢回大厅时，自动按原房间码重新加入
    ///    （默认尝试 3 次）。只认网络错误；收到房主踢出包（S_KICKED）或被移出房间
    ///    （S_LEAVE_GAME 且是自己）时明确不重连——这是房主的管理权，本功能不碰。
    /// 2. 断线归位（房主功能）：对局中掉线的玩家房主保留其占位角色；玩家重进时直接把
    ///    原角色、原位置还给他（RevertDummy），根治"重连后变幽灵观战者"。黑名单玩家
    ///    不归位、死亡状态下线的玩家不归位（避免复活作弊）。
    /// 只装自己：掉线后自动重连，但中途进房是观战（原版行为）；房主也开：重连直接回角色。
    /// </summary>
    [PatchFeature(
        "断线自动重连：网络掉线自动按原房间码重新加入（默认 3 次尝试）；被房主踢出/移出绝不重连；房主开启时支持断线归位（重进恢复原角色而非幽灵观战，死亡玩家不归位）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "花语")]
    public sealed class AutoRejoinFeature
    {
        [Config("自动重连开关（仅网络断线触发；被房主踢出/移出不触发）。")]
        public static bool AutoRejoinEnabled = true;

        [Config("自动重连最大尝试次数。")]
        public static int MaxAttempts = 3;

        [Config("回到大厅后第一次尝试重连前的等待秒数。")]
        public static float FirstDelaySeconds = 5f;

        [Config("每次重连失败后的等待秒数。")]
        public static float RetryDelaySeconds = 8f;

        [Config("（房主功能）掉线玩家重进房间时恢复其原角色而不是变成幽灵观战；房主开了才生效。")]
        public static bool HostResumeEnabled = true;
    }
}
