using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.TalkPolice
{
    /// <summary>
    /// 发言监督员：监听全房发言，统计每个玩家在滑动时间窗内的发言次数，
    /// 话痨超过阈值自动在聊天区点名广播（全房可见）。每轮投票（计票）时播报"本场话痨之王"并清空计数。
    /// 走聊天广播通道，无需房主与对方装插件。
    /// </summary>
    [PatchFeature(
        "发言监督员：统计发言次数，话痨超阈值自动点名广播；每轮计票时播报话痨之王。全房可见，无需房主。",
        defaultEnabled: true)]
    public sealed class TalkPoliceFeature
    {
        [Config("滑动时间窗（秒）：窗口内发言达到阈值即点名。", Min = 5f, Max = 300f)]
        public static float WindowSeconds = 30f;

        [Config("话痨阈值：窗口内发言达到该次数即被点名。", Min = 2, Max = 50)]
        public static int Threshold = 5;

        [Config("点名台词模板：{0} 为玩家名，{1} 为发言次数。")]
        public static string TauntFormat = "{0} 已经说了 {1} 句了，话多必有鬼";

        [Config("每轮计票时的话痨之王播报模板：{0} 为玩家名，{1} 为窗口内发言次数，空则关闭该播报。")]
        public static string SummaryFormat = "本场话痨之王：{0}（{1} 句）";

        [Config("点名冷却时间（秒），防止同一窗口连续点名刷屏。", Min = 0f, Max = 120f)]
        public static float Cooldown = 10f;

        [Config("是否不统计自己的发言（建议开启，免得自己被抓）。")]
        public static bool IgnoreSelf = true;
    }
}
