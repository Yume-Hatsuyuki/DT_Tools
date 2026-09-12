namespace DT_Tools.Console.Commands.Faction
{
    /// <summary>
    /// /list_black — 游戏中列出所有黑方（EPlayerColor.Black）。仅房主。
    /// </summary>
    internal sealed class ListBlackCommand : IConsoleCommand
    {
        public string   Name        => "list_black";
        public string[] Aliases     => new[] { "black", "黑方" };
        public string   Usage       => "list_black";
        public string   Description => "列出当前局所有黑方（仅游戏中、需房主）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            FactionListHelper.Execute(console, Protocol.EPlayerColor.Black, "黑方");
        }
    }
}
