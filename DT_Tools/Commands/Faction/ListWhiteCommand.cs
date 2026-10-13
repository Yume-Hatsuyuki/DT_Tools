using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using Protocol;

namespace DT_Tools.Commands.Faction
{
    /// <summary>
    /// /list_white — 游戏中列出所有白方（EPlayerColor.White）。
    /// </summary>
    /// <remarks>
    /// 仅房主。
    /// </remarks>
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
