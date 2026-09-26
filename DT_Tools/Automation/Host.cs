using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Automation
{
    /// <summary>
    /// 自动化总开关 + 主线程 Tick 派发（AutomationRunner 每帧调用）。
    /// sealed class（对齐 WebConsoleOptions）——static class 无法作 Engine.SectionOf&lt;T&gt;()
    /// 的类型实参，WebUI 需要经它取段名/键名（配置零字符串）。
    /// </summary>
    [ConfigSection("自动化模块总开关；各模块自身还有独立的 Enabled 开关。",
        Group = ConfigSectionAttribute.GroupAutomation)]
    public sealed class AutomationHost
    {
        [Config("Author: 梦初雪\n是否启用自动化模块总开关。")]
        public static bool Enabled = false;

        public static void Tick()
        {
            if (!Enabled)
                return;
            Engine.TickModules(true);
        }
    }
}
