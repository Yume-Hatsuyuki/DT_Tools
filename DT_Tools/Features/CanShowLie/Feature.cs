using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.CanShowLie
{
    [PatchFeature(
        "伪证：审判讨论阶段任何身份都可使用伪证，可选内容与黑方一致。\n二阶堂希罗：我当时睡得可香了。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameVote | FeatureTag.GameEvidence,
        Author = "梦初雪")]
    public sealed class CanShowLieFeature
    {
    }
}
