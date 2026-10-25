using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.HasungBoost
{
    [PatchFeature(
        "Hasung 越跑越快：Hasung 奔跑时累积冲刺能量，移速随消耗渐增（上限可配，停跑缓慢衰减；服务端，房主装生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerAbility | FeatureTag.PlayerMove,
        Author = "花语")]
    public sealed class HasungBoostFeature
    {
        [Config("冲刺能量上限（点）。", Min = 1, Max = 20)]
        public static int MaxBoostPoints = 5;

        [Config("每点能量对应的移速加成（SpeedDelta 档，原版奔跑速度 = 560 × (SpeedDelta + 0.3)）。", Min = 1, Max = 100)]
        public static int BoostPerPoint = 10;

        [Config("停止奔跑后每秒衰减的能量点数。", Min = 1, Max = 10)]
        public static int DecayPerSec = 1;
    }
}
