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
    /// 大厅/生存/侦探/开庭/结算五阶段；未配置曲目的阶段会停掉当前播放。
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
                    StageMusicPlayer.StopCurrent();   // 选角/空状态：未配置曲目，只保留一首
                    break;
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
}
