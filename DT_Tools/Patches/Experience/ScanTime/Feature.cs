using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.ScanTime
{
    /// <summary>
    /// 扫描读条：GameManagerEX.StartScanning 的 castingTime 可配置
    /// （0.1.15b GameManagerEX.cs:803）。配置 ≥0.5 时仍尊重 ScanUp→0.5。
    /// </summary>
    [PatchFeature(
        "福尔摩斯：可修改搜索读条时长（默认 2.0s）。配置<0.5s 忽略 ScanUp。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class ScanTimeFeature
    {
        [Config("扫描读条时长（秒）。建议 >= 0.1。", Min = 0.1f)]
        public static float CastingTime = 2.0f;
    }
}
