using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.CorpseWait
{
    [PatchFeature(
        "尸体自动进入调查的等待时间（秒）。默认与原版一致：约 50–70 随机。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameCorpse | FeatureTag.GameRound,
        Author = "梦初雪")]
    public sealed class CorpseWaitFeature
    {
        [Config("等待秒数下限（含）。原版 50。")]
        public static int MinWaitSeconds = 50;

        [Config("等待秒数上限（不含）。原版 71 → 实际最多 70。")]
        public static int MaxWaitSeconds = 71;
    }
}
