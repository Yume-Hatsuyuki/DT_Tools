using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.NicknameLimit
{
    [PatchFeature(
        "取消主页昵称字数上限：可超过原版权重 12；AllowAnyChar 开启后允许空格、符号、emoji 等任意字符（空名仍不可用）。\n服务端不过滤昵称字符。关闭时自动还原输入框上限与原版门禁。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerIdentity | FeatureTag.UiInput,
        Author = "梦初雪")]
    public sealed class NicknameLimitFeature
    {
        [Config("允许任意字符（空格、符号、emoji 等，仅 CJK 与字母数字以外的字符放开）。空名仍不可用。")]
        public static bool AllowAnyChar = false;

        private static void OnPatched() => NicknameLimitLogic.WireAllowAnyCharChanged();

        private static void OnEnabled()
        {
            NicknameLimitLogic.WireAllowAnyCharChanged();
            NicknameLimitLogic.RefreshAllLobbyScenes("OnEnabled");
        }

        private static void OnDisabled() =>
            NicknameLimitLogic.RestoreAllLobbyScenes();
    }
}
