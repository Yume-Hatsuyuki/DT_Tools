using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Automation.AutoReady
{
    /// <summary>进入大厅后自动准备。只准备，绝不发送开局包 C_START（原版也无全员准备自动开局）。</summary>
    [AutomationModule("自动准备", "进入大厅后自动点击准备（与 F5/准备按钮同路径发包 C_READY）；只准备不代开局。", Author = "梦初雪")]
    public sealed class AutoReadyModule
    {
        [Config("进入大厅后延迟多少秒再准备（等服务端房间就绪）。", Min = 0, Max = 120)]
        public static float DelaySeconds = 2f;

        [Config("准备后仍未就绪时的重试间隔（秒）。", Min = 0.2f, Max = 60)]
        public static float RetryInterval = 1f;

        [Config("本次进入大厅最多尝试次数。达到后停止。", Min = 1, Max = 999)]
        public static int MaxAttempts = 10;

        public static void Tick(bool hostEnabled)
        {
            if (!hostEnabled || !Engine.Enabled<AutoReadyModule>())
            {
                AutoReadyState.Reset();
                return;
            }
            AutoReadyTrigger.Tick();
        }
    }
}
