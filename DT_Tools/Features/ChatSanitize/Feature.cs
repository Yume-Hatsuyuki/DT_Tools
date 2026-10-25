using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ChatSanitize
{
    [PatchFeature(
        "房主侧聊天长度过滤：原版发言截断 100 字，开启后按 MaxLength 放行（需房主运行本插件）。\n与客户端「ChatLimit」输入框抬限独立配置；客户端上限高于本值时发言仍会被截断。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.UiInput | FeatureTag.SysHost,
        Author = "梦初雪")]
    public sealed class ChatSanitizeFeature
    {
        [Config("房主侧聊天长度上限（原版 100）。", Min = 100, Max = 8000)]
        public static int MaxLength = 1000;
    }
}
