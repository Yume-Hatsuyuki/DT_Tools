using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.DarkInteract
{
    [PatchFeature(
        "断电可交互：黑暗中仍可与原版禁止交互的设备互动（修电箱等本就可黑暗交互的设备行为不变）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameDarkness | FeatureTag.GameDevice,
        Author = "梦初雪")]
    public sealed class DarkInteractFeature
    {
    }
}
