using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Patches.Fun.SupplyShelf
{
    /// <summary>
    /// 货架随机道具（房主权威）：开局整替 DeviceManager.InitStorage，从扩展池刷道具。
    /// 补货与首轮必出见 SupplyShelfRefill（下游：Refill 经 Game/ItemPools.ShelfPoolProvider
    /// 只读本层发布的投放池，无 Patches 间横向依赖——AGENTS §3）。
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

        /// <summary>本功能的投放池（与鱼池共用 ItemPools，按 Mode 取表）。</summary>
        internal static int[] ItemPool => ItemPools.ForMode(Mode);

        /// <summary>装载时向 Game 层发布投放池提供者：取值随热改的 Mode 与 Enabled 实时判定。</summary>
        private static void OnLoaded()
            => ItemPools.ShelfPoolProvider =
                () => Engine.Enabled<SupplyShelfFeature>() ? ItemPools.ForMode(Mode) : null;
    }
}
