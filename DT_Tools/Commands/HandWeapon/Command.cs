using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.HandWeapon
{
    internal sealed class HandWeaponCommand : ICommand
    {
        public string Name => "hand_weapon";
        public string[] Aliases => new[] { "营图" };
        public string Usage => "hand_weapon <#playerId>";
        public string Description => "营图：黑幕无视距离把凶器交给指定 White（仅单体目标，一把刀只能递一次）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameWeapon;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx) => HandWeaponLogic.Execute(ctx);
    }
}
