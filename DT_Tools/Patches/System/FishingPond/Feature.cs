using DT_Tools.Core.Attributes;
using DT_Tools.Patches.System.RandomItems;

namespace DT_Tools.Patches.System.FishingPond
{
    /// <summary>
    /// 许愿鱼池：从扩展道具池随机出道具（与货架共用 Patches/System/RandomItems 池）。
    /// </summary>
    [PatchFeature(
        "许愿鱼池：从扩展池随机出道具。用 Mode 选择正常版（含武器）或安全版（不含武器）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class FishingPondFeature
    {
        [Config("道具模式：Normal=含武器；Safe=不含武器。二者互斥，只能选其一。")]
        public static RandomItemMode Mode = RandomItemMode.Normal;
    }
}
