using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Features.MastermindWeight
{
    /// <summary>
    /// 黑幕概率：房主可为指定玩家设置黑幕抽取权重（默认 1；0 为不会被选中；全部为 0 时回退为均匀抽取）。
    /// </summary>
    /// <remarks>
    /// 权重由 /mastermind_weight 命令设置，仅在当前房间有效，关闭功能时清空。
    /// 补丁挂在 GameRoom.SetMasterMind 上（0.1.18c Server.Game/GameRoom.cs:326），房主侧生效。
    /// </remarks>
    [PatchFeature(
        "黑幕概率：房主可为指定玩家设置黑幕抽取权重（默认 1，0 为不会被选中，越大越可能）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameRound | FeatureTag.PlayerIdentity,
        Author = "梦初雪")]
    public sealed class MastermindWeightFeature
    {
        /// <summary>未单独设置的玩家拥有的格子数（默认 10）。</summary>
        [Config("默认格子数：未单独设置的玩家拥有的格子数。格子数为 0 的玩家永不被选为黑幕；全部为 0 时回退为均匀抽取。", Min = 0, Max = 1000)]
        public static int DefaultWeight = 10;

        /// <summary>热关闭：清空权重，下次开启从默认值开始。</summary>
        private static void OnDisabled() => MastermindWeights.Clear();
    }
}
