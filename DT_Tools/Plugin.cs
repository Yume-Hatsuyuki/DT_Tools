using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

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

            // 自动扫描当前程序集中所有标记了 [PatchConfig] 的补丁类
            var patchTypes = typeof(Plugin).Assembly.GetTypes()
                .Where(t => t.IsClass && Attribute.IsDefined(t, typeof(PatchConfigAttribute)));

            foreach (var type in patchTypes)
            {

                // 强制运行补丁类的静态构造函数，确保其中的 Config.Bind() 被调用，
                // 这样即使补丁被禁用，.cfg 中也会生成对应的数值配置段（如 [BlackAttackRange]、[MoveSpeed]）。
                // 否则禁用补丁时类不会被加载，静态构造函数不执行，数值配置永远不会出现在 .cfg 里。
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                // 获取特性实例
                var attr = (PatchConfigAttribute)Attribute.GetCustomAttribute(type, typeof(PatchConfigAttribute));

                // 拼装最终描述：有 Author 时在最前面加一行
                string fullDescription = string.IsNullOrWhiteSpace(attr.Author)
                    ? attr.Description
                    : $"Author: {attr.Author}\n{attr.Description}";

                // 绑定配置项：使用特性中的中文描述和独立的默认值
                var configEntry = Config.Bind<bool>(
                    "General",
                    attr.ConfigKey,
                    attr.DefaultEnabled,
                    fullDescription
                );

                if (configEntry.Value)
                {
                    harmony.PatchAll(type);
                    Logger.LogInfo($"已启用补丁: {type.Name} (配置键: {attr.ConfigKey})");
                }
                else
                {
                    Logger.LogInfo($"已跳过补丁: {type.Name} (配置中已禁用)");
                }
            }

            Logger.LogInfo($"补丁 {PluginInfo.PLUGIN_GUID} 加载完成。");
        }
    }
}
