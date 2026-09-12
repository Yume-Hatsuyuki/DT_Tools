namespace DT_Tools.Console.Commands.Phase
{
    /// <summary>
    /// /enter_detective
    ///
    /// 强制进入调查阶段（EGameState.Detective）。仅房主、生存阶段（Survive）可用。
    ///
    /// 内部调用：
    ///   - 场上存在未处理尸体时：Corpse.DiscoverByTimeOver()，与原版“生存倒计时
    ///     归零发现尸体”完全同一路径（取证固化、尸体浮出、TrialManager.Init、
    ///     ChangeGameState 屏障切换）。
    ///   - 没有尸体时：不设置任何凶手，直接 ChangeGameState 裸进。需要 [StartDetective]
    ///     补丁（默认开启）修复原版 StartDetective 开头 black.IsAlive 的空引用；
    ///     本次裁判没有尸体与录像带，投票无人可投，结果按凶手未被捕获结算。
    ///
    /// 示例:
    ///   /enter_detective
    /// </summary>
    internal sealed class EnterDetectiveCommand : IConsoleCommand
    {
        public string   Name        => "enter_detective";
        public string[] Aliases     => new[] { "detective", "调查", "进入调查" };
        public string   Usage       => "enter_detective";
        public string   Description => "强制进入调查阶段（仅房主、生存阶段可用；有尸体时走原版发现流程）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            PhaseJumpHelper.JumpToDetective(console);
        }
    }
}
