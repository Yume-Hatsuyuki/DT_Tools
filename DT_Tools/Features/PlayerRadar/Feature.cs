using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.PlayerRadar
{
    /// <summary>
    /// 玩家雷达：平板地图显示全员 Pin。
    /// </summary>
    /// <remarks>
    /// 去掉原版 LateUpdate 中
    /// 「白方且存活则不刷新他人」分支（0.1.18c UI_GameTablet.cs / :3004）。
    /// </remarks>
    [PatchFeature(
        "玩家雷达：平板电脑显示全部玩家位置（白方无法区分黑方/黑幕）。热开启下一帧即生效；" +
        "热关闭后已显示的他人 Pin 会残留原位（白方存活期间原版不再刷新它们，死亡或重开对局才恢复），建议在对局外切换。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiTablet | FeatureTag.UiMapPin | FeatureTag.GameIntel,
        Author = "梦初雪")]
    public sealed class PlayerRadarFeature
    {
    }
}
