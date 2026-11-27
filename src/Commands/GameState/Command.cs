// 作用：实现 /game_state 命令的参数解析与执行；不负责命令注册与其他命令。
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.GameState
{
    internal sealed class GameStateCommand : ICommand
    {
        public string Name => "game_state";
        public string[] Aliases => new[] { "state", "game_phase", "状态", "阶段" };
        public string Usage => "game_state";
        public string Description => "查看当前游戏状态（裁判中含子阶段），房主/客户端均可用。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameRound;

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
