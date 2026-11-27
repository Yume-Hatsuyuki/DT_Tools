// 作用：声明重复角色功能、配置项与启用状态；不实现补丁与运行时逻辑。
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
