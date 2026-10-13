using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Mcp
{
    /// <summary>
    /// MCP 状态/开关/配置端点（WebUI 挂件用，也注册在 MCP 独立监听上）。
    /// /mcp 被停用或监听未拉起时这两条仍可用——挂件靠它读状态并重新打开。
    /// 挂件与「DT 配置」页都经 ConfigService 读写 Mcp 段，两处天然同步。
    /// </summary>
    internal static class McpStatusApi
    {
        public static void HandleStatus(HttpListenerContext ctx)
        {
            HttpServer.WriteJson(ctx.Response, new
            {
                ok = true,
                enabled = McpOptions.Enabled,
                running = McpBridge.Running,
                protocol = "2025-06-18",
                url = EndpointUrl(),
                port = McpOptions.Port,
                listenIp = McpOptions.ListenIp,
                password = McpOptions.Password ?? "",
                callCount = McpServer._callCount,
                lastCall = McpServer._lastCall,
                calls = McpServer.SnapshotCallLog(),
                tools = McpToolLoader.Tools.Values
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .Select(t => new { name = t.Name, author = t.Author })
                    .ToList(),
            });
        }

        public static void HandleToggle(HttpListenerContext ctx)
        {
            var body = ApiUtil.ParseBody(ctx);
            if (body == null || !bool.TryParse(body["enabled"]?.ToString(), out bool enabled))
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "invalid body" });
                return;
            }

            // 段名/键名必须与引擎推导值（Mcp/Enabled）逐字一致；此处走 ConfigService.Update
            // 是为了同步 BepInEx 条目再落盘——直改静态字段不会被 config.Save() 持久化
            var (ok, error, value) = ConfigService.Update(Engine.Config, "Mcp", "Enabled", enabled ? "true" : "false");
            if (!ok)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error });
                return;
            }
            ConfigService.Save(Engine.Config);

            Log.Info("Mcp", $"MCP 桥接已{(enabled ? "启用" : "停用")}（挂件开关）。");
            HttpServer.WriteJson(ctx.Response, new { ok = true, enabled = McpOptions.Enabled, value });
        }

        /// <summary>
        /// 端点展示地址（挂件复制配置用）：回环/未配置 → 127.0.0.1；通配监听 → 本机主要
        /// IPv4（局域网客户端按此接入，如 192.168.x.x）；具体 IP 原样（IPv6 已带方括号时
        /// 原样拼入）。枚举失败回退回环，只影响展示不影响监听。
        /// </summary>
        private static string EndpointUrl()
        {
            string ip = (McpOptions.ListenIp ?? "").Trim();
            if (ip.Length == 0 || ip.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                ip = "127.0.0.1";
            if (ip == "0.0.0.0" || ip == "*" || ip == "::")
            {
                ip = "127.0.0.1";
                try
                {
                    string host = Dns.GetHostName();
                    var lan = Dns.GetHostAddresses(host).FirstOrDefault(a =>
                        a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Loopback.Equals(a));
                    if (lan != null)
                        ip = lan.ToString();
                }
                catch
                {
                    // 主机名/网卡枚举不可用：保持回环展示
                }
            }
            return $"http://{ip}:{McpOptions.Port}/mcp";
        }
    }
}
