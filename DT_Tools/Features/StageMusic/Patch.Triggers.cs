using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.StageMusic
{
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

    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.UseHandWeapon))]
    internal static class StageMusicGiveKnifeTrigger
    {
        private static void Postfix(bool __result)
        {
            if (__result && Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.GiveKnife);
        }
    }

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
                    StageMusicPlayer.StopCurrent();
                    break;
            }
        }
    }

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
            }
        }
    }

    [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.TrialResult))]
    internal static class StageMusicExecutionTrigger
    {
        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.Execution);
        }
    }

    [HarmonyPatch(typeof(UI_LobbyScene), "OnEnable")]
    internal static class StageMusicLobbySceneTrigger
    {
        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.Lobby);
        }
    }

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

    [HarmonyPatch]
    internal static class StageMusicSelfDeadTrigger
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_DEAD");

        private static void Postfix()
        {
            if (Engine.Enabled<StageMusicFeature>())
                StageMusicPlayer.Play(MusicStage.SelfDead);
        }
    }

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

    [HarmonyPatch]
    internal static class StageMusicBlackSuccessionTrigger
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_NOTIFY_BLACK");

        private static void Postfix(Packet packet)
        {
            if (!Engine.Enabled<StageMusicFeature>())
                return;
            if (!(packet.Pkt is S_NOTIFY_BLACK pkt) || pkt.ByHand)
                return;
            var me = Managers.Player.MyPlayer;
            if (me == null || me.Color != EPlayerColor.Dark || !Managers.Game.IsAlive)
                return;
            StageMusicPlayer.Play(MusicStage.BlackSuccession);
        }
    }
}
