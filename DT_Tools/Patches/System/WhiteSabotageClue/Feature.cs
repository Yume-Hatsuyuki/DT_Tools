using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.WhiteSabotageClue
{
    /// <summary>
    /// 白方破坏留痕（房主端）：白方拉电闸/销毁证据时，在对应设备上以**真实玩家身份**
    /// 写入一条线索（PropositionInfo），侦探扫描设备即可在平板上看到是谁在破坏。
    /// 原版线索只记黑幕（拉电闸固定记 11037，0.1.16b Server.Game/Fusebox.cs:126），
    /// 黑方销毁证据记 66613——白方的破坏行为原版完全不留痕，本功能补齐这一环。
    /// 销毁证据原版仅黑方/黑幕（Server.Game/Device.cs:219 硬校验），DestroyEvidence
    /// 开启后白方也可销毁，痕迹记真实身份而非固定假身份；白方的销毁按钮显示需白方端
    /// 启用「SabotageButtonUnlock」（WhiteDestroyEvidence）。白方锁门的放行与留痕
    /// 已并入「DoorLockServer」（放行+LockDoorClue 一处调整）；拉电闸服务端无身份校验，
    /// 白方拆完线即可触发。
    /// 白方留痕会暴露自己——破坏收益与被推理风险并存。
    /// </summary>
    [PatchFeature(
        "白方破坏留痕：白方拉电闸/销毁证据时留下真实身份线索，侦探扫描设备可得。\nDestroyEvidence 同时放行白方销毁证据（原版仅黑方/黑幕）；白方的对应按钮显示见「SabotageButtonUnlock」；白方锁门的放行与留痕见「DoorLockServer」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class WhiteSabotageClueFeature
    {
        [Config("白方拉电闸留痕（白方的拆电源按钮需「SabotageButtonUnlock」放行显示）。")]
        public static bool BreakPower = true;

        [Config("放行白方销毁证据并留真实身份线索（原版仅黑方/黑幕；白方的销毁按钮需「SabotageButtonUnlock」放行显示）。")]
        public static bool DestroyEvidence = true;
    }
}
