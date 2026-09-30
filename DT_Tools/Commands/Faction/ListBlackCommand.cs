using DT_Tools.Commands;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    /// <summary>
    /// /list_black — 游戏中列出所有黑方（EPlayerColor.Black）。仅房主。
    /// </summary>
    internal sealed class ListBlackCommand : ICommand
    {
        public string Name => "list_black";
        public string[] Aliases => new[] { "black", "黑方" };
        public string Usage => "list_black";
        public string Description => "列出当前局所有黑方（仅游戏中、需房主）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
            => FactionLogic.Execute(ctx, EPlayerColor.Black, "黑方");
    }
}
