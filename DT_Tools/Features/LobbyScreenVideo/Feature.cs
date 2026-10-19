using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LobbyScreenVideo
{
    /// <summary>
    /// 大厅屏幕视频：把等待大厅的大厅屏幕（原版为世界空间记分板屏幕，
    /// 0.1.18c UI_ScoreBoard / GetWorldUI&lt;UI_ScoreBoard&gt;，显示各玩家金/银星）
    /// 改为视频播放器。房主配置视频路径后，大厅阶段持续播放（默认循环）；
    /// 离开大厅（开局/结算）自动暂停并还原星星显示。
    /// 客户端（非房主）开启本功能后：房主开启功能并配置视频时播放房主视频
    /// （经 Steam 大厅数据同步，推荐房主使用 http(s) 链接以便跨机播放）；
    /// 房主未开启/未配置或房主视频不可访问时，播放本机配置的视频。
    /// </summary>
    /// <remarks>
    /// 渲染：在 UI_ScoreBoard 根节点下挂透明覆盖层（RawImage + VideoPlayer），
    /// 用 RenderTexture 输出，矩形与星星屏面板容器 Content 对齐（不受其
    /// CanvasGroup 透明度影响，亦不铺满根画布），后添加的兄弟节点绘制在最上层，
    /// 天然盖住星星列表，无需改动原版内部对象。播放状态由 LobbyVideoController
    /// 的 Update 每帧与 EGameState.Lobby 同步，配置热改（路径/循环/音量）即时生效。
    /// 支持 http(s) 链接与本地文件绝对路径（mp4/H.264 等 VideoPlayer 支持格式）。
    /// 房主侧经 SteamMatchmaking.SetLobbyData 发布视频来源，客户端 GetLobbyData 读取。
    /// </remarks>
    [PatchFeature(
        "大厅屏幕视频：把等待大厅的大厅屏幕（原版显示各玩家星星的记分板屏幕）改为视频播放器。房主配置视频路径后大厅阶段持续循环播放（默认循环），离开大厅自动暂停并还原星星显示。客户端（非房主）开启后：房主开启并配置视频时播放房主视频（经 Steam 大厅数据同步，跨机播放建议房主用 http(s) 链接），否则播放本机配置的视频。",
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
