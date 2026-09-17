using System.Linq;
using BepInEx.Logging;
using DT_Tools.Features.System;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands.Phase
{
    /// <summary>
    /// 房主端强制跳转游戏阶段 / 裁判子状态的公共校验与入口。
    /// </summary>
    internal static class PhaseJumpHelper
    {
        /// <summary>校验房主与活动房间，失败返回 null 并已输出提示。</summary>
        public static GameRoom ValidateRoom(WebConsole console)
        {
            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行（阶段切换逻辑仅在 Host 端）。", LogLevel.Warning);
                return null;
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                console.Log("当前没有活动的游戏房间。", LogLevel.Warning);
                return null;
            }

            return room;
        }

        /// <summary>阶段切换屏障 / Host 迁移进行中时拒绝再次跳转。</summary>
        public static bool EnsureNotBusy(GameRoom room, WebConsole console)
        {
            if (room.IsTransitioning)
            {
                console.Log("阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。", LogLevel.Warning);
                return false;
            }

            if (room.IsMigrating)
            {
                console.Log("正在进行主机迁移，无法切换阶段。", LogLevel.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 校验房间 + 非忙，并确认处于指定 EGameState 之一。
        /// 失败返回 null（已写日志）。
        /// </summary>
        public static GameRoom RequireState(WebConsole console, params EGameState[] allowed)
        {
            var room = ValidateRoom(console);
            if (room == null) return null;
            if (!EnsureNotBusy(room, console)) return null;

            if (allowed != null && allowed.Length > 0)
            {
                for (int i = 0; i < allowed.Length; i++)
                {
                    if (room.State == allowed[i])
                        return room;
                }

                string names = string.Join(" / ", allowed);
                console.Log($"当前状态不允许此操作（需要: {names}，当前: {room.State}）。", LogLevel.Warning);
                return null;
            }

            return room;
        }

        /// <summary>
        /// 要求处于学级裁判且裁判子状态为 expected。
        /// 成功返回 TrialManager.Instance，否则 null（已写日志）。
        /// </summary>
        public static TrialManager RequireTrialState(WebConsole console, ETrialState expected, string failHint = null)
        {
            var room = RequireState(console, EGameState.Trial);
            if (room == null) return null;

            var trial = TrialManager.Instance;
            if (trial.State != expected)
            {
                console.Log(
                    failHint ?? $"当前裁判子状态为 {trial.State}，需要 {expected}。",
                    LogLevel.Warning);
                return null;
            }

            return trial;
        }

        /// <summary>
        /// 强制进入调查阶段（Detective）。仅允许从生存阶段（Survive）进入。
        /// </summary>
        public static void JumpToDetective(WebConsole console)
        {
            var room = ValidateRoom(console);
            if (room == null) return;
            if (!EnsureNotBusy(room, console)) return;

            switch (room.State)
            {
                case EGameState.Detective:
                    console.Log("当前已经处于调查阶段（Detective）。", LogLevel.Warning);
                    return;
                case EGameState.Survive:
                    break;
                default:
                    console.Log($"只能从生存阶段（Survive）进入调查阶段，当前状态: {room.State}。", LogLevel.Warning);
                    return;
            }

            var corpse = room.FindOldestUndiscoveredCorpse();
            if (corpse != null)
            {
                console.Log(
                    $"存在未处理尸体（#{corpse.DeviceInfo.DeviceId}），按原版“时间到发现尸体”流程进入调查阶段……",
                    LogLevel.Message);
                corpse.DiscoverByTimeOver();
                console.Log("已触发调查阶段切换（等待全体客户端加载完成）。", LogLevel.Message);
                return;
            }

            if (!DetectivePhaseFixFeature.IsApplied)
            {
                console.Log(
                    "场上没有未处理尸体，直接进入调查阶段需要启用补丁 [StartDetective] " +
                    "（配置文件中 [StartDetective].Enabled=true，默认开启，修改后重启游戏）。",
                    LogLevel.Warning);
                return;
            }

            console.Log("场上没有未处理尸体，将不设置凶手直接进入调查阶段（无录像带，投票将以凶手未被捕获结算）。", LogLevel.Warning);
            room.ChangeGameState(EGameState.Detective);
            console.Log("已强制进入调查阶段（Detective，等待全体客户端加载完成）。", LogLevel.Message);
        }

        /// <summary>
        /// 强制进入学级裁判（Trial）。
        /// </summary>
        public static void JumpToTrial(WebConsole console)
        {
            var room = ValidateRoom(console);
            if (room == null) return;
            if (!EnsureNotBusy(room, console)) return;

            switch (room.State)
            {
                case EGameState.Trial:
                    console.Log("当前已经处于学级裁判（Trial）。", LogLevel.Warning);
                    return;
                case EGameState.Detective:
                    break;
                case EGameState.Survive:
                    console.Log("将从生存阶段直接进入学级裁判（跳过调查阶段，无尸体/录像带，投票将以凶手未被捕获结算）。", LogLevel.Warning);
                    break;
                default:
                    console.Log(
                        $"只能从调查阶段（Detective）或生存阶段（Survive）进入学级裁判，当前状态: {room.State}。",
                        LogLevel.Warning);
                    return;
            }

            room.ChangeGameState(EGameState.Trial);
            console.Log("已触发学级裁判切换（等待全体客户端加载完成）。", LogLevel.Message);
        }

        /// <summary>
        /// 强制进入投票阶段（VotePhase）。仅 Discuss 可用。
        /// </summary>
        public static void JumpToVote(WebConsole console)
        {
            var room = ValidateRoom(console);
            if (room == null) return;
            if (!EnsureNotBusy(room, console)) return;

            if (room.State != EGameState.Trial)
            {
                console.Log($"当前不在学级裁判中（当前状态: {room.State}），无法进入投票阶段。", LogLevel.Warning);
                return;
            }

            var trial = TrialManager.Instance;
            if (trial.State == ETrialState.VotePhase)
            {
                console.Log("当前已经处于投票阶段（VotePhase）。若要立即开票请使用 /skip_vote。", LogLevel.Warning);
                return;
            }

            if (trial.State != ETrialState.Discuss)
            {
                console.Log($"当前裁判子状态为 {trial.State}，只能从讨论阶段（Discuss）强制进入投票。", LogLevel.Warning);
                return;
            }

            console.Log("已强制进入投票阶段（等待全体客户端完成加载后开始 40 秒投票倒计时）。", LogLevel.Message);
            trial.State = ETrialState.VotePhase;
        }

        /// <summary>
        /// 跳过投票阶段立即开票（VoteResult）。仅 VotePhase 可用。
        /// </summary>
        public static void SkipVote(WebConsole console)
        {
            var room = ValidateRoom(console);
            if (room == null) return;
            if (!EnsureNotBusy(room, console)) return;

            if (room.State != EGameState.Trial)
            {
                console.Log($"当前不在学级裁判中（当前状态: {room.State}），无法跳过投票。", LogLevel.Warning);
                return;
            }

            var trial = TrialManager.Instance;
            if (trial.State != ETrialState.VotePhase)
            {
                string hint = trial.State == ETrialState.Discuss
                    ? "当前为讨论阶段，请使用游戏内的“跳过讨论”表决，或先 /enter_vote。"
                    : $"投票阶段已结束（当前裁判子状态: {trial.State}），无法跳过。";
                console.Log(hint, LogLevel.Warning);
                return;
            }

            console.Log("已跳过投票阶段，立即开票（未投票玩家按弃权处理）。", LogLevel.Message);
            trial.State = ETrialState.VoteResult;
        }

        /// <summary>
        /// 快速结束对局进入 TotalResult。
        /// </summary>
        public static void ForceEndGame(WebConsole console, EResultType result)
        {
            var room = ValidateRoom(console);
            if (room == null) return;
            if (!EnsureNotBusy(room, console)) return;

            switch (room.State)
            {
                case EGameState.Survive:
                case EGameState.Detective:
                case EGameState.Trial:
                    break;
                case EGameState.TotalResult:
                    console.Log("当前已经处于总结算（TotalResult）。", LogLevel.Warning);
                    return;
                default:
                    console.Log($"只能在对局进行中（Survive / Detective / Trial）使用，当前状态: {room.State}。", LogLevel.Warning);
                    return;
            }

            room.ResultType = result;
            room.PrimaryWinnerId = result == EResultType.BlackWin
                ? (room.MasterMind?.PublicInfo.PlayerId ?? 0)
                : 0;

            room.ApplyTeamResults();

            if (result == EResultType.BlackWin)
            {
                foreach (var item in room.AlivePlayers.ToList())
                {
                    if (item.Color != EPlayerColor.Black && item.Color != EPlayerColor.Dark)
                        item.Session.Send(new S_STOP_CONTROL());
                }
            }

            console.Log(
                $"已强制结束对局（{result}），正在进入总结算（等待全体客户端加载完成）……",
                LogLevel.Message);
            room.ChangeGameState(EGameState.TotalResult);
        }
    }
}
