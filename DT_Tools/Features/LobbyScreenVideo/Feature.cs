using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LobbyScreenVideo
{
    [PatchFeature(
        "大厅屏幕视频：把等待大厅的大厅屏幕（原版显示各玩家星星的记分板屏幕）改为视频播放器。配置视频路径后大厅阶段持续循环播放（默认循环），离开大厅自动暂停并还原星星显示。仅本机配置生效：每台机器各自播放本机配置的视频，不接收任何网络下发的媒体源。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.UiHud | FeatureTag.GameDevice | FeatureTag.SysHost,
        Author = "合理")]
    public sealed class LobbyScreenVideoFeature
    {
        [Config("视频来源：http(s) 链接或本机文件绝对路径（mp4/H.264 等 VideoPlayer 支持格式）。留空 = 不播放，显示原版星星屏。")]
        public static string VideoTrack = "";

        [Config("循环播放（默认开）。关闭后播放一遍停在末帧。")]
        public static bool Loop = true;

        [Config("视频音量（0–1），仅当视频含音轨时生效。", Min = 0f, Max = 1f)]
        public static float Volume = 0.8f;

        private static void OnPatched() => LobbyScreenVideoLogic.WireConfigChanged();

        private static void OnEnabled()
        {
            LobbyScreenVideoLogic.NotifyDirty();
            LobbyScreenVideoLogic.EnsureCurrentScreen();
        }

        private static void OnDisabled() => LobbyScreenVideoLogic.StopAndRestore();
    }
}
