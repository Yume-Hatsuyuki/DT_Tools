using System.IO;
using BepInEx;
using HarmonyLib;
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
                    (WebConsoleSection, (System.Action)BindWebConsoleSection)
                });

            // 关闭自动保存后，首次运行需主动落盘生成 .cfg
            if (!File.Exists(Config.ConfigFilePath))
                Config.Save();

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
                    $"{PluginInfo.PLUGIN_GUID} 加载完成：成功 {result.EnabledCount}，" +
                    $"跳过 {result.SkippedCount}，失败 {result.FailedCount}。" +
                    "失败的功能已跳过，其余不受影响；请检查上方错误日志。");
            }
            else
            {
                Logger.LogInfo(
                    $"{PluginInfo.PLUGIN_GUID} 加载完成：已启用 {result.EnabledCount} 个功能" +
                    $"（跳过 {result.SkippedCount}）。");
            }
        }

        private void BindWebConsoleSection()
        {
            WebConsole.CfgEnabled = Config.Bind(
                WebConsoleSection,
                "Enabled",
                true,
                "是否启用控制台 WebUI。");

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
    }
}
