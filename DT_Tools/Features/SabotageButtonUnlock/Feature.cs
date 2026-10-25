using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    [PatchFeature(
        "破坏行为按钮身份放开：LockDoorMode/BreakPowerMode 控制锁门/拆电源按钮额外放行谁（Dark=默认原版仅黑幕；Black=黑幕+黑方；White=黑幕+白方；All=全部），WhiteDestroyEvidence 让白方也能销毁证据。\n拆电源放开即全场景生效（服务端无校验）；锁门需房主启用「DoorLockServer」、白方销毁证据需房主启用「WhiteSabotageClue」，否则会给未生效提示。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.GameSabotage | FeatureTag.GameDoor | FeatureTag.GameFuse,
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
