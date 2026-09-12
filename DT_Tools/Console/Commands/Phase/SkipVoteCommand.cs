using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands.Phase
{
    /// <summary>
    /// /skip_vote
    ///
    /// 跳过学级裁判的投票阶段（ETrialState.VotePhase），立即开票。仅房主、
    /// 且裁判处于投票阶段时可用。
    ///
    /// 实现与原版“40 秒投票倒计时归零”完全等效：
    ///   TickVotePhase 在 Time &lt;= 0 时执行 State = ETrialState.VoteResult，
    ///   setter 内部广播 S_TRIAL_STATE(VoteResult) → StartVoteResult() 汇总
    ///   _candidates 当前票数广播 S_RESULT_VOTE → 有录像带进 Replay，否则直进
    ///   TrialResult。因此跳过前已投出的票照常计入，未投的玩家按弃权处理
    ///   （与自然超时一致），后续开票/真相公开/处刑结算流程不受影响。
    ///
    /// 注意：讨论阶段（Discuss）不适用——原版有正式的“全员跳过讨论”表决
    /// 机制（游戏内按钮）；开票之后的阶段也无法用本命令回退。
    ///
    /// 示例:
    ///   /skip_vote
    /// </summary>
    internal sealed class SkipVoteCommand : IConsoleCommand
    {
        public string   Name        => "skip_vote";
        public string[] Aliases     => new[] { "skipvote", "跳过投票" };
        public string   Usage       => "skip_vote";
        public string   Description => "跳过裁判投票阶段立即开票（仅房主、投票阶段可用；已投票数保留，未投按弃权）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            var room = PhaseJumpHelper.ValidateRoom(console);
            if (room == null) return;
            if (!PhaseJumpHelper.EnsureNotBusy(room, console)) return;

            if (room.State != EGameState.Trial)
            {
                console.Log($"当前不在学级裁判中（当前状态: {room.State}），无法跳过投票。", LogLevel.Warning);
                return;
            }

            var trial = TrialManager.Instance;
            if (trial.State != ETrialState.VotePhase)
            {
                string hint = trial.State == ETrialState.Discuss
                    ? "当前为讨论阶段，请使用游戏内的“跳过讨论”表决。"
                    : $"投票阶段已结束（当前裁判子状态: {trial.State}），无法跳过。";
                console.Log(hint, LogLevel.Warning);
                return;
            }

            // 与 40 秒倒计时归零同一路径：setter 广播 S_TRIAL_STATE 并进入 StartVoteResult
            console.Log("已跳过投票阶段，立即开票（未投票玩家按弃权处理）。", LogLevel.Message);
            trial.State = ETrialState.VoteResult;
        }
    }
}
