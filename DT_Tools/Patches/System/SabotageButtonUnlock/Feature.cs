using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>
    /// 破坏行为按钮身份放开（客户端）：锁门/拆电源/销毁证据的交互提示按钮原版仅黑幕
    /// 可见（销毁证据目标黑方也可见：UI_GameScene.cs:1373 + DeviceBase.cs:395 两层判定），
    /// 按档位放开显示。锁门/拆电源档位是"叠加"语义：Dark（默认）=仅黑幕（原版档位，
    /// 不额外放行）；Black=黑幕+黑方；White=黑幕+白方；All=黑幕+黑方+白方。
    /// 拆电源服务端无身份校验（Fusebox.Interact 全身份受理），放开即全场景生效；
    /// 锁门服务端有黑幕硬校验（Server.Game/Door.cs:53），实际生效需房主启用
    /// 「DoorLockServer」（放行档位 + 锁门留痕都在该功能）；
    /// 白方销毁证据服务端有 Black/Dark 硬校验（Server.Game/Device.cs:219），
    /// 实际生效需房主启用「WhiteSabotageClue」。
    /// 白方动作的留痕开关在房主端「WhiteSabotageClue」（拉电闸/销毁证据）与
    /// 「DoorLockServer」（锁门）——本功能只管按钮亮不亮。
    /// </summary>
    [PatchFeature(
        "破坏行为按钮身份放开：LockDoorMode/BreakPowerMode 控制锁门/拆电源按钮额外放行谁（Dark=默认原版仅黑幕；Black=黑幕+黑方；White=黑幕+白方；All=全部），WhiteDestroyEvidence 让白方也能销毁证据。\n拆电源放开即全场景生效（服务端无校验）；锁门需房主启用「DoorLockServer」、白方销毁证据需房主启用「WhiteSabotageClue」，否则会给未生效提示。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Author = "梦初雪")]
    public sealed class SabotageButtonUnlockFeature
    {
        [Config("锁门按钮额外放行：Dark=默认（原版档位，仅黑幕）；Black=黑幕+黑方；White=黑幕+白方；All=黑幕+黑方+白方。")]
        public static SabotageMode LockDoorMode = SabotageMode.Dark;

        [Config("拆电源按钮额外放行：Dark=默认（原版档位，仅黑幕）；Black=黑幕+黑方；White=黑幕+白方；All=黑幕+黑方+白方。")]
        public static SabotageMode BreakPowerMode = SabotageMode.Dark;

        [Config("白方也可销毁证据（原版仅黑方/黑幕可用）。白方实际销毁生效需房主启用「WhiteSabotageClue」。")]
        public static bool WhiteDestroyEvidence = false;
    }
}
