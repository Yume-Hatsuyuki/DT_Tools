namespace DT_Tools.Console.Commands.Faction
{
    /// <summary>
    /// /list_white — 游戏中列出所有白方（EPlayerColor.White）。仅房主。
    /// </summary>
    internal sealed class ListWhiteCommand : IConsoleCommand
    {
        public string   Name        => "list_white";
        public string[] Aliases     => new[] { "white", "白方" };
        public string   Usage       => "list_white";
        public string   Description => "列出当前局所有白方（仅游戏中、需房主）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            FactionListHelper.Execute(console, Protocol.EPlayerColor.White, "白方");
        }
    }
}
