using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.RemoveFog
{
    [PatchFeature(
        "移除迷雾：移除房间阴影效果。热切换不即时生效：开关都在下一次幽灵视觉变化" +
        "（OnGhostVisualChanged，0.1.18c MyPlayer.cs:1889）时才应用到房间阴影，建议在对局外切换。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameDarkness | FeatureTag.GameGhost,
        Author = "梦初雪")]
    public sealed class RemoveFogFeature
    {
    }
}
