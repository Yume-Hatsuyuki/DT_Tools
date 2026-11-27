// 作用：实现 /list_dark 命令的参数解析与执行；不负责命令注册与其他命令。
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    internal sealed class ListDarkCommand : ICommand
    {
        public string Name => "list_dark";
        public string[] Aliases => new[] { "dark", "mastermind", "黑幕" };
        public string Usage => "list_dark";
        public string Description => "列出当前局黑幕（仅游戏中、需房主）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameIntel;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
            => FactionLogic.Execute(ctx, EPlayerColor.Dark, "黑幕");
    }
}
