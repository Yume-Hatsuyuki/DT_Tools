using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.GhostView
{
    [PatchFeature(
        "幽灵视角：（⚠️奇怪的功能）化身幽灵。可能影响正常对局，建议仅本地调试。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameGhost | FeatureTag.PlayerVisual,
        Author = "梦初雪")]
    public sealed class GhostViewFeature
    {
    }
}
