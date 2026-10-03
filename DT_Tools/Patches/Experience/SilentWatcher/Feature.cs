using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.SilentWatcher
{
    /// <summary>
    /// 沉默猎人：监控全房发言节奏，如果超过 N 秒全场没人说话，
    /// 自动在聊天区广播调侃"最后发言的人"（带已沉默秒数），全房可见。
    /// 只调侃"最后说话的人"，不点名沉默者本人——避免误伤死者/旁观者，节目效果安全。
    /// </summary>
    [PatchFeature(
        "沉默猎人：全场超过设定秒数无人发言时，自动广播调侃最后发言者。全房可见，无需房主。",
        defaultEnabled: true)]
    public sealed class SilentWatcherFeature
    {
        [Config("沉默触发阈值（秒）：全场超过该时长无人发言即触发广播。", Min = 20f, Max = 600f)]
        public static float TimeoutSeconds = 90f;

        [Config("播报冷却时间（秒）：触发一次后需等待该时长才可再次触发，防止刷屏。", Min = 0f, Max = 600f)]
        public static float Cooldown = 60f;

        [Config("播报台词模板：{0} 为最后发言的玩家名，{1} 为已沉默秒数（取整）。")]
        public static string TauntFormat = "{0} 说完那句后全场哑了 {1} 秒，气氛组呢？";

        [Config("检查间隔（秒）：内部扫描频率，一般不用改。", Min = 2f, Max = 30f)]
        public static float CheckInterval = 5f;
    }
}
