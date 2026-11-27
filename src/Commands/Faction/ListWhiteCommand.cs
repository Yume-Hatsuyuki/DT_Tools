// 作用：实现 /list_white 命令的参数解析与执行；不负责命令注册与其他命令。
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    internal sealed class ListWhiteCommand : ICommand
    {
        public string Name => "list_white";
        public string[] Aliases => new[] { "white", "白方" };
        public string Usage => "list_white";
        public string Description => "列出当前局所有白方（仅游戏中、需房主）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameIntel;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
            => FactionLogic.Execute(ctx, EPlayerColor.White, "白方");
    }
}
