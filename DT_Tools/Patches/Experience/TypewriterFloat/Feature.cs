using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.TypewriterFloat
{
    /// <summary>
    /// 打字机消息浮文化（客户端）：生存阶段对讲机（打字机）公开频道的消息改用
    /// 秘密通话浮文样式匿名显示（随机位置倾斜、逐字上屏后淡出）；秘密通话本就
    /// 浮文（仅黑方可见，维持原版）；自己的消息不显示（与秘密通话无自身回声对齐）。
    /// 服务端不过滤对讲机文本，全身份可见。关闭时恢复原版。
    /// </summary>
    [PatchFeature(
        "打字机消息浮文化：生存阶段对讲机（打字机）公开频道的消息一律以秘密通话浮文样式匿名显示" +
        "（秘密通话维持原版浮文；自己的消息不显示）。关闭时恢复原版。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class TypewriterFloatFeature
    {
    }
}
