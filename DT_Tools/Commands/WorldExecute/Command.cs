using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.WorldExecute
{
    internal sealed class WorldExecuteCommand : ICommand
    {
        public string Name => "world.execute";
        public string[] Aliases => new[] { "自杀", "自尽", "自刎归天", "execute" };
        public string Usage => "world.execute me";
        public string Description => "黑方自刀：/world.execute me 以 Murder 路径结束自己（仅持刀 Black · Survive）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameKill;
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx) => WorldExecuteLogic.Execute(ctx);
    }
}
