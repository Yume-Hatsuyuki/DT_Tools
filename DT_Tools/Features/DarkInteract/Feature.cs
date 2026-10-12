using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.DarkInteract
{
    /// 断电可交互设备：跳过 DeviceBase.GetInteractMessageBase 中
    /// 「Survive + Darkness + !CanUseDarkness → DarknessError」门闩
    /// （0.1.16b DeviceBase.cs:365（待核对）-368（待核对）)。纯客户端；服务端不校验黑暗交互。
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
