using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.DarkInteract
{
    /// <summary>
    /// 断电可交互设备：跳过 DeviceBase.GetInteractMessageBase 中
    /// 「Survive + Darkness + !CanUseDarkness → DarknessError」门闩
    /// （0.1.16a DeviceBase.cs:365-368）。纯客户端；服务端不校验黑暗交互。
    /// </summary>
    [PatchFeature(
        "断电可交互：黑暗中仍可与原版禁止交互的设备互动（修电箱等本就可黑暗交互的设备行为不变）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class DarkInteractFeature
    {
    }
}
