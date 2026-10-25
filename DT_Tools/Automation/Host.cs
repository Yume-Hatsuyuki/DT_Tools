using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Automation
{
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
