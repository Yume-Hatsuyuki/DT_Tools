using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ChatTranslate
{
    [PatchFeature(
        "游戏内聊天翻译：自己和别人的普通聊天消息本地翻译成中文（免 key 网页接口或百度开放平台 API）。\n原文照常显示，译文作为第二条本地消息（[翻译版]标签可自定义颜色，仅自己可见，不广播）；\n发送中文可自动翻成目标语言广播，让所有人看到译文。翻译失败显示原文。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.UiChat,
        Author = "花语")]
    public sealed class ChatTranslateFeature
    {
        [Config("百度翻译开放平台 应用ID（appid）。申请：https://fanyi-api.baidu.com，标准版免费。留空走免 key 模式。")]
        public static string BaiduAppId = "";

        [Config("百度翻译开放平台 应用密钥。与 appid 配套，留空走免 key 模式。")]
        public static string BaiduSecretKey = "";

        [Config("源语言：auto=自动检测（推荐）；也可固定，如 en / kor / jp。")]
        public static string SourceLang = "auto";

        [Config("目标语言：zh=简体中文（默认）。")]
        public static string TargetLang = "zh";

        [Config("翻译结果缓存条数上限，超出后清空重建，防止内存膨胀。")]
        public static int CacheCapacity = 500;

        [Config("单次翻译 HTTP 超时（毫秒），超时显示原文。")]
        public static int RequestTimeoutMs = 3000;

        [Config("发送翻译：开启后，发出的中文消息自动翻译成目标语言再广播，让所有人看到译文（需已配置百度 key 或开启免 key 模式）。")]
        public static bool TranslateOutgoing = false;

        [Config("发送翻译目标语言：kor=韩文（默认，游戏主语言）、en=英文、jp=日文等。")]
        public static string OutgoingLang = "kor";

        [Config("免 key 模式：未填 appid/密钥时自动走百度网页翻译接口（免费，接口为逆向实现，可能随官方调整失效）。关闭后必须填 key 才能翻译。")]
        public static bool WebApiFallback = true;

        [Config("翻译版标签颜色（十六进制 #RRGGBB，如 #4CAF50 绿色）。译文作为第二条本地消息显示，仅自己可见，格式：[翻译版]译文。")]
        public static string TranslationTagColor = "#4CAF50";
    }
}
