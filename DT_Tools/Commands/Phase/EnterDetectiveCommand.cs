using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.Phase
{
    /// /enter_detective — 强制进入调查阶段。仅房主、Survive。
    internal sealed class EnterDetectiveCommand : ICommand
    {
        public string Name => "enter_detective";
        public string[] Aliases => new[] { "detective", "调查", "进入调查" };
        public string Usage => "enter_detective";
        public string Description => "强制进入调查阶段（仅房主、生存阶段可用；有尸体时走原版发现流程）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameRound;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx) => PhaseLogic.EnterDetective(ctx);
    }
}
