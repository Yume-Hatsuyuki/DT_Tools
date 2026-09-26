using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.SilentWeaponMove
{
    /// <summary>
    /// 刀转移静默（服务端）：军械库刀转移时不再向玩家发送 WeaponMoved 系统通知，
    /// 转移本身照常。通知点两处——黑幕本人（Armory.cs:229 ReturnWeapon）与其余玩家
    /// （DeviceManager.cs:548 SpawnNextWeapon），均经 GameRoom.AlertMessage 单发
    /// S_SYSTEM_MESSAGE（GameRoom.cs:1908，纯 UI 提示）；武器架状态同步走
    /// S_MODIFY_DEVICE 通道，与本通知无关，抑制后不会脱节。
    /// </summary>
    [PatchFeature(
        "刀转移静默：刀在军械库间转移/回收时不再向玩家弹 WeaponMoved 系统通知（转移本身照常，需房主）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class SilentWeaponMoveFeature
    {
    }
}
