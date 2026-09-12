namespace DT_Tools.Console.Commands.Phase
{
    /// <summary>
    /// /enter_trial
    ///
    /// 强制进入学级裁判（EGameState.Trial）。仅房主可用。
    ///
    ///   - 调查阶段（Detective）执行：等效于调查倒计时立即归零，直接开庭。
    ///   - 生存阶段（Survive）执行：连调查阶段一并跳过。无需任何凶手数据——原版裁判
    ///     链路对 Black==null 全程有保护，最终按凶手未被捕获（黑方胜）结算。
    ///
    /// 开庭后原版子状态机照常推进：
    ///   Discuss → VotePhase → VoteResult →（Replay）→ TrialResult → TotalResult
    ///
    /// 示例:
    ///   /enter_trial
    /// </summary>
    internal sealed class EnterTrialCommand : IConsoleCommand
    {
        public string   Name        => "enter_trial";
        public string[] Aliases     => new[] { "trial", "裁判", "学级裁判", "学籍裁判" };
        public string   Usage       => "enter_trial";
        public string   Description => "强制进入学级裁判（调查阶段=跳过剩余调查时间；生存阶段=连调查一并跳过）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            PhaseJumpHelper.JumpToTrial(console);
        }
    }
}
