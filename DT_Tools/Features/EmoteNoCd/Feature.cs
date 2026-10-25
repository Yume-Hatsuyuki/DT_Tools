using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.EmoteNoCd
{
    [PatchFeature(
        "表情发送无冷却：可连续使用表情动作。",
        defaultEnabled: true,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerEmote,
        Author = "梦初雪")]
    public sealed class EmoteNoCdFeature
    {
    }
}
