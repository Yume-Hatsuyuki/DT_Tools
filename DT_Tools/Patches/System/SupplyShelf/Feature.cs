using DT_Tools.Core.Attributes;
using DT_Tools.Patches.System.RandomItems;

namespace DT_Tools.Patches.System.SupplyShelf
{
    /// <summary>
    /// 货架随机道具（房主权威）：开局整替 DeviceManager.InitStorage，从扩展池刷道具。
    /// 补货与首轮必出见 SupplyShelfRefill。
    /// </summary>
    [PatchFeature(
        "货架随机道具：开局从扩展池刷道具。Mode 选正常/安全；AlwaysFilled 控制是否留空槽。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class SupplyShelfFeature
    {
        [Config("道具模式：Normal=含武器；Safe=不含武器。")]
        public static RandomItemMode Mode = RandomItemMode.Normal;

        [Config("始终有道具：开局每个格子都填入道具，不出现空槽（0）。")]
        public static bool AlwaysFilled = false;

        /// <summary>本功能的投放池（与鱼池共用 RandomItemPools，按 Mode 取表）。</summary>
        internal static int[] ItemPool => RandomItemPools.ForMode(Mode);
    }
}
