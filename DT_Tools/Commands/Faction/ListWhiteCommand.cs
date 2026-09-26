using DT_Tools.Commands;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    /// <summary>
    /// /list_white — 游戏中列出所有白方（EPlayerColor.White）。仅房主。
    /// </summary>
    internal sealed class ListWhiteCommand : ICommand
    {
        public string Name => "list_white";
        public string[] Aliases => new[] { "white", "白方" };
        public string Usage => "list_white";
        public string Description => "列出当前局所有白方（仅游戏中、需房主）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
            => FactionLogic.Execute(ctx, EPlayerColor.White, "白方");
    }
}
