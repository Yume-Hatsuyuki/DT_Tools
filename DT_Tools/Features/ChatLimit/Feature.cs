using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ChatLimit
{
    [PatchFeature(
        "取消聊天输入框字数截断：原版输入框上限 100，开启后抬升上限（默认 1000，可改 MaxLength）。\n房主侧发言过滤为独立功能「ChatSanitize」，客户端上限高于其值时发言仍会被服务端截断。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiInput | FeatureTag.UiChat,
        Author = "梦初雪")]
    public sealed class ChatLimitFeature
    {
        [Config("聊天输入框最大字数（范围 100～8000）。", Min = 100, Max = 8000)]
        public static int MaxLength = 1000;

        private static void OnEnabled() => ChatLimitLogic.ApplyToExistingUi();

        private static void OnDisabled() => ChatLimitLogic.RestoreExistingUi();
    }
}
