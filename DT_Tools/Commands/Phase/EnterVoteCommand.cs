using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.Phase
{
    internal sealed class EnterVoteCommand : ICommand
    {
        public string Name => "enter_vote";
        public string[] Aliases => new[] { "vote", "投票", "进入投票", "force_vote" };
        public string Usage => "enter_vote";
        public string Description => "强制进入裁判投票阶段（仅房主、讨论阶段可用；等效于讨论倒计时归零）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameVote;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx) => PhaseLogic.EnterVote(ctx);
    }
}
