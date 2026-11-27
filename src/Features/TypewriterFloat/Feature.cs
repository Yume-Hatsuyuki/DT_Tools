// 作用：声明打字机消息浮文化功能、配置项与启用状态；不实现补丁与运行时逻辑。
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.TypewriterFloat
{
    [PatchFeature(
        "打字机消息浮文化：生存阶段对讲机（打字机）公开频道的消息以浮文样式匿名显示" +
        "（自己的消息不显示）。关闭时恢复原版。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiChat | FeatureTag.GameDevice,
        Author = "梦初雪")]
    public sealed class TypewriterFloatFeature
    {
    }
}
