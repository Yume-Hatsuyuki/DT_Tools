using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.SilentWeaponMove
{
    [PatchFeature(
        "刀转移静默：刀在军械库间转移/回收时不再向玩家弹 WeaponMoved 系统通知（转移本身照常，需房主）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameWeapon,
        Author = "梦初雪")]
    public sealed class SilentWeaponMoveFeature
    {
    }
}
