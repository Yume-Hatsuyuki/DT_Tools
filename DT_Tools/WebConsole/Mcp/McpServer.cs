using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DT_Tools.Commands;
using DT_Tools.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp
{
    /// <summary>
    /// MCP 桥接服务器：寄生于 WebConsole 的 HttpListener（同端口、同鉴权），新增 /mcp 路由。
    /// 传输 = MCP Streamable HTTP（无状态）：POST /mcp 收 JSON-RPC，application/json 回包；
    /// 通知回 202 空包；GET/DELETE → 405（无服务端主动推送，不开 SSE 通道）。
    /// 协议基准 2025-06-18，initialize 兼容协商 2024-11-05 / 2025-03-26。
    /// 线程模型：本类只做协议解析（HTTP 线程可安全执行）；工具实现触 Unity API 的
    /// 必须自走 WebConsole.RunOnMain（见 AGENTS.md §13）。
    /// </summary>
    internal static class McpServer
    {
        private const string LatestProtocol = "2025-06-18";
        private static readonly string[] SupportedProtocols = { "2024-11-05", "2025-03-26", LatestProtocol };

        // 挂件状态面（McpStatusApi 同程序集读）：累计调用次数与最近调用的工具名
        internal static long _callCount;
        internal static string _lastCall = "（无）";

        // 挂件调用日志环形缓冲（新→旧展示）：HTTP 线程池并发触达，锁内读写；
        // 只在内存，不做持久化，容量取"滚动能看完全部近期动作"的最小够用量
        private const int CallLogCapacity = 30;
        private static readonly object _callLogLock = new object();
        private static readonly List<object> _callLog = new List<object>();

        /// <summary>追加一条调用记录（供 /api/mcp/status 与挂件滚动日志）。</summary>
        private static void PushCallLog(string time, string name, string args, bool ok, string error, long ms)
        {
            lock (_callLogLock)
            {
                _callLog.Insert(0, new { t = time, name, args, ok, error, ms });
                if (_callLog.Count > CallLogCapacity)
                    _callLog.RemoveAt(_callLog.Count - 1);
            }
        }

        /// <summary>调用日志快照（新→旧）。元素为匿名对象，由 Newtonsoft 序列化。</summary>
        internal static List<object> SnapshotCallLog()
        {
            lock (_callLogLock)
                return new List<object>(_callLog);
        }

        private static bool _announced;

        /// <summary>
        /// 注册到一条路由面（可多次调用、不同 Router 实例）：MCP 独立监听与 WebConsole 路由
        /// 各挂一份，共用同一套静态处理器——MCP 客户端走哪个门都等价。就绪日志只打一次。
        /// </summary>
        public static void RegisterRoutes(Router router)
        {
            McpToolLoader.Load();
            router.Add("POST", "/mcp", HandlePost);
            router.Add("GET", "/mcp", NotAllowed);
            router.Add("DELETE", "/mcp", NotAllowed);
            router.Add("GET", "/api/mcp/status", McpStatusApi.HandleStatus);
            router.Add("POST", "/api/mcp/toggle", McpStatusApi.HandleToggle);
            if (_announced)
                return;
            _announced = true;
            Log.Info("Mcp", $"MCP 桥接就绪 → http://<ListenIp>:{McpOptions.Port}/mcp（Enabled={McpOptions.Enabled}）");
        }

        private static void NotAllowed(HttpListenerContext ctx)
            => HttpServer.WriteText(ctx.Response, 405, "Method Not Allowed");

        private static void HandlePost(HttpListenerContext ctx)
        {
            var resp = ctx.Response;
            if (!McpOptions.Enabled)
            {
                HttpServer.WriteText(resp, 503, "MCP bridge disabled");
                return;
            }

            string raw = HttpServer.ReadBody(ctx.Request);
            JToken root;
            try
            {
                root = JToken.Parse(raw);
            }
            catch (Exception)
            {
                HttpServer.WriteRaw(resp, 400, "application/json",
                    McpRpc.Error(null, McpRpc.ParseError, "parse error").ToString(Formatting.None));
                return;
            }

            if (root is JArray batch)
            {
                // batch：逐条处理，通知不产生回包；全部为通知时回 202 空包
                var responses = new JArray(batch.Select(item => ProcessMessage(item as JObject)).Where(r => r != null));
                if (responses.Count == 0)
                {
                    WriteEmpty(resp);
                    return;
                }
                HttpServer.WriteRaw(resp, 200, "application/json", responses.ToString(Formatting.None));
                return;
            }

            var response = ProcessMessage(root as JObject);
            if (response == null)
            {
                WriteEmpty(resp);
                return;
            }
            HttpServer.WriteRaw(resp, 200, "application/json", response.ToString(Formatting.None));
        }

        private static void WriteEmpty(HttpListenerResponse resp)
            => HttpServer.WriteRaw(resp, 202, "application/json", "");

        /// <summary>处理一条 JSON-RPC 消息。通知（无 id）处理后返回 null（不回包）。</summary>
        private static JObject ProcessMessage(JObject msg)
        {
            if (msg == null || msg["method"] == null)
                return McpRpc.Error(null, McpRpc.InvalidRequest, "invalid request: missing method");

            string method = msg["method"].ToString();
            bool isNotification = msg["id"] == null;
            JToken id = isNotification ? null : msg["id"];
            var parameters = msg["params"] as JObject;

            try
            {
                switch (method)
                {
                    case "initialize":
                        return isNotification ? null : McpRpc.Result(id, BuildInitialize(parameters));
                    case "notifications/initialized":
                        return null;
                    case "ping":
                        return isNotification ? null : McpRpc.Result(id, new JObject());
                    case "tools/list":
                        return isNotification ? null : McpRpc.Result(id, BuildToolsList());
                    case "tools/call":
                        return isNotification ? null : HandleToolCall(id, parameters);
                    default:
                        // 未知通知按 JSON-RPC 约定静默忽略；未知请求回 -32601
                        return isNotification ? null : McpRpc.Error(id, McpRpc.MethodNotFound, $"unknown method: {method}");
                }
            }
            catch (Exception ex)
            {
                Log.Exception("Mcp", ex, $"MCP 消息处理异常: {method}");
                return isNotification ? null : McpRpc.Error(id, McpRpc.InternalError, "internal error");
            }
        }

        private static JObject BuildInitialize(JObject parameters)
        {
            // 版本协商：客户端请求的版本在支持列表内则回显，否则回最新（客户端可拒绝）
            string requested = parameters?["protocolVersion"]?.ToString();
            string agreed = requested != null && SupportedProtocols.Contains(requested) ? requested : LatestProtocol;
            return new JObject
            {
                ["protocolVersion"] = agreed,
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject { ["listChanged"] = false },
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = "dt-tools",
                    ["version"] = MyPluginInfo.PLUGIN_VERSION,
                },
                ["instructions"] = "DT_Tools（Deadly Trick 工具插件）桥接。行动能力全部在 run_command（模组命令）里，先 list_commands 查阅；发包用 call_c/call_s（房主）或 call_me（客户端），"
                    + "包语义与风险先读仓库 .github/skills/MCP/MCP.md（功能列表），未收录的包按同目录 SKILL.md 维护协议读 0.1.16b 源码补录后再发。",
            };
        }

        private static JObject BuildToolsList()
        {
            var tools = new JArray();
            foreach (var def in McpToolLoader.Tools.Values.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                tools.Add(new JObject
                {
                    ["name"] = def.Name,
                    ["description"] = def.Description,
                    ["inputSchema"] = def.SchemaJson.DeepClone(),
                });
            }
            return new JObject { ["tools"] = tools };
        }

        private static JObject HandleToolCall(JToken id, JObject parameters)
        {
            string name = parameters?["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                return McpRpc.Error(id, McpRpc.InvalidParams, "missing tool name");
            if (!McpToolLoader.Tools.TryGetValue(name, out var def))
                return McpRpc.Error(id, McpRpc.InvalidParams, $"unknown tool: {name}");

            var args = parameters["arguments"] as JObject ?? new JObject();
            string argSummary = SummarizeArgs(args);
            _callCount++;
            _lastCall = $"{name} {argSummary}";

            bool logCalls = McpOptions.LogCalls;
            if (logCalls)
                Log.Info("Mcp", $"调用 {name} {argSummary}");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            CommandResult result;
            try
            {
                result = def.Execute(args);
            }
            catch (Exception ex)
            {
                Log.Exception("Mcp", ex, $"工具执行异常: {name}");
                if (logCalls)
                    Log.Info("Mcp", $"结果 {name} 异常 {ex.GetType().Name}({sw.ElapsedMilliseconds}ms)");
                PushCallLog(DateTime.Now.ToString("HH:mm:ss"), name, argSummary, false, $"异常 {ex.GetType().Name}", sw.ElapsedMilliseconds);
                return McpRpc.Error(id, McpRpc.InternalError, "tool error");
            }
            sw.Stop();

            if (logCalls)
                Log.Info("Mcp", result.Ok
                    ? $"结果 {name} ok({sw.ElapsedMilliseconds}ms)"
                    : $"结果 {name} fail[{result.Error}]({sw.ElapsedMilliseconds}ms)");
            PushCallLog(DateTime.Now.ToString("HH:mm:ss"), name, argSummary, result.Ok, result.Error, sw.ElapsedMilliseconds);
            return McpRpc.Result(id, new JObject
            {
                ["content"] = new JArray
                {
                    new JObject { ["type"] = "text", ["text"] = Json.To(result) },
                },
                ["isError"] = !result.Ok,
            });
        }

        /// <summary>参数摘要（key=value，超长截断）：进全局日志与挂件最近调用，不落全量 JSON。</summary>
        private static string SummarizeArgs(JObject args)
        {
            if (args == null || !args.HasValues)
                return "";
            var parts = args.Properties().Select(p =>
            {
                string value = p.Value?.ToString(Formatting.None) ?? "null";
                return $"{p.Name}={Truncate(value, 160)}";
            });
            return Truncate(string.Join(" ", parts), 300);
        }

        private static string Truncate(string text, int max)
            => text.Length <= max ? text : text.Substring(0, max) + "…";
    }
}
