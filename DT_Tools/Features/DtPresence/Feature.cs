using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.DtPresence
{
    /// <summary>
    /// DT_Tools 在场暴露：通过 Steam Lobby Member Data 公布本机已安装本模组，
    /// 并可选标记其他同样安装了本模组的玩家头顶名字颜色。
    /// </summary>
    /// <remarks>
    /// Member Data 键 <c>dt_tools</c>（与游戏原版 <c>build</c> 游戏版本键区分）。
    /// 每人只能写自己的 Member Data；其他人用 GetLobbyMemberData 读取。
    /// 标记仅本机客户端可见（改 UI_NameTag 颜色），不改网络同步。
    /// 黑方原版会把名字染成粉红红（UI_NameTag.SetNameColor，0.1.18c），
    /// 标记开启时在黑方色与自定义色之间做呼吸过渡，避免被固定盖死。
    /// </remarks>
    [PatchFeature(
        "DT_Tools 在场暴露：向同房间玩家公布本机已安装本模组（Lobby Member Data），并可把其他同模玩家头顶名字标色。",
        defaultEnabled: true,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerIdentity | FeatureTag.SysNetwork,
        Author = "梦初雪")]
    public sealed class DtPresenceFeature
    {
        /// <summary>
        /// 在 Member Data 中写入具体版本号（如 2.0.0.6）；关闭则只写占位 "1"。
        /// </summary>
        [Config("公布版本号：开启时 Member Data 写入完整版本（如 2.0.0.6），关闭时只写占位值 1 表示在场。")]
        public static bool PublishVersion = true;

        /// <summary>
        /// 为其他已安装 DT_Tools 的玩家头顶名字上色（仅本机可见）。
        /// </summary>
        [Config("标记同模玩家：为其他已安装 DT_Tools 的玩家头顶名字上色（仅本机可见）。")]
        public static bool MarkOthers = false;

        /// <summary>
        /// 标记颜色（HTML：#RGB / #RRGGBB / #RRGGBBAA）。黑方时会与原版粉红红做呼吸过渡。
        /// </summary>
        [Config("标记颜色（HTML，如 #D9D914）。成为黑方后会在原版红与此色之间呼吸过渡。")]
        public static string MarkColor = "#D9D914";

        /// <summary>
        /// 黑方时红↔标记色呼吸周期（秒）。越小越快。
        /// </summary>
        [Config("黑方呼吸周期（秒）：标记色与原版红之间来回一次的时间。", Min = 0.4f, Max = 8f)]
        public static float PulsePeriod = 2.0f;

        private static void OnPatched() => DtPresenceLogic.EnsureHost();

        private static void OnEnabled() => DtPresenceLogic.OnFeatureEnabled();

        private static void OnDisabled() => DtPresenceLogic.OnFeatureDisabled();
    }
}
