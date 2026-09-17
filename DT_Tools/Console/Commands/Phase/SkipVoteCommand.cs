namespace DT_Tools.Console.Commands.Phase
{
    /// <summary>
    /// /skip_vote — 跳过投票阶段立即开票。仅房主、VotePhase。
    /// </summary>
    internal sealed class SkipVoteCommand : IConsoleCommand
    {
        public string   Name        => "skip_vote";
        public string[] Aliases     => new[] { "skipvote", "跳过投票" };
        public string   Usage       => "skip_vote";
        public string   Description => "跳过裁判投票阶段立即开票（仅房主、投票阶段可用；已投票数保留，未投按弃权）。";
        public string   Author      => "梦初雪";

        public bool RequireHost => true;

        public void Execute(string[] args, WebConsole console)
        {
            PhaseJumpHelper.SkipVote(console);
        }
    }
}
