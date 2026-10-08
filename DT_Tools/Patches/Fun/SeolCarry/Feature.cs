using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.SeolCarry
{
    /// <summary>
    /// Seol 时停搬人：时停（TheWorld，5000ms）结束瞬间记录 Seol 的位置 P，并把 Seol 的
    /// 技能键恢复可用（绕过冷却）；Seol 对准任意玩家按下技能键（C_USE_SKILL{TargetId}），
    /// 服务端拦截本次技能（不触发原时停），改为给目标玩家加传送流程（EBuffType.Stop +
    /// Casting + 2 秒后强制 Move 到 P）——取巧实现"搬人"：把目标传送到 Seol 时停结束时的位置。
    /// 全程服务端逻辑（房主进程），Seol 无需装 mod；搬人后 Seol 技能进入原 60 秒冷却。
    /// </summary>
    [PatchFeature(
        "Seol 时停搬人：Seol 时停结束后技能键恢复，对准某人按技能键，把该玩家传送回 Seol 时停结束时的位置（服务端，房主装生效，Seol 无需装）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "花语")]
    public sealed class SeolCarryFeature
    {
        [Config("搬人传送延迟（毫秒，原版传送为 2000）。", Min = 0, Max = 10000)]
        public static int CarryDelayMs = 2000;

        [Config("搬人后 Seol 技能的冷却（秒，原版时停冷却为 60）。", Min = 5, Max = 120)]
        public static int CooldownSec = 60;
    }
}
