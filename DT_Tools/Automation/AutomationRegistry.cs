using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using DT_Tools.Core;

namespace DT_Tools.Automation
{
    /// <summary>
    /// 发现并驱动全部 IAutomationModule。
    /// </summary>
    internal static class AutomationRegistry
    {
        private static readonly List<IAutomationModule> Modules = new List<IAutomationModule>();
        private static bool _initialized;
        private static ManualLogSource _log;

        public static IReadOnlyList<IAutomationModule> All => Modules;

        public static void Initialize(ConfigFile config, ManualLogSource log)
        {
            if (_initialized) return;
            _log = log;
            _initialized = true;

            var types = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    t.IsClass && !t.IsAbstract &&
                    typeof(IAutomationModule).IsAssignableFrom(t) &&
                    Attribute.IsDefined(t, typeof(AutomationModuleAttribute)))
                .OrderBy(t => t.GetCustomAttribute<AutomationModuleAttribute>().Id);

            foreach (var type in types)
            {
                try
                {
                    var mod = (IAutomationModule)Activator.CreateInstance(type, nonPublic: true);
                    mod.BindConfig(config);
                    Modules.Add(mod);
                    log?.LogInfo($"[Automation] registered {mod.Id} ({mod.Section})");
                }
                catch (Exception ex)
                {
                    log?.LogError($"[Automation] failed to register {type.Name}: {ex.Message}");
                }
            }
        }

        public static IAutomationModule Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Modules.Count; i++)
            {
                if (string.Equals(Modules[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Modules[i];
            }
            return null;
        }

        public static void TickAll(bool hostEnabled)
        {
            for (int i = 0; i < Modules.Count; i++)
            {
                try
                {
                    Modules[i].Tick(hostEnabled);
                }
                catch (Exception ex)
                {
                    Modules[i].Log.Error($"tick exception: {ex.Message}");
                    _log?.LogWarning($"[Automation] {Modules[i].Id} tick: {ex.Message}");
                }
            }
        }
    }
}
