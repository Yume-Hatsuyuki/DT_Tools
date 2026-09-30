using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.NicknameLimit
{
    /// <summary>
    /// 取消主页昵称限制（权重 CJK×2+字母数字 ≤12），可选放开字符白名单。
    /// 源码侧无 characterLimit；若 Prefab 仍设了硬上限，一并抬高。
    /// 门禁点：OnValidateNickname / EnsureNicknameOrShowError / StartLobbySection 重校验
    /// （0.1.15b UI_LobbyScene.cs:814）/ 各进房入口读 _isNickNameValid——
    /// 全部经由 Logic.IsNameAcceptable 单一判定源，字符与字数两个维度在此正交。
    /// </summary>
    [PatchFeature(
        "取消主页昵称字数上限：可超过原版权重 12；AllowAnyChar 开启后允许空格、符号、emoji 等任意字符（空名仍不可用）。\n服务端不过滤昵称字符。关闭时自动还原输入框上限与原版门禁。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class NicknameLimitFeature
    {
        [Config("允许任意字符（空格、符号、emoji 等，仅 CJK 与字母数字以外的字符放开）。空名仍不可用。")]
        public static bool AllowAnyChar = false;

        private static void OnPatched() => NicknameLimitLogic.WireAllowAnyCharChanged();

        /// <summary>运行时打开：对当前已存在的大厅场景补一次刷新。</summary>
        private static void OnEnabled()
        {
            NicknameLimitLogic.WireAllowAnyCharChanged();
            NicknameLimitLogic.RefreshAllLobbyScenes("OnEnabled");
        }

        /// <summary>热关闭清理：还原 characterLimit 并按原版权重规则重算门禁/按钮着色。</summary>
        private static void OnDisabled() =>
            NicknameLimitLogic.RestoreAllLobbyScenes();
    }
}
