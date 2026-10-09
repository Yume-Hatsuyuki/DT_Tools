using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.HasungBoost
{
    /// <summary>
    /// Hasung 移速强化——"越跑越快"：Hasung 奔跑（消耗体力）时累积冲刺能量，每 1 秒 +1 点；
    /// 每点能量在移速计算（0.1.16b BuffComponent.RefreshSpeed，Speed = 560 × (SpeedDelta + 0.3)）
    /// 基础上额外增加 BoostPerPoint 档（SpeedDelta 单位），上限 MaxBoostPoints 点。
    /// 停止奔跑后每秒衰减 DecayPerSec 点。疲惫（Exhausted，体力耗尽）时不累积。
    /// 全程服务端（房主进程）逻辑，Hasung 无需装 mod；默认关闭。
    /// </summary>
    [PatchFeature(
        "Hasung 越跑越快：Hasung 奔跑时累积冲刺能量，移速随消耗渐增（上限可配，停跑缓慢衰减；服务端，房主装生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
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
