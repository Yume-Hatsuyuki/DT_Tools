using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace DT_Tools.Automation
{
    /// <summary>
    /// 自动化宿主：总开关配置 + 主线程 Tick。
    /// </summary>
    internal static class AutomationHost
    {
        public const string Section = "Automation";

        public static ConfigEntry<bool> CfgEnabled;

        private static bool _bound;
        private static ManualLogSource _log;

        public static bool IsHostEnabled => CfgEnabled != null && CfgEnabled.Value;

        public static void BindAndInit(ConfigFile config, ManualLogSource log)
        {
            _log = log;
            if (!_bound)
            {
                CfgEnabled = config.Bind(
                    Section,
                    "Enabled",
                    true,
                    "Author: 梦初雪\n" + "自动化总开关。关闭后所有自动化模块均不运行。");
                _bound = true;
            }

            AutomationRegistry.Initialize(config, log);
            log?.LogInfo($"[Automation] host bound, master Enabled={CfgEnabled.Value}, modules={AutomationRegistry.All.Count}");
        }

        public static void Tick()
        {
            if (!_bound) return;
            AutomationRegistry.TickAll(IsHostEnabled);
        }
    }

    /// <summary>
    /// 挂到 WebConsole 同级或独立 GO 上，驱动 AutomationHost.Tick。
    /// </summary>
    internal sealed class AutomationRunner : MonoBehaviour
    {
        private void Update()
        {
            AutomationHost.Tick();
        }
    }
}
