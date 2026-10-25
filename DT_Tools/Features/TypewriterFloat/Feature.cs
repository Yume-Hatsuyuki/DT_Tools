using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.TypewriterFloat
{
    [PatchFeature(
        "打字机消息浮文化：生存阶段对讲机（打字机）公开频道的消息一律以秘密通话浮文样式匿名显示" +
        "（秘密通话维持原版浮文；自己的消息不显示）。关闭时恢复原版。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiChat | FeatureTag.GameDevice,
        Author = "梦初雪")]
    public sealed class TypewriterFloatFeature
    {
    }
}
