using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 阶段音乐（客户端，实验性）：按游戏阶段播放自定义音乐（http 链接或本地文件，
    /// 由 TrackSource 统一指定来源，仅本地收听）。
    /// 触发点审计（0.1.15b）——击杀：Handle_S_KILL_PLAYER 只发凶手（PacketHandler.cs:952）；
    /// 递刀：UseHandWeapon 发包成功（MyPlayer.cs:779）；阶段切换：GameManagerEX.StartState
    /// （:385，一次覆盖大厅/生存/侦探/开庭/结算）；处刑：UI_TrialEvent.TrialResult（:1214）；
    /// 主菜单/房内大厅：UI_LobbyScene.OnEnable（:1961）+ StartLobby（:446）。
    /// 单播放槽治理：同曲目播放中不重播（连杀防重复）、切阶段只保留一首、限长到点停止。
    /// 原版 BGM 可选静音（各阶段原版会播 DetectiveBGM/TrialMainBGM/Beta_Result_BGM 等，
    /// 含主菜单 MainTitleBGM）。麦克风广播见 MicInject（实验性，失败自动降级仅本地）。
    /// 未来扩展：新增阶段=加 MusicStage 成员 + 对应 [Config] 字段 + 触发补丁；
    /// 「广播设备自动播放音乐」（音乐经广播室对全房间播放）预留为后续功能。
    /// </summary>
    [PatchFeature(
        "[实验性] 阶段音乐：按游戏阶段播放自定义音乐（TrackSource 统一选择在线链接或本地文件，" +
        "仅本地收听）。八阶段独立配置，同曲目不重复播放、切阶段自动只保留一首、可限单次时长，" +
        "可选静音原版 BGM（含主菜单）。\n麦克风广播（实验性）：开启且麦克风未静音时，音乐混入" +
        "你的语音发给所有人；注入失败或麦克风静音时自动降级为仅本地播放。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class StageMusicFeature
    {
        [Config("曲目来源（八阶段统一）：Http=各阶段填在线链接；Local=各阶段填本地文件路径。")]
        public static TrackSource Source = TrackSource.Http;

        [Config("击杀音乐（黑方击杀时播放）。留空=该阶段不播放。")]
        public static string KillTrack = "";

        [Config("递刀音乐（黑幕递出刀时播放）。留空=该阶段不播放。")]
        public static string GiveKnifeTrack = "";

        [Config("大厅音乐（主菜单与房内大厅播放）。留空=该阶段不播放。")]
        public static string LobbyTrack = "";

        [Config("生存阶段音乐（进入生存阶段时播放）。留空=该阶段不播放。")]
        public static string SurviveTrack = "";

        [Config("侦探阶段音乐（进入侦探阶段时播放）。留空=该阶段不播放。")]
        public static string DetectTrack = "";

        [Config("开庭音乐（进入审判阶段时播放）。留空=该阶段不播放。")]
        public static string TrialTrack = "";

        [Config("处刑音乐（审判处刑演出开始时播放）。留空=该阶段不播放。")]
        public static string ExecutionTrack = "";

        [Config("胜利结算音乐（结算画面出现时播放）。留空=该阶段不播放。")]
        public static string VictoryTrack = "";

        [Config("播放音量（含麦克风广播混音音量）。", Min = 0f, Max = 1f)]
        public static float Volume = 1f;

        [Config("单次播放时长上限（秒）。-1 或 0=不限，到点自动停止。对阶段音乐与 /play_audio 点播同样生效。", Min = -1, Max = 600)]
        public static int MaxSeconds = -1;

        [Config("静音原版 BGM：开启后原版各阶段背景音乐不再播放（含主菜单 MainTitleBGM，避免与自定义音乐混音）。")]
        public static bool MuteVanillaBgm = false;

        [Config("麦克风广播（实验性）：开启且麦克风未静音时，阶段音乐混入你的语音发给所有人；注入失败自动降级为仅本地播放。")]
        public static bool MicBroadcast = false;

        // 注意：麦克风广播用的 Harmony 实例归 StageMusicMicInject 自己持有（见该文件），
        // 不能声明在本类——FeatureLoader.HasSelfManagedHarmony 会扫描 Feature 类上的任意
        // static Harmony 字段来判定"自管挂载、引擎跳过 PatchAll"（见 DetectivePhaseFix 的
        // 正当用法：Feature 自己调 PatchAll(typeof(Feature))）。本类从未自管主补丁挂载，
        // 若在此处声明 Harmony 字段，会让引擎误判整个 StageMusic 为自管，从而跳过
        // Patch.Triggers.cs 里全部 [HarmonyPatch] 类的自动挂载——历史事故复现（起因不同，
        // 症状相同：全部阶段音乐静默失效，且启动日志仍显示"补丁已挂载"具有误导性）。

        /// <summary>运行时开启：挂载麦克风广播注入（动态挂载，失败降级不抛出）。</summary>
        private static void OnEnabled()
        {
            StageMusicMicInject.TryMount();
        }
    }
}
