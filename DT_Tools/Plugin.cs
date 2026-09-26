using System;
using System.IO;
using BepInEx;
using DT_Tools.Automation;
using DT_Tools.Commands;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DT_Tools
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // 热改只动内存；落盘仅通过显式 Save / 配置导入
            Config.SaveOnConfigSet = false;

            // 主线程泵 + 协程宿主（常驻）：HTTP/文件监听线程投递的生命周期钩子
            // （WireLifecycle）在此安全触碰 Unity API
            CoroutineHost.EnsureCreated();

            var result = Engine.Load(new Harmony(PluginInfo.PLUGIN_GUID), Config, Logger);
            CommandRegistry.Load();

            // 关闭自动保存后，首次运行需主动落盘生成 .cfg
            if (!File.Exists(Config.ConfigFilePath))
                Config.Save();

            // 自动化主线程 Tick（常驻）
            var go = new GameObject("DT_Tools");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AutomationRunner>();

            // WebUI 控制台（按配置启停）
            if (DT_Tools.WebConsole.WebConsoleOptions.Enabled)
            {
                var wcGo = new GameObject("DT_WebConsole");
                Object.DontDestroyOnLoad(wcGo);
                wcGo.AddComponent<DT_Tools.WebConsole.WebConsole>().Init(Logger);
            }
            else
            {
                Logger.LogInfo("WebConsole 已在配置中禁用，跳过启动。");
            }

            if (result.FailedCount > 0)
            {
                Logger.LogWarning(
                    $"{PluginInfo.PLUGIN_GUID} 加载完成：挂载成功 {result.MountedCount}，失败 {result.FailedCount}。" +
                    "失败的功能已跳过，其余不受影响；各功能 Enabled 可在局内热切换。");
            }
            else
            {
                Logger.LogInfo(
                    $"{PluginInfo.PLUGIN_GUID} 加载完成：补丁功能 {result.MountedCount} 个，" +
                    $"自动化模块 {result.ModuleCount} 个，命令 {CommandRegistry.All.Count} 条" +
                    "（Enabled 支持局内热切换）。");
            }
        }
    }
}
