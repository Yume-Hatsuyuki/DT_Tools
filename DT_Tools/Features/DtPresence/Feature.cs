using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.DtPresence
{
    [PatchFeature(
        "DT_Tools 在场暴露：向同房间玩家公布本机已安装本模组（Lobby Member Data），并可把其他同模玩家头顶名字标色。",
        defaultEnabled: true,
        side: FeatureSide.Client,
        Tags = FeatureTag.PlayerIdentity | FeatureTag.SysNetwork,
        Author = "梦初雪")]
    public sealed class DtPresenceFeature
    {
        [Config("公布版本号：开启时 Member Data 写入完整版本（如 2.0.0.6），关闭时只写占位值 1 表示在场。")]
        public static bool PublishVersion = true;

        [Config("标记同模玩家：为其他已安装 DT_Tools 的玩家头顶名字上色（仅本机可见）。")]
        public static bool MarkOthers = false;

        [Config("标记颜色（HTML，如 #D9D914）。成为黑方后会在原版红与此色之间呼吸过渡。")]
        public static string MarkColor = "#D9D914";

        [Config("黑方呼吸周期（秒）：标记色与原版红之间来回一次的时间。", Min = 0.4f, Max = 8f)]
        public static float PulsePeriod = 2.0f;

        private static void OnPatched() => DtPresenceLogic.EnsureHost();

        private static void OnEnabled() => DtPresenceLogic.OnFeatureEnabled();

        private static void OnDisabled() => DtPresenceLogic.OnFeatureDisabled();
    }
}
