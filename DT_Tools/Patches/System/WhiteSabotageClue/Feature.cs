using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.WhiteSabotageClue
{
    /// <summary>
    /// 白方破坏留痕（房主端）：白方锁门/拉电闸时，在对应设备上以**真实玩家身份**
    /// 写入一条线索（PropositionInfo），侦探扫描设备即可在平板上看到是谁在破坏。
    /// 原版线索只记黑幕（拉电闸固定记 11037，0.1.15b Server.Game/Fusebox.cs:126），
    /// 黑方销毁证据记 66613——白方的破坏行为原版完全不留痕，本功能补齐这一环。
    /// 锁门的实际放行依赖房主启用「DoorLockServer」；拉电闸服务端无身份校验，
    /// 白方拆完线即可触发。白方留痕会暴露自己——破坏收益与被推理风险并存。
    /// </summary>
    [PatchFeature(
        "白方破坏留痕：白方锁门/拉电闸时留下真实身份线索，侦探扫描设备可得（锁门需房主启用「DoorLockServer」放行白方）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class WhiteSabotageClueFeature
    {
        [Config("白方锁门留痕")]
        public static bool LockDoor = true;

        [Config("白方拉电闸留痕")]
        public static bool BreakPower = true;
    }
}
