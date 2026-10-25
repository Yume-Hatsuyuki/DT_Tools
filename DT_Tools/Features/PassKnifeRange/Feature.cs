using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.PassKnifeRange
{
    [PatchFeature(
        "递刀距离：可修改黑幕最大递交武器的距离（默认 224）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameWeapon | FeatureTag.PlayerCombat,
        Author = "梦初雪")]
    public sealed class PassKnifeRangeFeature
    {
        [Config("递交武器可触及的最大距离，游戏默认为 224。", Min = 0f)]
        public static float Range = 224f;
    }
}
