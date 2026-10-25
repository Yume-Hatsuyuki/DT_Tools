using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using DT_Tools.Automation;
using DT_Tools.Commands;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DT_Tools
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            DtPaths.EnsureRoot();
            var config = new ConfigFile(DtPaths.Cfg, saveOnInit: false) { SaveOnConfigSet = false };

            CoroutineHost.EnsureCreated();

            var result = Engine.Load(new Harmony(MyPluginInfo.PLUGIN_GUID), config, Logger);
            CommandRegistry.Load();

            if (!File.Exists(DtPaths.Cfg))
                config.Save();

            var go = new GameObject("DT_Tools");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AutomationRunner>();

            if (DT_Tools.WebConsole.WebConsoleOptions.Enabled)
            {
                var wcGo = new GameObject("DT_WebConsole");
                Object.DontDestroyOnLoad(wcGo);
                wcGo.AddComponent<DT_Tools.WebConsole.WebConsole>().Init(Logger);
            }
            else
            {
                Log.Info("Plugin", "WebConsole 已在配置中禁用，跳过启动。");
            }

            if (DT_Tools.WebConsole.Mcp.McpOptions.Enabled)
            {
                var mcpGo = new GameObject("DT_Mcp");
                Object.DontDestroyOnLoad(mcpGo);
                mcpGo.AddComponent<DT_Tools.WebConsole.Mcp.McpBridge>().Init(Logger);
            }
            else
            {
                Log.Info("Plugin", "MCP 桥接已在配置中禁用，跳过启动（/mcp 仍可经 WebConsole 端口兜底接入）。");
            }

            if (result.FailedCount > 0)
            {
                Log.Warn("Plugin",
                    $"{MyPluginInfo.PLUGIN_GUID} 加载完成：挂载成功 {result.MountedCount}，失败 {result.FailedCount}。失败的功能已跳过，其余不受影响；各功能 Enabled 可在局内热切换。");
            }
            else
            {
                Log.Info("Plugin",
                    $"{MyPluginInfo.PLUGIN_GUID} 加载完成：补丁功能 {result.MountedCount} 个，自动化模块 {result.ModuleCount} 个，命令 {CommandRegistry.All.Count} 条（Enabled 支持局内热切换）。");
            }
        }
    }
}
