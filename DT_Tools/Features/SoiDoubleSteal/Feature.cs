using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.SoiDoubleSteal
{
    [PatchFeature(
        "SOI 双偷：SOI 整局可偷 2 次技能，偷来的技能用掉后自动恢复原技能，才能偷下一个（服务端，房主装生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerAbility | FeatureTag.GameBuff,
        Author = "花语")]
    public sealed class SoiDoubleStealFeature
    {
        [Config("SOI 整局可偷技能的总次数。", Min = 1, Max = 5)]
        public static int MaxSteals = 2;
    }
}
