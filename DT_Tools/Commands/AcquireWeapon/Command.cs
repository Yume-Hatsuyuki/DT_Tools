using DT_Tools.Commands;

namespace DT_Tools.Commands.AcquireWeapon
{
    /// <summary>
    /// /acquire_weapon [#armoryId] — 天匠：本机玩家无视距离从当前开放的武器架取得凶器
    /// （客户端发包，跟随控制台，非房主可用）。
    /// 无参数 → 列出全部武器架（只读，不发包）；带参数 → 从指定武器架拔刀。
    /// 机制、颜色分流与服务端门禁依据统一见 AcquireWeaponLogic（Logic.cs 头注释）。
    /// </summary>
    internal sealed class AcquireWeaponCommand : ICommand
    {
        public string Name => "acquire_weapon";
        public string[] Aliases => new[] { "天匠" };
        public string Usage => "acquire_weapon [#armoryId]";
        public string Description => "天匠：无参数列出武器架信息（含真伪可拔判定），带参数无视距离拔刀（对开放刀架生效；房主启用武器架多刀时全部开放架可拔）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            // ── 无参数：列出全部刀架信息 ──
            if (ctx.Args.Length == 0)
                return AcquireWeaponLogic.ListArmories(ctx);

            // ── 带参数：执行拔刀 ──
            return AcquireWeaponLogic.Acquire(ctx, ctx.Args[0]);
        }
    }
}
