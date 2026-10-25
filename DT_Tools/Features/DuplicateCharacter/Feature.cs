using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.DuplicateCharacter
{
    [PatchFeature(
        "重复角色：多名玩家可选择同一角色（房主启用即可）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.PlayerIdentity | FeatureTag.SysRoom,
        Author = "梦初雪")]
    public sealed class DuplicateCharacterFeature
    {
    }
}
