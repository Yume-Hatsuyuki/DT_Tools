using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Features.MastermindWeight
{
    [PatchFeature(
        "黑幕概率：房主可为指定玩家设置黑幕抽取权重（默认 1，0 为不会被选中，越大越可能）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameRound | FeatureTag.PlayerIdentity,
        Author = "梦初雪")]
    public sealed class MastermindWeightFeature
    {
        [Config("默认格子数：未单独设置的玩家拥有的格子数。格子数为 0 的玩家永不被选为黑幕；全部为 0 时回退为均匀抽取。", Min = 0, Max = 1000)]
        public static int DefaultWeight = 10;

        private static void OnDisabled() => MastermindWeights.Clear();
    }
}
