using DT_Tools.Commands;

namespace DT_Tools.Commands.Phase
{
    /// <summary>
    /// /enter_trial — 强制进入学级裁判。仅房主。
    /// </summary>
    internal sealed class EnterTrialCommand : ICommand
    {
        public string Name => "enter_trial";
        public string[] Aliases => new[] { "trial", "裁判", "学级裁判", "学籍裁判" };
        public string Usage => "enter_trial";
        public string Description => "强制进入学级裁判（调查阶段=跳过剩余调查时间；生存阶段=连调查一并跳过）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx) => PhaseLogic.EnterTrial(ctx);
    }
}
