using DT_Tools.Commands;

namespace DT_Tools.Commands.GameState
{
    /// <summary>
    /// /game_state — 查询当前游戏主状态机 EGameState（大厅 / 选角 / 生存 / 调查 /
    /// 学级裁判 / 总结算）；处于学级裁判时附加裁判子状态机 ETrialState
    /// （讨论 / 投票 / 开票 / 真相公开 / 审判结果）。纯读命令，房主与普通客户端均可执行。
    /// </summary>
    internal sealed class GameStateCommand : ICommand
    {
        public string Name => "game_state";
        public string[] Aliases => new[] { "state", "game_phase", "状态", "阶段" };
        public string Usage => "game_state";
        public string Description => "查看当前游戏状态（裁判中含子阶段），房主/客户端均可用。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!GameStateLogic.TryRead(out var result, out string code, out string text))
            {
                ctx.Reply(text);
                return CommandResult.Fail(code);
            }

            ctx.Reply(GameStateFormat.Reply(result));
            return CommandResult.Success(GameStateFormat.Result(result));
        }
    }
}
