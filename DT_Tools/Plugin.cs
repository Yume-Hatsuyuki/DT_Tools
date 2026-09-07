using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using DT_Tools.Console;

namespace DT_Tools
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private const string WEB_CONSOLE_SECTION = "WebConsole";

        private void Awake()
        {
            Instance = this;

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            var patchTypes = typeof(Plugin).Assembly.GetTypes()
                .Where(t => t.IsClass && Attribute.IsDefined(t, typeof(PatchConfigAttribute)))
                .Select(t => (
                    Type: t,
                    Attr: (PatchConfigAttribute)Attribute.GetCustomAttribute(t, typeof(PatchConfigAttribute))))
                .ToList();

            // 稳定顺序：补丁段 + WebConsole 段一起按段名排序，避免反射/书写顺序导致 .cfg 每次重排，
            // 也让 WebConsole 落在它字母序应在的位置（而不是因为代码写在最前面就永远排第一）。
            var allSections = patchTypes
                .Select(x => x.Attr.Section)
                .Append(WEB_CONSOLE_SECTION)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            // 按段绑定：先写 Enabled，再跑静态构造函数写入同段子项，保证子项紧跟开关下方
            var enableEntries = new System.Collections.Generic.List<(Type Type, ConfigEntry<bool> Entry)>();

            foreach (var section in allSections)
            {
                if (section == WEB_CONSOLE_SECTION)
                {
                    WebConsole.CfgEnabled = Config.Bind(
                        WEB_CONSOLE_SECTION,
                        "Enabled",
                        false,
                        "是否启用控制台 WebUI。");

                    WebConsole.CfgPort = Config.Bind(
                        WEB_CONSOLE_SECTION,
                        "Port",
                        19450,
                        "WebUI 监听端口。启动后用浏览器打开 http://127.0.0.1:<Port>/");

                    WebConsole.CfgPassword = Config.Bind(
                        WEB_CONSOLE_SECTION,
                        "Password",
                        "",
                        "访问密码。留空则不需要密码。");

                    continue;
                }

                var (type, attr) = patchTypes.First(x => x.Attr.Section == section);
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

            // ── 启动 WebConsole（受 Enabled 开关控制）────────
            if (WebConsole.CfgEnabled.Value)
            {
                var go = new GameObject("DT_WebConsole");
                DontDestroyOnLoad(go);
                go.AddComponent<WebConsole>().Init(Logger);
            }
            else
            {
                Logger.LogInfo("WebConsole 已在配置中禁用，跳过启动。");
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
