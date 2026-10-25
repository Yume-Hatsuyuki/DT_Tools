using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.Power
{
    internal sealed class RestorePowerCommand : ICommand
    {
        public string Name => "restore_power";
        public string[] Aliases => new[] { "power_on", "开灯", "来电", "复电", "电力恢复", "恢复电力" };
        public string Usage => "restore_power";
        public string Description => "强制恢复全场电力（等效所有损坏电箱被修完，需房主·生存阶段）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameFuse;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx) => PowerLogic.RestorePower(ctx);
    }
}
