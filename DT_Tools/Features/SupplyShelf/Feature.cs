using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Features.SupplyShelf
{
    [PatchFeature(
        "货架随机道具：开局从扩展池刷道具。Mode 选正常/安全；AlwaysFilled 控制是否留空槽。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameDevice | FeatureTag.GameWeapon,
        Author = "梦初雪")]
    public sealed class SupplyShelfFeature
    {
        [Config("道具模式：Normal=含武器；Safe=不含武器。")]
        public static RandomItemMode Mode = RandomItemMode.Normal;

        [Config("始终有道具：开局每个格子都填入道具，不出现空槽（0）。")]
        public static bool AlwaysFilled = false;

        internal static int[] ItemPool => ItemPools.ForMode(Mode);

        private static void OnLoaded()
            => ItemPools.ShelfPoolProvider =
                () => Engine.Enabled<SupplyShelfFeature>() ? ItemPools.ForMode(Mode) : null;
    }
}
