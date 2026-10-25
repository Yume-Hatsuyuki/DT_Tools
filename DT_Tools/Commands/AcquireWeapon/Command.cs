using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.AcquireWeapon
{
    internal sealed class AcquireWeaponCommand : ICommand
    {
        public string Name => "acquire_weapon";
        public string[] Aliases => new[] { "天匠" };
        public string Usage => "acquire_weapon [#armoryId]";
        public string Description => "天匠：无参数列出武器架信息（含真伪可拔判定），带参数无视距离拔刀（对开放刀架生效；房主启用武器架多刀时全部开放架可拔）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameWeapon;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
                return AcquireWeaponLogic.ListArmories(ctx);

            return AcquireWeaponLogic.Acquire(ctx, ctx.Args[0]);
        }
    }
}
