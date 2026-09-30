using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.DoorLockServer
{
    /// <summary>
    /// 锁门服务端放行：Door.HandleEvent 原版仅黑幕可锁门（0.1.15b Server.Game/Door.cs:53），
    /// 本功能按 LockDoorMode 额外放行黑方/白方/所有身份。锁定序列与原版一致：
    /// CanSabotage 置 false + 40 秒恢复 + S_COOLTIME_SABOTAGE + LockDoor。
    /// 白方锁门留痕（真实身份线索）随放行归本功能承担（LockDoorClue，原
    /// 「WhiteSabotageClue」的 LockDoor 配置已并入）——放行与留痕一处调整。
    /// 客户端按钮显示由「SabotageButtonUnlock」负责；进别人房间时能否锁门取决于
    /// 房主是否启用本功能（判定在房主机）。
    /// </summary>
    [PatchFeature(
        "锁门服务端放行：LockDoorMode 选额外放行谁（Dark=默认原版仅黑幕；Black=黑幕+黑方；White=黑幕+白方；All=全部），白方经放行锁门默认留真实身份线索（LockDoorClue 可关）。\n进别人房间时取决于房主是否启用本功能；客户端按钮显示见「SabotageButtonUnlock」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class DoorLockServerFeature
    {
        [Config("锁门额外放行身份：Dark=默认（原版档位，仅黑幕）；Black=黑幕+黑方；White=黑幕+白方；All=全部。")]
        public static LockDoorMode Mode = LockDoorMode.Black;

        [Config("白方经本功能放行锁门时留下真实身份线索（侦探扫描设备可得）。黑幕原版锁门与黑方锁门不记线索。")]
        public static bool LockDoorClue = true;
    }
}
