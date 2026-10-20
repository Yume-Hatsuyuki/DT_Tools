using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LobbyScreenVideo
{
    /// <summary>
    /// 大厅屏幕视频：把等待大厅的大厅屏幕（原版为世界空间记分板屏幕，
    /// 0.1.18c UI_ScoreBoard / GetWorldUI&lt;UI_ScoreBoard&gt;，显示各玩家金/银星）
    /// 改为视频播放器。配置视频路径后，大厅阶段持续播放（默认循环）；
    /// 离开大厅（开局/结算）自动暂停并还原星星显示。
    /// 仅本机配置生效：每台机器各自播放本机配置的视频，不接收任何网络下发的媒体源。
    /// </summary>
    /// <remarks>
    /// 渲染：在 UI_ScoreBoard 根节点下挂透明覆盖层（RawImage + VideoPlayer），
    /// 用 RenderTexture 输出，矩形与星星屏面板容器 Content 对齐（不受其
    /// CanvasGroup 透明度影响，亦不铺满根画布），后添加的兄弟节点绘制在最上层，
    /// 天然盖住星星列表，无需改动原版内部对象。播放状态由 LobbyVideoController
    /// 的 Update 每帧与 EGameState.Lobby 同步，配置热改（路径/循环/音量）即时生效。
    /// 支持 http(s) 链接与本地文件绝对路径（mp4/H.264 等 VideoPlayer 支持格式）。
    /// 媒体来源仅来自本机配置（VideoTrack），不经网络同步。
    /// </remarks>
    [PatchFeature(
        "大厅屏幕视频：把等待大厅的大厅屏幕（原版显示各玩家星星的记分板屏幕）改为视频播放器。配置视频路径后大厅阶段持续循环播放（默认循环），离开大厅自动暂停并还原星星显示。仅本机配置生效：每台机器各自播放本机配置的视频，不接收任何网络下发的媒体源。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.UiHud | FeatureTag.GameDevice | FeatureTag.SysHost,
        Author = "合理")]
    public sealed class LobbyScreenVideoFeature
    {
        /// <summary>
        /// 视频来源：http(s) 链接或本机文件绝对路径（VideoPlayer 支持格式，如 mp4/H.264）。
        /// </summary>
        /// <remarks>
        /// 留空 = 不播放，大厅屏幕显示原版星星。
        /// </remarks>
        [Config("视频来源：http(s) 链接或本机文件绝对路径（mp4/H.264 等 VideoPlayer 支持格式）。留空 = 不播放，显示原版星星屏。")]
        public static string VideoTrack = "";

        /// <summary>
        /// 循环播放（默认开）。关闭后播完一遍停在末帧，进入大厅时重新从开头播放。
        /// </summary>
        [Config("循环播放（默认开）。关闭后播放一遍停在末帧。")]
        public static bool Loop = true;

        /// <summary>
        /// 视频音量（0–1），仅当视频含音轨时生效。
        /// </summary>
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
