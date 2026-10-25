using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.WhiteSabotageClue
{
    [PatchFeature(
        "白方破坏留痕：白方拉电闸/销毁证据时留下真实身份线索，侦探扫描设备可得。\nDestroyEvidence 同时放行白方销毁证据（原版仅黑方/黑幕）；白方的对应按钮显示见「SabotageButtonUnlock」；白方锁门的放行见「DoorLockServer」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameSabotage | FeatureTag.GameEvidence,
        Author = "梦初雪")]
    public sealed class WhiteSabotageClueFeature
    {
        [Config("白方拉电闸留痕（白方的拆电源按钮需「SabotageButtonUnlock」放行显示）。")]
        public static bool BreakPower = true;

        [Config("放行白方销毁证据并留真实身份线索（原版仅黑方/黑幕；白方的销毁按钮需「SabotageButtonUnlock」放行显示）。")]
        public static bool DestroyEvidence = true;
    }
}
