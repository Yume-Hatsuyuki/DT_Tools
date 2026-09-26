using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>
    /// 破坏行为按钮身份放开（客户端）：锁门/拆电源的交互提示按钮原版仅黑幕可见
    /// （UI_GameScene.cs:1373 + DeviceBase.cs:395 两层判定），按两个独立档位放开显示
    /// （黑幕原本就可用，维持不变）。拆电源服务端无身份校验（Fusebox.Interact 全身份
    /// 受理），放开即全场景生效；锁门服务端有黑幕硬校验（Server.Game/Door.cs:53），
    /// 实际生效需房主启用「DoorLockServer」，未启用时本功能会给出本地失败提示。
    /// </summary>
    [PatchFeature(
        "破坏行为按钮身份放开：LockDoorMode/BreakPowerMode 各自控制锁门/拆电源按钮额外放行谁（黑方/白方/所有；黑幕原本可用）。\n拆电源放开即全场景生效（服务端无校验）；锁门实际生效需房主启用「DoorLockServer」，否则会给锁门失败提示。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class SabotageButtonUnlockFeature
    {
        [Config("锁门按钮额外放行：Black=仅黑方；White=仅白板；All=所有身份。")]
        public static SabotageMode LockDoorMode = SabotageMode.Black;

        [Config("拆电源按钮额外放行：Black=仅黑方；White=仅白板；All=所有身份。")]
        public static SabotageMode BreakPowerMode = SabotageMode.Black;
    }
}
