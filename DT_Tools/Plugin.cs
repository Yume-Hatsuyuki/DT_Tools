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

            // 稳定顺序：按段名排序，避免反射顺序导致 .cfg 每次重排
            var patchTypes = typeof(Plugin).Assembly.GetTypes()
                .Where(t => t.IsClass && Attribute.IsDefined(t, typeof(PatchConfigAttribute)))
                .Select(t => (
                    Type: t,
                    Attr: (PatchConfigAttribute)Attribute.GetCustomAttribute(t, typeof(PatchConfigAttribute))))
                .OrderBy(x => x.Attr.Section, StringComparer.Ordinal)
                .ToList();

            // 按段绑定：先写 Enabled，再跑静态构造函数写入同段子项，保证子项紧跟开关下方
            var enableEntries = new System.Collections.Generic.List<(Type Type, ConfigEntry<bool> Entry)>();
            foreach (var (type, attr) in patchTypes)
            {
                string fullDescription = string.IsNullOrWhiteSpace(attr.Author)
                    ? attr.Description
                    : $"Author: {attr.Author}\n{attr.Description}";

                var configEntry = Config.Bind(
                    attr.Section,
                    "Enabled",
                    attr.DefaultEnabled,
                    fullDescription);

                enableEntries.Add((type, configEntry));

                // 立即跑静态构造，把该功能的子项绑进同一段（出现在 Enabled 之后）
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }

            // 按开关决定是否打补丁
            foreach (var (type, configEntry) in enableEntries)
            {
                if (configEntry.Value)
                {
                    harmony.PatchAll(type);
                    Logger.LogInfo($"已启用补丁: {type.Name} ([{configEntry.Definition.Section}].Enabled)");
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
