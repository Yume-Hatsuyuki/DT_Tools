// 作用：声明许愿鱼池功能、配置项与启用状态；不实现补丁与运行时逻辑。
using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Features.FishingPond
{
    [PatchFeature(
        "许愿鱼池：从扩展池随机出道具。用 Mode 选择正常版（含武器）或安全版（不含武器）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameDevice | FeatureTag.GameWeapon,
        Author = "梦初雪")]
    public sealed class FishingPondFeature
    {
        [Config("道具模式：Normal=含武器；Safe=不含武器。二者互斥，只能选其一。")]
        public static RandomItemMode Mode = RandomItemMode.Normal;
    }
}
