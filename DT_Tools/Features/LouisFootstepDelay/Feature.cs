using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LouisFootstepDelay
{
    [PatchFeature(
        "路易斯警报脚印延长：被路易斯警报标记的玩家死后，凶手脚印追踪从 3 秒延长为 5 秒（仅路易斯标记场景生效，普通杀人保持原版）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameIntel | FeatureTag.PlayerAbility,
        Author = "花语")]
    public sealed class LouisFootstepDelayFeature
    {
        [Config("被路易斯标记的玩家死后，凶手脚印追踪时长（毫秒，原版 3000）。", Min = 1000, Max = 20000)]
        public static int FootprintMs = 5000;
    }
}
