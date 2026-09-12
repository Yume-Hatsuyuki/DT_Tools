namespace DT_Tools.Console.Commands.Faction
{
    /// <summary>
    /// /list_dark — 游戏中列出黑幕（EPlayerColor.Dark）。仅房主。
    /// </summary>
    internal sealed class ListDarkCommand : IConsoleCommand
    {
        public string   Name        => "list_dark";
        public string[] Aliases     => new[] { "dark", "mastermind", "黑幕" };
        public string   Usage       => "list_dark";
        public string   Description => "列出当前局黑幕（仅游戏中、需房主）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            FactionListHelper.Execute(console, Protocol.EPlayerColor.Dark, "黑幕");
        }
    }
}
