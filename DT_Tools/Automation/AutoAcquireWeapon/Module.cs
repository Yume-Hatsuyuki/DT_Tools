using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Automation.AutoAcquireWeapon
{
    /// <summary>进入 Survive 后自动从开放武器架取刀（C_INTERACT_ARMORY，无视距离）。</summary>
    [AutomationModule("开局自动取刀", "进入生存阶段后自动对开放武器架发送 C_INTERACT_ARMORY（白方拔刀 / 黑幕截刀）。", Author = "梦初雪")]
    public sealed class AutoAcquireWeaponModule
    {
        [Config("首次尝试前等待（秒）。进入生存阶段以后多久开始拔刀。", Min = 0, Max = 300)]
        public static float DelaySeconds = 30f;

        [Config("重试间隔（秒）。刀架未开放 / 发包失败后隔多久再试。", Min = 0.2f, Max = 60)]
        public static float RetryInterval = 1f;

        [Config("本局最多尝试次数。达到后停止。", Min = 1, Max = 9999)]
        public static int MaxAttempts = 50;

        public static void Tick(bool hostEnabled)
        {
            if (!hostEnabled || !Engine.Enabled<AutoAcquireWeaponModule>())
            {
                AutoAcquireWeaponState.Reset();
                return;
            }
            AutoAcquireWeaponTrigger.Tick();
        }
    }
}
