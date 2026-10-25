using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.Power
{
    internal sealed class BlackoutCommand : ICommand
    {
        public string Name => "blackout";
        public string[] Aliases => new[] { "关灯", "停电", "断电", "熄灯" };
        public string Usage => "blackout";
        public string Description => "强制触发全场停电（等效 Dark 断开第 2 个电箱，需房主·生存阶段）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameFuse;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx) => PowerLogic.Blackout(ctx);
    }
}
