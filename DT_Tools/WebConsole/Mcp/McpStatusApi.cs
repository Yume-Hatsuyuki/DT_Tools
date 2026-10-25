using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Mcp
{
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
                }
            }
            return $"http://{ip}:{McpOptions.Port}/mcp";
        }
    }
}
