using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    [PatchFeature(
        "0.1.21h 官方已移除门锁破坏玩法，锁门相关项为 mod 加回：门上的破坏读条无法还原，锁门后 20 秒由房主端自动解锁；锁门成功后本机进入全局冷却，期间换任何普通门都只显示倒计时。\n破坏行为按钮身份放开：LockDoorMode/BreakPowerMode 控制锁门/拆电源按钮额外放行谁（Dark=默认原版仅黑幕；Black=黑幕+黑方；White=黑幕+白方；All=全部），WhiteDestroyEvidence 让白方也能销毁证据。\n拆电源放开即全场景生效（服务端无校验）；锁门需房主启用「DoorLockServer」、白方销毁证据需房主启用「WhiteSabotageClue」，否则会给未生效提示。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameSabotage | FeatureTag.GameDoor | FeatureTag.GameFuse,
        Author = "梦初雪")]
    public sealed class SabotageButtonUnlockFeature
    {
        [Config("锁门按钮额外放行（0.1.21h 官方已移除门锁，启用后为 mod 加回）：Dark=默认（原版档位，仅黑幕）；Black=黑幕+黑方；White=黑幕+白方；All=黑幕+黑方+白方。")]
        public static SabotageMode LockDoorMode = SabotageMode.Dark;

        [Config("拆电源按钮额外放行：Dark=默认（原版档位，仅黑幕）；Black=黑幕+黑方；White=黑幕+白方；All=黑幕+黑方+白方。")]
        public static SabotageMode BreakPowerMode = SabotageMode.Dark;

        [Config("白方也可销毁证据（原版仅黑方/黑幕可用）。白方实际销毁生效需房主启用「WhiteSabotageClue」。")]
        public static bool WhiteDestroyEvidence = false;

        [Config("锁门冷却（秒，本机每玩家全局计时）：需与房主「DoorLockServer」的冷却间隔一致。0=不显示冷却。原版 40。")]
        public static int LockCooltime = 40;
    }
}
