using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.DoorLockServer
{
    /// <summary>
    /// 锁门服务端放行：Door.HandleEvent 原版仅黑幕可锁门（0.1.15b Server.Game/Door.cs:53），
    /// 本功能按 LockDoorMode 额外放行黑方/白板/所有身份。锁定序列与原版一致：
    /// CanSabotage 置 false + 40 秒恢复 + S_COOLTIME_SABOTAGE + LockDoor。
    /// 客户端按钮显示由「SabotageButtonUnlock」负责；进别人房间时能否锁门取决于
    /// 房主是否启用本功能（判定在房主机）。
    /// </summary>
    [PatchFeature(
        "锁门服务端放行：LockDoorMode 选额外放行谁（Black=黑方；White=白板；All=所有身份；黑幕原本可用）。\n进别人房间时取决于房主是否启用本功能；客户端按钮显示见「SabotageButtonUnlock」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class DoorLockServerFeature
    {
        [Config("锁门额外放行身份：Black=仅黑方；White=仅白板；All=所有身份。")]
        public static LockDoorMode Mode = LockDoorMode.Black;
    }
}
