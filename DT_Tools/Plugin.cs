using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace DT_Tools
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            // 扫描本程序集中所有带 [PatchConfig] 的类型
            var patchTypes = typeof(Plugin).Assembly.GetTypes()
                .Where(t => t.IsClass && Attribute.IsDefined(t, typeof(PatchConfigAttribute)));

            foreach (var type in patchTypes)
            {
                // 强制跑静态构造函数，确保即使补丁被禁用，Config.Bind 也会执行，
                // 数值配置段（如 [MoveSpeed]）仍会出现在 .cfg 中。
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                var attr = (PatchConfigAttribute)Attribute.GetCustomAttribute(
                    type, typeof(PatchConfigAttribute));

                string fullDescription = string.IsNullOrWhiteSpace(attr.Author)
                    ? attr.Description
                    : $"Author: {attr.Author}\n{attr.Description}";

                var configEntry = Config.Bind(
                    "General",
                    attr.ConfigKey,
                    attr.DefaultEnabled,
                    fullDescription);

                if (configEntry.Value)
                {
                    harmony.PatchAll(type);
                    Logger.LogInfo($"已启用补丁: {type.Name} ({attr.ConfigKey})");
                }
                else
                {
                    Logger.LogInfo($"已跳过补丁: {type.Name} (配置中已禁用)");
                }
            }

            Logger.LogInfo($"{PluginInfo.PLUGIN_GUID} 加载完成。");
        }
    }
}
