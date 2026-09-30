using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.SupplyShelfRefill
{
    /// <summary>
    /// 货架补货（房主权威）：拿取后按间隔补同一格；可选开局必出指定道具 ID。
    /// 开局道具池见 SupplyShelf；兜底道具集中在 SupplyShelfRefillLogic.FallbackItems。
    /// </summary>
    [PatchFeature(
        "货架补货：拿取后按间隔补同一格；可选开局必出指定道具 ID。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class SupplyShelfRefillFeature
    {
        [Config("拿取后补货间隔（秒）。-1 关闭；0 立刻；大于 0 为延迟秒数。")]
        public static int RefillIntervalSeconds = -1;

        [Config("首轮必出道具 ID（DataId）。0 表示不强制。例：3009 铃铛，3008 气喇叭。")]
        public static int GuaranteedItemId = 0;
    }
}
