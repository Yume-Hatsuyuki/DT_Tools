using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Patches.System.DetectivePhaseFix;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Phase
{
    /// <summary>
    /// 房主端强制跳转游戏阶段 / 裁判子状态 / 总结算的公共业务
    /// （对应旧 PhaseJumpHelper；房主门禁由框架 RequireHost 统一执行）。
    /// </summary>
    internal static class PhaseLogic
    {
        /// <summary>校验活动房间，失败返回 null 并已输出提示。</summary>
        private static GameRoom ValidateRoom(CommandContext ctx)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return null;
            }
            return room;
        }

        /// <summary>阶段切换屏障 / Host 迁移进行中时拒绝再次跳转。</summary>
        private static bool EnsureNotBusy(GameRoom room, CommandContext ctx)
        {
            if (room.IsTransitioning)
            {
                ctx.Reply("阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。");
                return false;
            }
            if (room.IsMigrating)
            {
                ctx.Reply("正在进行主机迁移，无法切换阶段。");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 强制进入调查阶段（Detective）。仅允许从生存阶段（Survive）进入；
        /// 有未处理尸体时按原版"时间到发现尸体"流程走。
        /// </summary>
        public static CommandResult EnterDetective(CommandContext ctx)
        {
            var room = ValidateRoom(ctx);
            if (room == null) return CommandResult.Fail("no room");
            if (!EnsureNotBusy(room, ctx)) return CommandResult.Fail("busy");

            switch (room.State)
            {
                case EGameState.Detective:
                    ctx.Reply("当前已经处于调查阶段（Detective）。");
                    return CommandResult.Fail("already detective");
                case EGameState.Survive:
                    break;
                default:
                    ctx.Reply($"只能从生存阶段（Survive）进入调查阶段，当前状态: {room.State}。");
                    return CommandResult.Fail("invalid state");
            }

            var corpse = room.FindOldestUndiscoveredCorpse();
            if (corpse != null)
            {
                ctx.Reply(
                    $"存在未处理尸体（#{corpse.DeviceInfo.DeviceId}），按原版“时间到发现尸体”流程进入调查阶段……");
                corpse.DiscoverByTimeOver();
                ctx.Reply("已触发调查阶段切换（等待全体客户端加载完成）。");
                return CommandResult.Success(new { via = "corpse", corpseId = corpse.DeviceInfo.DeviceId });
            }

            if (!DetectivePhaseFixFeature.IsApplied)
            {
                ctx.Warn(
                    "场上没有未处理尸体，直接进入调查阶段需要启用补丁 [StartDetective] " +
                    "（配置文件中 [StartDetective].Enabled=true，默认开启，修改后重启游戏）。");
                return CommandResult.Fail("patch not applied");
            }

            ctx.Warn("场上没有未处理尸体，将不设置凶手直接进入调查阶段（无录像带，投票将以凶手未被捕获结算）。");
            room.ChangeGameState(EGameState.Detective);
            ctx.Reply("已强制进入调查阶段（Detective，等待全体客户端加载完成）。");
            return CommandResult.Success(new { via = "force" });
        }

        /// <summary>强制进入学级裁判（Trial）。</summary>
        public static CommandResult EnterTrial(CommandContext ctx)
        {
            var room = ValidateRoom(ctx);
            if (room == null) return CommandResult.Fail("no room");
            if (!EnsureNotBusy(room, ctx)) return CommandResult.Fail("busy");

            switch (room.State)
            {
                case EGameState.Trial:
                    ctx.Reply("当前已经处于学级裁判（Trial）。");
                    return CommandResult.Fail("already trial");
                case EGameState.Detective:
                    break;
                case EGameState.Survive:
                    ctx.Warn("将从生存阶段直接进入学级裁判（跳过调查阶段，无尸体/录像带，投票将以凶手未被捕获结算）。");
                    break;
                default:
                    ctx.Reply(
                        $"只能从调查阶段（Detective）或生存阶段（Survive）进入学级裁判，当前状态: {room.State}。");
                    return CommandResult.Fail("invalid state");
            }

            var fromState = room.State;
            room.ChangeGameState(EGameState.Trial);
            ctx.Reply("已触发学级裁判切换（等待全体客户端加载完成）。");
            return CommandResult.Success(new { from = fromState.ToString() });
        }

        /// <summary>强制进入投票阶段（VotePhase）。仅 Discuss 可用。</summary>
        public static CommandResult EnterVote(CommandContext ctx)
        {
            var room = ValidateRoom(ctx);
            if (room == null) return CommandResult.Fail("no room");
            if (!EnsureNotBusy(room, ctx)) return CommandResult.Fail("busy");

            if (room.State != EGameState.Trial)
            {
                ctx.Reply($"当前不在学级裁判中（当前状态: {room.State}），无法进入投票阶段。");
                return CommandResult.Fail("not trial");
            }

            var trial = TrialManager.Instance;
            if (trial.State == ETrialState.VotePhase)
            {
                ctx.Reply("当前已经处于投票阶段（VotePhase）。若要立即开票请使用 /skip_vote。");
                return CommandResult.Fail("already vote");
            }

            if (trial.State != ETrialState.Discuss)
            {
                ctx.Reply($"当前裁判子状态为 {trial.State}，只能从讨论阶段（Discuss）强制进入投票。");
                return CommandResult.Fail("invalid trial state");
            }

            ctx.Reply("已强制进入投票阶段（等待全体客户端完成加载后开始 40 秒投票倒计时）。");
            trial.State = ETrialState.VotePhase;
            return CommandResult.Success(new { state = ETrialState.VotePhase.ToString() });
        }

        /// <summary>跳过投票阶段立即开票（VoteResult）。仅 VotePhase 可用。</summary>
        public static CommandResult SkipVote(CommandContext ctx)
        {
            var room = ValidateRoom(ctx);
            if (room == null) return CommandResult.Fail("no room");
            if (!EnsureNotBusy(room, ctx)) return CommandResult.Fail("busy");

            if (room.State != EGameState.Trial)
            {
                ctx.Reply($"当前不在学级裁判中（当前状态: {room.State}），无法跳过投票。");
                return CommandResult.Fail("not trial");
            }

            var trial = TrialManager.Instance;
            if (trial.State != ETrialState.VotePhase)
            {
                string hint = trial.State == ETrialState.Discuss
                    ? "当前为讨论阶段，请使用游戏内的“跳过讨论”表决，或先 /enter_vote。"
                    : $"投票阶段已结束（当前裁判子状态: {trial.State}），无法跳过。";
                ctx.Reply(hint);
                return CommandResult.Fail("invalid trial state");
            }

            ctx.Reply("已跳过投票阶段，立即开票（未投票玩家按弃权处理）。");
            trial.State = ETrialState.VoteResult;
            return CommandResult.Success(new { state = ETrialState.VoteResult.ToString() });
        }

        /// <summary>快速结束对局进入 TotalResult。</summary>
        public static CommandResult ForceEndGame(CommandContext ctx, EResultType result)
        {
            var room = ValidateRoom(ctx);
            if (room == null) return CommandResult.Fail("no room");
            if (!EnsureNotBusy(room, ctx)) return CommandResult.Fail("busy");

            switch (room.State)
            {
                case EGameState.Survive:
                case EGameState.Detective:
                case EGameState.Trial:
                    break;
                case EGameState.TotalResult:
                    ctx.Reply("当前已经处于总结算（TotalResult）。");
                    return CommandResult.Fail("already total");
                default:
                    ctx.Reply($"只能在对局进行中（Survive / Detective / Trial）使用，当前状态: {room.State}。");
                    return CommandResult.Fail("invalid state");
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

            ctx.Reply(
                $"已强制结束对局（{result}），正在进入总结算（等待全体客户端加载完成）……");
            room.ChangeGameState(EGameState.TotalResult);
            return CommandResult.Success(new { result = result.ToString(), primaryWinnerId = room.PrimaryWinnerId });
        }
    }
}
