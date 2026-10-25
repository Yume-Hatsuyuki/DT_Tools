using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.AttackRange
{
    [PatchFeature(
        "黑方攻击距离：可修改最大攻击距离（默认 224）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerCombat,
        Author = "梦初雪")]
    public sealed class AttackRangeFeature
    {
        [Config("黑方攻击可命中的最大距离，游戏默认为 224。", Min = 0f)]
        public static float Range = 224f;
    }
}
