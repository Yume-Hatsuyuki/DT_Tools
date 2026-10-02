using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 阶段音乐（客户端，实验性）：按游戏阶段播放自定义音乐（http 链接或本地文件，
    /// 由 TrackSource 统一指定来源，仅本地收听）。
    /// 触发点审计（0.1.16b，含 2026-09 补充审计）——
    /// 击杀：Handle_S_KILL_PLAYER 只发凶手（PacketHandler.cs:953）；
    /// 递刀：UseHandWeapon 发包成功（MyPlayer.cs:779）；
    /// 阶段切换：GameManagerEX.StartState（GameManagerEX.cs:386，覆盖大厅/选角/生存/侦探/开庭/结算六阶段）；
    /// 处刑：UI_TrialEvent.TrialResult（UI_TrialEvent.cs:1214）；
    /// 主菜单/房内大厅：UI_LobbyScene.OnEnable（UI_LobbyScene.cs:2209）+ StartLobby（GameManagerEX.cs:447）；
    /// 审判子阶段：UI_TrialEvent.StartState（UI_TrialEvent.cs:1652，覆盖讨论/投票/唱票/回放四子阶段，
    /// TrialResult 子阶段沿用上面更精确的处刑触发，不重复挂载）；
    /// 发现尸体：Handle_S_DISCOVER_CORPSE（PacketHandler.cs:821，仅发现者本人，Server.Game/Corpse.cs:349）；
    /// 自己死亡：Handle_S_DEAD（PacketHandler.cs:392，仅死者本人——S_DEAD 只发死者
    /// Server.Game/Player.cs:827；S_NOTIFY_DEAD 实为灵媒知晓通知，不能用作本机死亡信号）；
    /// 结局动画：Handle_S_ENDING_CAMERA（PacketHandler.cs:592，仅 IsEnd=true 时触发）；
    /// 黑幕继承：Handle_S_NOTIFY_BLACK（PacketHandler.cs:482，ByHand=false 非递刀时按客户端 Dark 分支
    /// 判定——收包瞬间本机颜色仍为 Dark 且存活；递刀 ByHand=true 已由 GiveKnife 覆盖）。
    /// 单播放槽治理：同曲目播放中不重播（连杀防重复）、切阶段只保留一首、限长到点停止。
    /// 原版 BGM 可选静音（各阶段原版会播 DetectiveBGM/TrialMainBGM/Beta_Result_BGM 等，
    /// 含主菜单 MainTitleBGM）。麦克风广播见 MicInject（实验性，失败自动降级仅本地）。
    /// 未来扩展：新增阶段=加 MusicStage 成员 + 对应 [Config] 字段 + 触发补丁；
    /// 「广播设备自动播放音乐」（音乐经广播室对全房间播放）预留为后续功能。
    /// </summary>
    [PatchFeature(
        "[实验性] 阶段音乐：按游戏阶段播放自定义音乐（TrackSource 统一选择在线链接或本地文件，" +
        "仅本地收听）。17 个阶段独立配置曲目/音量/时长上限（原 8 阶段 + 选角/审判四子阶段/发现尸体/" +
        "自己死亡/结局动画/黑幕继承，2026-09 补充审计，全部默认留空需自行配置），同曲目不重复播放、" +
        "切阶段自动只保留一首，可选静音原版 BGM（含主菜单）。" +
        "/play_audio 点播不读取本段配置，参数独立传入（见该命令）。\n" +
        "麦克风广播（实验性）：开启且麦克风未静音时，音乐混入你的语音发给所有人；" +
        "注入失败或麦克风静音时自动降级为仅本地播放。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class StageMusicFeature
    {
        [Config("曲目来源（八阶段统一）：Http=各阶段填在线链接；Local=各阶段填本地文件路径。")]
        public static TrackSource Source = TrackSource.Http;

        [Config("击杀音乐（黑方击杀时播放）。留空=该阶段不播放。")]
        public static string KillTrack = "";

        [Config("击杀音乐音量。", Min = 0f, Max = 1f)]
        public static float KillVolume = 1f;

        [Config("击杀音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int KillMaxSeconds = -1;

        [Config("递刀音乐（黑幕递出刀时播放）。留空=该阶段不播放。")]
        public static string GiveKnifeTrack = "";

        [Config("递刀音乐音量。", Min = 0f, Max = 1f)]
        public static float GiveKnifeVolume = 1f;

        [Config("递刀音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int GiveKnifeMaxSeconds = -1;

        [Config("大厅音乐（主菜单与房内大厅播放）。留空=该阶段不播放。")]
        public static string LobbyTrack = "";

        [Config("大厅音乐音量。", Min = 0f, Max = 1f)]
        public static float LobbyVolume = 1f;

        [Config("大厅音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int LobbyMaxSeconds = -1;

        [Config("生存阶段音乐（进入生存阶段时播放）。留空=该阶段不播放。")]
        public static string SurviveTrack = "";

        [Config("生存阶段音乐音量。", Min = 0f, Max = 1f)]
        public static float SurviveVolume = 1f;

        [Config("生存阶段音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int SurviveMaxSeconds = -1;

        [Config("侦探阶段音乐（进入侦探阶段时播放）。留空=该阶段不播放。")]
        public static string DetectTrack = "";

        [Config("侦探阶段音乐音量。", Min = 0f, Max = 1f)]
        public static float DetectVolume = 1f;

        [Config("侦探阶段音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int DetectMaxSeconds = -1;

        [Config("开庭音乐（进入审判阶段时播放）。留空=该阶段不播放。")]
        public static string TrialTrack = "";

        [Config("开庭音乐音量。", Min = 0f, Max = 1f)]
        public static float TrialVolume = 1f;

        [Config("开庭音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int TrialMaxSeconds = -1;

        [Config("处刑音乐（审判处刑演出开始时播放）。留空=该阶段不播放。")]
        public static string ExecutionTrack = "";

        [Config("处刑音乐音量。", Min = 0f, Max = 1f)]
        public static float ExecutionVolume = 1f;

        [Config("处刑音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int ExecutionMaxSeconds = -1;

        [Config("胜利结算音乐（结算画面出现时播放）。留空=该阶段不播放。")]
        public static string VictoryTrack = "";

        [Config("胜利结算音乐音量。", Min = 0f, Max = 1f)]
        public static float VictoryVolume = 1f;

        [Config("胜利结算音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int VictoryMaxSeconds = -1;

        // ── 以下 9 项为 2026-09 审计新增阶段，默认全部留空（不影响原有八阶段） ──

        [Config("选角阶段音乐（进入角色选择弹窗时播放）。留空=该阶段不播放。")]
        public static string PickCharacterTrack = "";

        [Config("选角阶段音乐音量。", Min = 0f, Max = 1f)]
        public static float PickCharacterVolume = 1f;

        [Config("选角阶段音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int PickCharacterMaxSeconds = -1;

        [Config("审判·讨论子阶段音乐（Discuss，开庭发言与自由讨论）。留空=该阶段不播放。")]
        public static string DiscussTrack = "";

        [Config("讨论子阶段音乐音量。", Min = 0f, Max = 1f)]
        public static float DiscussVolume = 1f;

        [Config("讨论子阶段音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int DiscussMaxSeconds = -1;

        [Config("审判·投票子阶段音乐（VotePhase）。留空=该阶段不播放。")]
        public static string VotePhaseTrack = "";

        [Config("投票子阶段音乐音量。", Min = 0f, Max = 1f)]
        public static float VotePhaseVolume = 1f;

        [Config("投票子阶段音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int VotePhaseMaxSeconds = -1;

        [Config("审判·唱票子阶段音乐（VoteResult，票数揭晓）。留空=该阶段不播放。")]
        public static string VoteResultTrack = "";

        [Config("唱票子阶段音乐音量。", Min = 0f, Max = 1f)]
        public static float VoteResultVolume = 1f;

        [Config("唱票子阶段音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int VoteResultMaxSeconds = -1;

        [Config("审判·回放子阶段音乐（Replay，关键证据回放）。留空=该阶段不播放。")]
        public static string ReplayTrack = "";

        [Config("回放子阶段音乐音量。", Min = 0f, Max = 1f)]
        public static float ReplayVolume = 1f;

        [Config("回放子阶段音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int ReplayMaxSeconds = -1;

        [Config("发现尸体音乐（本机发现尸体时播放——服务端只发给发现者本人）。留空=该阶段不播放。")]
        public static string DiscoverCorpseTrack = "";

        [Config("发现尸体音乐音量。", Min = 0f, Max = 1f)]
        public static float DiscoverCorpseVolume = 1f;

        [Config("发现尸体音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int DiscoverCorpseMaxSeconds = -1;

        [Config("自己死亡音乐（本机玩家死亡瞬间，仅本人播放）。留空=该阶段不播放。")]
        public static string SelfDeadTrack = "";

        [Config("自己死亡音乐音量。", Min = 0f, Max = 1f)]
        public static float SelfDeadVolume = 1f;

        [Config("自己死亡音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int SelfDeadMaxSeconds = -1;

        [Config("结局动画音乐（电梯离场结局演出开始时播放）。留空=该阶段不播放。")]
        public static string EndingCutsceneTrack = "";

        [Config("结局动画音乐音量。", Min = 0f, Max = 1f)]
        public static float EndingCutsceneVolume = 1f;

        [Config("结局动画音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int EndingCutsceneMaxSeconds = -1;

        [Config("黑幕继承音乐（非递刀方式出现新黑幕时，黑幕侧本地播放；递刀交接由递刀音乐覆盖）。留空=该阶段不播放。")]
        public static string BlackSuccessionTrack = "";

        [Config("黑幕继承音乐音量。", Min = 0f, Max = 1f)]
        public static float BlackSuccessionVolume = 1f;

        [Config("黑幕继承音乐时长上限（秒）。-1 或 0=不限，到点自动停止。", Min = -1, Max = 600)]
        public static int BlackSuccessionMaxSeconds = -1;

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

        /// <summary>运行时关闭：停播并销毁 AudioSource 与音频缓存、复位混音状态（有持续音频/对象副作用必须清理）。</summary>
        private static void OnDisabled()
        {
            StageMusicPlayer.Shutdown();
        }
    }
}
