using DT_Tools.Commands;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    /// <summary>
    /// /list_dark — 游戏中列出黑幕（EPlayerColor.Dark）。仅房主。
    /// </summary>
    internal sealed class ListDarkCommand : ICommand
    {
        public string Name => "list_dark";
        public string[] Aliases => new[] { "dark", "mastermind", "黑幕" };
        public string Usage => "list_dark";
        public string Description => "列出当前局黑幕（仅游戏中、需房主）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
            => FactionLogic.Execute(ctx, EPlayerColor.Dark, "黑幕");
    }
}
