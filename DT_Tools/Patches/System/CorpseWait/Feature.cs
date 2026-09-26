using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.CorpseWait
{
    /// <summary>
    /// 首具非炸弹尸体出现后，自动进入调查阶段的等待时间。
    /// 原版：Util.GetRandomNumber(50, 71) → 约 50–70 秒（0.1.15b Server.Game/Corpse.cs:218）。
    /// 手动报告尸体仍立即进入调查，不受本配置影响。
    /// </summary>
    [PatchFeature(
        "尸体自动进入调查的等待时间（秒）。默认与原版一致：约 50–70 随机。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class CorpseWaitFeature
    {
        [Config("等待秒数下限（含）。原版 50。")]
        public static int MinWaitSeconds = 50;

        [Config("等待秒数上限（不含）。原版 71 → 实际最多 70。")]
        public static int MaxWaitSeconds = 71;
    }
}
