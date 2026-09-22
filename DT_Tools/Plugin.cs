using System.IO;
using BepInEx;
using HarmonyLib;
using DT_Tools.Automation;
using DT_Tools.Console;
using DT_Tools.Core;

namespace DT_Tools
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private const string WebConsoleSection = "WebConsole";

        private void Awake()
        {
            Instance = this;

            // 热改只动内存；落盘仅通过 Config API save / import(overwrite)
            Config.SaveOnConfigSet = false;

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            var result = PatchLoader.Load(
                harmony,
                Config,
                Logger,
                extraSections: new[]
                {
                    (WebConsoleSection, (System.Action)BindWebConsoleSection),
                    (AutomationHost.Section, (System.Action)BindAutomationSection),
                });

            // 关闭自动保存后，首次运行需主动落盘生成 .cfg
            if (!File.Exists(Config.ConfigFilePath))
                Config.Save();

            // 自动化主线程 Tick（与 WebConsole 无关，始终挂载）
            var autoGo = new UnityEngine.GameObject("DT_Automation");
            UnityEngine.Object.DontDestroyOnLoad(autoGo);
            autoGo.AddComponent<AutomationRunner>();

            if (WebConsole.CfgEnabled != null && WebConsole.CfgEnabled.Value)
            {
                var go = new UnityEngine.GameObject("DT_WebConsole");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<WebConsole>().Init(Logger);
            }
            else
            {
                Logger.LogInfo("WebConsole 已在配置中禁用，跳过启动。");
            }

            if (result.FailedCount > 0)
            {
                Logger.LogWarning(
                    $"{PluginInfo.PLUGIN_GUID} 加载完成：挂载成功 {result.MountedCount}，" +
                    $"失败 {result.FailedCount}。" +
                    "失败的功能已跳过，其余不受影响；各功能 Enabled 可在局内热切换。");
            }
            else
            {
                Logger.LogInfo(
                    $"{PluginInfo.PLUGIN_GUID} 加载完成：已挂载 {result.MountedCount} 个功能（Enabled 支持局内热切换）。");
            }
        }

        private void BindWebConsoleSection()
        {
            WebConsole.CfgEnabled = Config.Bind(
                WebConsoleSection,
                "Enabled",
                true,
                "Author: 梦初雪\n"+"是否启用控制台 WebUI。");

            WebConsole.CfgPort = Config.Bind(
                WebConsoleSection,
                "Port",
                19450,
                "WebUI 监听端口。启动后用浏览器打开 http://127.0.0.1:<Port>/");

            WebConsole.CfgPassword = Config.Bind(
                WebConsoleSection,
                "Password",
                "",
                "访问密码。留空则不需要密码。");
        }

        private void BindAutomationSection()
        {
            AutomationHost.BindAndInit(Config, Logger);
        }
    }
}
