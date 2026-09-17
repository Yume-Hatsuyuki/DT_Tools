namespace DT_Tools.Console.Commands.Phase
{
    /// <summary>
    /// /enter_vote — 强制进入投票阶段（Discuss → VotePhase）。仅房主。
    /// </summary>
    internal sealed class EnterVoteCommand : IConsoleCommand
    {
        public string   Name        => "enter_vote";
        public string[] Aliases     => new[] { "vote", "投票", "进入投票", "force_vote" };
        public string   Usage       => "enter_vote";
        public string   Description => "强制进入裁判投票阶段（仅房主、讨论阶段可用；等效于讨论倒计时归零）。";
        public string   Author      => "梦初雪";

        public bool RequireHost => true;

        public void Execute(string[] args, WebConsole console)
        {
            PhaseJumpHelper.JumpToVote(console);
        }
    }
}
