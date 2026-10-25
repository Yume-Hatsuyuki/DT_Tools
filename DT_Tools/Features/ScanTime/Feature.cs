using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.ScanTime
{
    [PatchFeature(
        "福尔摩斯：可修改搜索读条时长（默认 2.0s）。配置<0.5s 忽略 ScanUp。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameEvidence,
        Author = "梦初雪")]
    public sealed class ScanTimeFeature
    {
        [Config("扫描读条时长（秒）。建议 >= 0.1。", Min = 0.1f)]
        public static float CastingTime = 2.0f;
    }
}
