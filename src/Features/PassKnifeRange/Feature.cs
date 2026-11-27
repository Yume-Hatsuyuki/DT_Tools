// 作用：声明递刀距离功能、配置项与启用状态；不实现补丁与运行时逻辑。
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
