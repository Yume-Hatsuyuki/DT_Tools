// 作用：声明专业电工功能、配置项与启用状态；不实现补丁与运行时逻辑。
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.RepairFuse
{
    [PatchFeature(
        "专业电工：可修改修电闸默认读条时长（默认 10s，优先结算来自服务端的剩余时间）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameFuse,
        Author = "梦初雪")]
    public sealed class RepairFuseFeature
    {
        [Config("修电闸默认读条时长（秒）。建议 >= 0.1；不会超过服务端剩余时间。", Min = 0.1f)]
        public static float CastingTime = 10f;
    }
}
