using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using NAudio.Wave;
using Protocol;

namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 击杀触发：Handle_S_KILL_PLAYER 是"本机击杀了人"的权威信号（服务端只发凶手，
    /// 0.1.15b DeviceManager.cs:588-609）。PacketHandler 为 internal（PacketHandler.cs:9），
    /// TargetMethod 运行时解析。
    /// </summary>
    [HarmonyPatch]
    internal static class StageMusicKillTrigger
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_KILL_PLAYER");

        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.Kill);
        }
    }

    /// <summary>
    /// 递刀触发（黑幕侧）：UseHandWeapon 发包成功（return true，0.1.15b MyPlayer.cs:779）
    /// 即黑幕把刀递出，黑幕本地播放递刀音乐。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.UseHandWeapon))]
    internal static class StageMusicGiveKnifeTrigger
    {
        private static void Postfix(bool __result)
        {
            if (__result && Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.GiveKnife);
        }
    }

    /// <summary>
    /// 阶段切换总触发：StartState（私有，0.1.15b GameManagerEX.cs:385）在每次
    /// State 变化时被调用且各 Start* 子方法已执行完——一个补丁覆盖
    /// 大厅/选角/生存/侦探/开庭/结算六阶段；未配置曲目的阶段会停掉当前播放。
    /// </summary>
    [HarmonyPatch(typeof(GameManagerEX), "StartState")]
    internal static class StageMusicStateTrigger
    {
        private static void Postfix(EGameState state)
        {
            if (!Engine.Enabled<StageMusicFeature>())
                return;

            switch (state)
            {
                case EGameState.Lobby:
                    StageMusicPlayer.Play(MusicStage.Lobby);
                    break;
                case EGameState.PickCharacter:
                    StageMusicPlayer.Play(MusicStage.PickCharacter);
                    break;
                case EGameState.Survive:
                    StageMusicPlayer.Play(MusicStage.Survive);
                    break;
                case EGameState.Detective:
                    StageMusicPlayer.Play(MusicStage.Detective);
                    break;
                case EGameState.Trial:
                    StageMusicPlayer.Play(MusicStage.Trial);
                    break;
                case EGameState.TotalResult:
                    StageMusicPlayer.Play(MusicStage.Victory);
                    break;
                default:
                    StageMusicPlayer.StopCurrent();   // NoneState 等空状态：未配置曲目，只保留一首
                    break;
            }
        }
    }

    /// <summary>
    /// 审判子阶段触发：UI_TrialEvent.StartState（私有，0.1.15b UI_TrialEvent.cs:1652）在
    /// 审判内部 Discuss→VotePhase→VoteResult→Replay→TrialResult 每次切换时调用。
    /// 只覆盖前四个子阶段——TrialResult 子阶段沿用 StageMusicExecutionTrigger（更精确，
    /// 挂在裁决数据到达的 TrialResult 方法上，而非仅仅状态切换的瞬间），避免同一时机
    /// 两个触发器都响应造成重复判断。
    /// </summary>
    [HarmonyPatch(typeof(UI_TrialEvent), "StartState")]
    internal static class StageMusicTrialStateTrigger
    {
        private static void Postfix(ETrialState state)
        {
            if (!Engine.Enabled<StageMusicFeature>())
                return;

            switch (state)
            {
                case ETrialState.Discuss:
                    StageMusicPlayer.Play(MusicStage.Discuss);
                    break;
                case ETrialState.VotePhase:
                    StageMusicPlayer.Play(MusicStage.VotePhase);
                    break;
                case ETrialState.VoteResult:
                    StageMusicPlayer.Play(MusicStage.VoteResult);
                    break;
                case ETrialState.Replay:
                    StageMusicPlayer.Play(MusicStage.Replay);
                    break;
                    // ETrialState.TrialResult: 故意不处理，见上方注释。
            }
        }
    }

    /// <summary>
    /// 处刑触发（审判子演出）：TrialResult 为处刑演出序列起点
    /// （0.1.15b UI_TrialEvent.cs:1214，public）。
    /// </summary>
    [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.TrialResult))]
    internal static class StageMusicExecutionTrigger
    {
        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.Execution);
        }
    }

    /// <summary>
    /// 主菜单大厅触发：UI_LobbyScene.OnEnable 播 MainTitleBGM（0.1.15b UI_LobbyScene.cs:1961），
    /// 同一曲目与房内大厅互为去重。
    /// </summary>
    [HarmonyPatch(typeof(UI_LobbyScene), "OnEnable")]
    internal static class StageMusicLobbySceneTrigger
    {
        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.Lobby);
        }
    }

    /// <summary>
    /// 发现尸体触发：Handle_S_DISCOVER_CORPSE 是全员广播（举报/发现尸体时服务端群发，
    /// 0.1.15b PacketHandler.cs:820）。PacketHandler 为 internal，TargetMethod 运行时解析。
    /// </summary>
    [HarmonyPatch]
    internal static class StageMusicDiscoverCorpseTrigger
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_DISCOVER_CORPSE");

        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.DiscoverCorpse);
        }
    }

    /// <summary>
    /// 自己死亡触发：Handle_S_NOTIFY_DEAD 只发给刚死亡的受害者本人
    /// （驱动 UI_SoulSence 出窍动画，0.1.15b PacketHandler.cs:585）。
    /// </summary>
    [HarmonyPatch]
    internal static class StageMusicSelfDeadTrigger
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_NOTIFY_DEAD");

        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.SelfDead);
        }
    }

    /// <summary>
    /// 结局动画触发：Handle_S_ENDING_CAMERA 驱动电梯离场结局镜头，s_ENDING_CAMERA.IsEnd
    /// 区分"进入结局镜头"(true) 与"结局镜头结束"(false)，只在 true 时触发
    /// （0.1.15b PacketHandler.cs:591，S_ENDING_CAMERA.cs:28）。
    /// </summary>
    [HarmonyPatch]
    internal static class StageMusicEndingCutsceneTrigger
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_ENDING_CAMERA");

        private static void Postfix(Packet packet)
        {
            if (!Engine.Enabled<StageMusicFeature>())
                return;
            if (packet.Pkt is S_ENDING_CAMERA pkt && pkt.IsEnd)
                StageMusicPlayer.Play(MusicStage.EndingCutscene);
        }
    }

    /// <summary>
    /// 黑幕继承触发：Handle_S_NOTIFY_BLACK 通知"你已成为黑幕"，ByHand 区分来源——
    /// true=递刀交接（已由 StageMusicGiveKnifeTrigger 覆盖，此处跳过避免重复触发）；
    /// false=原黑幕死亡后系统指定继承（当前无专属触发，本补丁覆盖此情形）。
    /// 只在通知的 PlayerId 是本机玩家时触发（0.1.15b PacketHandler.cs:481，
    /// S_NOTIFY_BLACK.cs:32,45）。
    /// </summary>
    [HarmonyPatch]
    internal static class StageMusicBlackSuccessionTrigger
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_NOTIFY_BLACK");

        private static void Postfix(Packet packet)
        {
            if (!Engine.Enabled<StageMusicFeature>())
                return;
            var me = Managers.Player.MyPlayer;
            if (me == null)
                return;
            if (packet.Pkt is S_NOTIFY_BLACK pkt
                && !pkt.ByHand
                && pkt.PlayerId == me.PublicInfo.PlayerId)
            {
                StageMusicPlayer.Play(MusicStage.BlackSuccession);
            }
        }
    }
}
