using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.SeolCarry
{
    [PatchFeature(
        "Seol 时停搬人：Seol 时停结束后技能键恢复，对准某人按技能键，把该玩家传送回 Seol 时停结束时的位置（服务端，房主装生效，Seol 无需装）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerAbility | FeatureTag.PlayerMove,
        Author = "花语")]
    public sealed class SeolCarryFeature
    {
        [Config("搬人传送延迟（毫秒，原版传送为 2000）。", Min = 0, Max = 10000)]
        public static int CarryDelayMs = 2000;

        [Config("搬人后 Seol 技能的冷却（秒，原版时停冷却为 60）。", Min = 5, Max = 120)]
        public static int CooldownSec = 60;
    }
}
