using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.DestroyEvidence
{
    /// <summary>
    /// 销毁证据读条。DeviceBase.UseSabotage 原版 2.5f（0.1.15b DeviceBase.cs:460）
    /// → CastingTime（仅本地表现；服务端冷却与判定不受影响）。
    /// </summary>
    [PatchFeature(
        "专业清洁：可修改 Dark/Black 销毁证据读条时长（默认 2.5s，仅本地表现）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class DestroyEvidenceFeature
    {
        [Config("销毁证据读条时长（秒）。建议 >= 0.1；服务端冷却不受影响。", Min = 0.1f)]
        public static float CastingTime = 2.5f;
    }
}
