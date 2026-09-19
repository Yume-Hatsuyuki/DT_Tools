using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using DT_Tools.Console.Commands;
using DT_Tools.Console.Http;
using UnityEngine;

namespace DT_Tools.Console
{
    /// <summary>
    /// 内嵌 HTTP 服务器，在本地端口提供控制台 WebUI。
    /// 线程模型：
    ///   - HttpListener 接连接线程只负责 GetContext，请求丢到 ThreadPool 处理（避免 /api/run 阻塞轮询）
    ///   - 命令通过 _pending 队列转发到 Unity 主线程，由 Update() 消费执行
    ///   - /api/run 在工作线程上同步等待主线程结果（命令可用 SetResult 返回结构化 JSON）
    ///   - 日志写入固定容量环形缓冲（无 List.RemoveAt 开销）
    /// </summary>
    internal class WebConsole : MonoBehaviour
    {
        // ── 单例 ─────────────────────────────────────────────
        public static WebConsole Instance { get; private set; }

        // ── 配置 ─────────────────────────────────────────────
        internal static ConfigEntry<bool>   CfgEnabled;
        internal static ConfigEntry<int>    CfgPort;
        internal static ConfigEntry<string> CfgPassword;   // 空 = 不鉴权

        // ── 日志环形缓冲（线程安全，O(1) 写入） ───────────────
        private const int LOG_CAPACITY = 200;
        private readonly LogEntry[] _logRing  = new LogEntry[LOG_CAPACITY];
        private int                 _logHead;   // 最旧条目下标
        private int                 _logCount;  // 当前有效条数
        private int                 _logSeq;
        private readonly object     _logLock  = new object();

        // ── 命令队列（listener→主线程） ──────────────────────
        private readonly Queue<PendingRequest> _pending  = new Queue<PendingRequest>();
        private readonly object                _pendLock = new object();

        // 当前主线程正在执行的命令的结果（仅主线程读写）
        private string _currentResultJson;

        // ── 静态响应缓存（命令表启动后不变；HTML 恒定） ─────
        private string        _cachedCommandsJson;

        // ── HTTP 监听器 ───────────────────────────────────────
        private HttpListener _listener;
        private Thread       _listenerThread;
        private bool         _running;

        // ── 命令注册表 ───────────────────────────────────────
        private readonly Dictionary<string, IConsoleCommand> _commands =
            new Dictionary<string, IConsoleCommand>(StringComparer.OrdinalIgnoreCase);

        private ManualLogSource _log4bep;

        // ═════════════════════════════════════════════════════
        //  初始化
        // ═════════════════════════════════════════════════════

        internal void Init(ManualLogSource bepLog)
        {
            if (Instance != null) { Destroy(this); return; }
            Instance  = this;
            _log4bep  = bepLog;
            DontDestroyOnLoad(gameObject);

            // 自动扫描并注册所有 IConsoleCommand 实现
            RegisterDiscoveredCommands();
            _cachedCommandsJson = BuildCommandsJson();

            StartServer();
        }

        /// <summary>
        /// 反射扫描本程序集中实现 <see cref="IConsoleCommand"/> 的具体类并注册。
        /// <list type="bullet">
        ///   <item>优先匹配构造函数 <c>ctor(Dictionary&lt;string, IConsoleCommand&gt;)</c>（如 HelpCommand，注入注册表）。</item>
        ///   <item>否则要求无参构造函数。</item>
        ///   <item>抽象类 / 接口 / 无法实例化的类型会被跳过。</item>
        /// </list>
        /// 新增命令只需实现接口并放在程序集内，无需再改此处。
        /// </summary>
        private void RegisterDiscoveredCommands()
        {
            var commandType = typeof(IConsoleCommand);
            var registryCtorParam = typeof(Dictionary<string, IConsoleCommand>);

            var types = GetType().Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && commandType.IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal); // 稳定顺序，避免反射顺序抖动

            foreach (var type in types)
            {
                IConsoleCommand instance = null;

                // HelpCommand 等需要注入注册表的命令
                var registryCtor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { registryCtorParam },
                    null);
                if (registryCtor != null)
                {
                    instance = (IConsoleCommand)registryCtor.Invoke(new object[] { _commands });
                }
                else
                {
                    var emptyCtor = type.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null);
                    if (emptyCtor == null)
                    {
                        Log($"[WebConsole] 跳过命令类型 {type.Name}：无可用构造函数。", LogLevel.Warning);
                        continue;
                    }
                    instance = (IConsoleCommand)emptyCtor.Invoke(null);
                }

                Register(instance);
                Log($"[WebConsole] 已注册命令 /{instance.Name}", LogLevel.Debug);
            }
        }

        public void Register(IConsoleCommand cmd)
        {
            _commands[cmd.Name] = cmd;
            foreach (var alias in cmd.Aliases)
                _commands[alias] = cmd;
        }

        // ═════════════════════════════════════════════════════
        //  HTTP 服务器
        // ═════════════════════════════════════════════════════

        private void StartServer()
        {
            int port = CfgPort.Value;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                _listener.Start();
                _running = true;
                _listenerThread = new Thread(ListenerLoop) { IsBackground = true, Name = "DT_WebConsole" };
                _listenerThread.Start();
                Log($"[WebConsole] 已启动 → http://127.0.0.1:{port}/", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log($"[WebConsole] 启动失败: {ex.Message}", LogLevel.Error);
            }
        }

        private void StopServer()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        private void ListenerLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { break; }

                // 接到连接后立刻交 ThreadPool，listener 继续 Accept。
                // 这样 /api/run 的 Wait 不会堵住 /api/log 轮询或其它并发请求。
                var captured = ctx;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        HandleRequest(captured);
                    }
                    catch (Exception ex)
                    {
                        try { Log($"[WebConsole] 请求处理错误: {ex.Message}", LogLevel.Warning); }
                        catch { /* 关闭阶段忽略 */ }
                        try { captured.Response.Abort(); } catch { }
                    }
                });
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;
            resp.AddHeader("Access-Control-Allow-Origin", "*");

            // 简单密码鉴权（cookie 或 query token）
            string pass = CfgPassword.Value?.Trim();
            if (!string.IsNullOrEmpty(pass))
            {
                string token = req.QueryString["token"] ?? "";
                string cookie = req.Cookies["dt_token"]?.Value ?? "";
                if (token != pass && cookie != pass)
                {
                    if (req.Url.AbsolutePath == "/login" && req.HttpMethod == "POST")
                    {
                        string body = ReadBody(req);
                        if (body == pass)
                        {
                            resp.SetCookie(new Cookie("dt_token", pass, "/"));
                            WriteText(resp, "ok");
                            return;
                        }
                    }
                    resp.StatusCode = 401;
                    if (!StaticFiles.TryServeLogin(resp, msg => Log(msg, LogLevel.Warning)))
                        WriteText(resp, "Unauthorized");
                    return;
                }
            }

            string path = req.Url.AbsolutePath;

            // 静态资源（外置 WEBUI）；缺失时对页面 503，API 不受影响
            if (StaticFiles.TryServe(req, resp, msg => Log(msg, LogLevel.Warning)))
                return;

            if (path.StartsWith("/api/config/", StringComparison.Ordinal))
            {
                HandleConfigApi(req, resp, path);
                return;
            }

            if (path.StartsWith("/api/automation/", StringComparison.Ordinal))
            {
                HandleAutomationApi(req, resp, path);
                return;
            }

            if (path == "/api/steam/players" && req.HttpMethod == "GET")
            {
                WriteJson(resp, SteamApi.GetCurrentPlayersJson(msg => Log(msg, LogLevel.Warning)));
                return;
            }

            if (path == "/api/run" && req.HttpMethod == "POST")
            {
                string cmd = ReadBody(req).Trim();
                if (string.IsNullOrEmpty(cmd))
                {
                    WriteJson(resp, "{\"ok\":false,\"error\":\"empty command\"}");
                    return;
                }

                // 同步等待主线程执行完毕，返回命令自定义的 JSON（或默认 {"ok":true}）
                var pending = new PendingRequest
                {
                    Command = cmd,
                    Done    = new ManualResetEventSlim(false)
                };
                lock (_pendLock) _pending.Enqueue(pending);

                // 超时保护：主线程卡死时不无限挂起 HTTP 线程
                const int timeoutMs = 5000;
                if (pending.Done.Wait(timeoutMs))
                    WriteJson(resp, pending.ResultJson ?? "{\"ok\":true}");
                else
                    WriteJson(resp, "{\"ok\":false,\"error\":\"timeout\"}");

                pending.Done.Dispose();
                return;
            }

            if (path == "/api/log")
            {
                // ?since=N  — 只返回 seq > N 的条目
                int since = 0;
                if (!int.TryParse(req.QueryString["since"], out since)) since = 0;
                WriteJson(resp, BuildLogJson(since));
                return;
            }

            if (path == "/api/commands")
            {
                // 命令表启动后不变，直接返回缓存
                WriteJson(resp, _cachedCommandsJson ?? "[]");
                return;
            }

            resp.StatusCode = 404;
            WriteText(resp, "Not Found");
        }

        // ═════════════════════════════════════════════════════
        //  Unity 主线程消费队列
        // ═════════════════════════════════════════════════════

        private void Update()
        {
            while (true)
            {
                PendingRequest pending;
                lock (_pendLock)
                {
                    if (_pending.Count == 0) break;
                    pending = _pending.Dequeue();
                }
                ExecuteCommand(pending);
            }
        }

        // 入队统一走 /api/run（WebUI 与脚本相同路径）；脚本可拿到 SetResult 的 JSON

        // ═════════════════════════════════════════════════════
        //  命令执行（主线程）
        // ═════════════════════════════════════════════════════

        private void ExecuteCommand(PendingRequest pending)
        {
            string raw = pending.Command ?? "";

            // 去掉前导 / 或 !
            if (raw.StartsWith("/") || raw.StartsWith("!"))
                raw = raw.Substring(1);

            if (string.IsNullOrWhiteSpace(raw))
            {
                CompleteRequest(pending, "{\"ok\":false,\"error\":\"empty command\"}");
                return;
            }

            var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string name = parts[0];

            if (!_commands.TryGetValue(name, out var cmd))
            {
                Log($"未知命令: {name}  （输入 /? 或 /help 查看帮助）", LogLevel.Warning);
                CompleteRequest(pending, "{\"ok\":false,\"error\":\"unknown command\"}");
                return;
            }

            string[] args = parts.Length > 1
                ? parts[1..]
                : Array.Empty<string>();

            if (cmd.RequireHost)
            {
                bool isHost = Managers.Host != null && Managers.Host.IsHost;
                if (!isHost)
                {
                    Log($"[{name}] 需要房主权限", LogLevel.Warning);
                    CompleteRequest(pending, "{\"ok\":false,\"error\":\"host required\"}");
                    return;
                }
            }

            _currentResultJson = null;
            try
            {
                cmd.Execute(args, this);
                CompleteRequest(pending, _currentResultJson ?? "{\"ok\":true}");
            }
            catch (Exception ex)
            {
                Log($"[{name}] 执行出错: {ex.Message}", LogLevel.Error);
                CompleteRequest(pending, "{\"ok\":false,\"error\":" + JsonEscape(ex.Message) + "}");
            }
        }

        private static void CompleteRequest(PendingRequest pending, string json)
        {
            if (pending.Done == null) return; // fire-and-forget（WebUI）
            pending.ResultJson = json;
            pending.Done.Set();
        }

        /// <summary>
        /// 命令在 Execute 内调用，为当前 /api/run 请求设置结构化 JSON 返回值。
        /// 未调用时默认返回 <c>{"ok":true}</c>。WebUI 日志仍走 <see cref="Log"/>。
        /// 仅主线程有效。
        /// </summary>
        public void SetResult(string json)
        {
            _currentResultJson = json;
        }

        // ═════════════════════════════════════════════════════
        //  日志（固定容量环形缓冲）
        // ═════════════════════════════════════════════════════

        public void Log(string message, LogLevel level = LogLevel.Message)
        {
            // 同时输出到 BepInEx 控制台
            _log4bep?.Log(level, message);

            lock (_logLock)
            {
                _logSeq++;
                var entry = new LogEntry
                {
                    Seq     = _logSeq,
                    Time    = DateTime.Now,
                    Level   = level,
                    Message = message
                };

                if (_logCount < LOG_CAPACITY)
                {
                    int idx = (_logHead + _logCount) % LOG_CAPACITY;
                    _logRing[idx] = entry;
                    _logCount++;
                }
                else
                {
                    // 覆盖最旧条目，头指针前移
                    _logRing[_logHead] = entry;
                    _logHead = (_logHead + 1) % LOG_CAPACITY;
                }
            }
        }

        private string BuildLogJson(int since)
        {
            // 持锁只做快照，字符串拼接放到锁外，缩短与 Log 的竞争窗口
            LogEntry[] snapshot;
            int count;
            int head;
            lock (_logLock)
            {
                count = _logCount;
                head  = _logHead;
                snapshot = new LogEntry[count];
                for (int i = 0; i < count; i++)
                    snapshot[i] = _logRing[(head + i) % LOG_CAPACITY];
            }

            var sb = new StringBuilder(count > 0 ? count * 64 : 2);
            sb.Append('[');
            bool first = true;
            for (int i = 0; i < count; i++)
            {
                var e = snapshot[i];
                if (e.Seq <= since) continue;
                if (!first) sb.Append(',');
                first = false;
                string color = e.Level switch
                {
                    LogLevel.Error   => "red",
                    LogLevel.Warning => "orange",
                    LogLevel.Info    => "cyan",
                    _                => "white"
                };
                sb.Append("{\"seq\":").Append(e.Seq)
                  .Append(",\"time\":\"").Append(e.Time.ToString("HH:mm:ss")).Append('"')
                  .Append(",\"color\":\"").Append(color).Append('"')
                  .Append(",\"msg\":").Append(JsonEscape(e.Message))
                  .Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private string BuildCommandsJson()
        {
            // 返回去重后的命令详情（主名 + 别名 + 用法 + 描述 + 作者），供前端预览/补全
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<IConsoleCommand>();
            foreach (var kv in _commands)
            {
                if (kv.Key != kv.Value.Name) continue; // 只取主名条目
                if (!seen.Add(kv.Value.Name)) continue;
                list.Add(kv.Value);
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var sb = new StringBuilder("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var c = list[i];
                sb.Append('{');
                sb.Append("\"name\":").Append(JsonEscape(c.Name));
                sb.Append(",\"aliases\":[");
                for (int j = 0; j < c.Aliases.Length; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(JsonEscape(c.Aliases[j]));
                }
                sb.Append(']');
                sb.Append(",\"usage\":").Append(JsonEscape(c.Usage));
                sb.Append(",\"description\":").Append(JsonEscape(c.Description));
                sb.Append(",\"author\":").Append(JsonEscape(c.Author ?? ""));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════
        //  WebUI HTML
        // ═════════════════════════════════════════════════════


        private void HandleAutomationApi(HttpListenerRequest req, HttpListenerResponse resp, string path)
        {
            var config = Plugin.Instance != null ? Plugin.Instance.Config : null;
            if (config == null)
            {
                WriteJson(resp, "{\"ok\":false,\"error\":\"plugin not ready\"}");
                return;
            }

            if (path == "/api/automation/status" && req.HttpMethod == "GET")
            {
                WriteJson(resp, AutomationApi.StatusJson(config));
                return;
            }

            if (path == "/api/automation/host" && req.HttpMethod == "POST")
            {
                WriteJson(resp, AutomationApi.SetHostEnabled(ReadBody(req)));
                return;
            }

            // /api/automation/modules/{id}/log
            const string prefix = "/api/automation/modules/";
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                string rest = path.Substring(prefix.Length);
                int slash = rest.IndexOf('/');
                string id = slash >= 0 ? rest.Substring(0, slash) : rest;
                string tail = slash >= 0 ? rest.Substring(slash) : "";

                if (tail == "/log" && req.HttpMethod == "GET")
                {
                    WriteJson(resp, AutomationApi.GetModuleLog(id));
                    return;
                }
                if (tail == "/log" && req.HttpMethod == "DELETE")
                {
                    WriteJson(resp, AutomationApi.ClearModuleLog(id));
                    return;
                }
                if (tail == "/log/clear" && req.HttpMethod == "POST")
                {
                    WriteJson(resp, AutomationApi.ClearModuleLog(id));
                    return;
                }
            }

            WriteJson(resp, "{\"ok\":false,\"error\":\"unknown automation endpoint\"}");
        }

        private void HandleConfigApi(HttpListenerRequest req, HttpListenerResponse resp, string path)
        {
            var config = Plugin.Instance.Config;

            // 功能段独立日志
            const string secPrefix = "/api/config/section/";
            if (path.StartsWith(secPrefix, StringComparison.Ordinal))
            {
                string rest = path.Substring(secPrefix.Length);
                int slash = rest.IndexOf('/');
                string section = slash >= 0 ? Uri.UnescapeDataString(rest.Substring(0, slash)) : Uri.UnescapeDataString(rest);
                string tail = slash >= 0 ? rest.Substring(slash) : "";
                if (tail == "/log" && req.HttpMethod == "GET")
                {
                    var (seq, lines) = DT_Tools.Core.FeatureLogRegistry.Snapshot(section);
                    var sb = new System.Text.StringBuilder();
                    sb.Append("{\"ok\":true,\"section\":").Append(JsonEscape(section));
                    sb.Append(",\"seq\":").Append(seq);
                    sb.Append(",\"lines\":[");
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(JsonEscape(lines[i]));
                    }
                    sb.Append("]}");
                    WriteJson(resp, sb.ToString());
                    return;
                }
                if ((tail == "/log/clear" && req.HttpMethod == "POST") ||
                    (tail == "/log" && req.HttpMethod == "DELETE"))
                {
                    DT_Tools.Core.FeatureLogRegistry.Clear(section);
                    DT_Tools.Core.FeatureLogRegistry.Info(section, "日志已清空");
                    WriteJson(resp, "{\"ok\":true}");
                    return;
                }
            }

            if (path == "/api/config/list" && req.HttpMethod == "GET")
            {
                WriteJson(resp, ConfigApi.ListJson(config));
                return;
            }

            if (path == "/api/config/update" && req.HttpMethod == "POST")
            {
                WriteJson(resp, ConfigApi.HandleUpdate(config, ReadBody(req)));
                return;
            }

            if (path == "/api/config/save" && req.HttpMethod == "POST")
            {
                WriteJson(resp, ConfigApi.HandleSave(config));
                return;
            }

            if (path == "/api/config/reset" && req.HttpMethod == "POST")
            {
                WriteJson(resp, ConfigApi.HandleReset(config, ReadBody(req)));
                return;
            }

            if (path == "/api/config/import" && req.HttpMethod == "POST")
            {
                WriteJson(resp, ConfigApi.HandleImport(config, ReadBody(req)));
                return;
            }

            if (path == "/api/config/export.cfg" && req.HttpMethod == "GET")
            {
                string cfg = ConfigApi.ExportCfg(config);
                var bytes = Encoding.UTF8.GetBytes(cfg);
                resp.StatusCode = 200;
                resp.ContentType = "text/plain; charset=utf-8";
                resp.AddHeader("Content-Disposition", "attachment; filename=\"DT_Tools.cfg\"");
                resp.ContentLength64 = bytes.Length;
                resp.OutputStream.Write(bytes, 0, bytes.Length);
                resp.OutputStream.Close();
                return;
            }

            resp.StatusCode = 404;
            WriteText(resp, "Not Found");
        }


        // ═════════════════════════════════════════════════════
        //  HTTP 工具
        // ═════════════════════════════════════════════════════

        private static string ReadBody(HttpListenerRequest req)
        {
            using var sr = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
            return sr.ReadToEnd();
        }

        private static void WriteHtml(HttpListenerResponse resp, string html, int code = 200)
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            resp.StatusCode = code;
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }

        private static void WriteJson(HttpListenerResponse resp, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            resp.StatusCode = 200;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }

        private static void WriteText(HttpListenerResponse resp, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            resp.StatusCode = 200;
            resp.ContentType = "text/plain; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }

        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════
        //  生命周期
        // ═════════════════════════════════════════════════════

        private void OnDestroy()
        {
            StopServer();
            if (Instance == this) Instance = null;
        }

        // ─── 内部类型 ─────────────────────────────────────────
        private struct LogEntry
        {
            public int      Seq;
            public DateTime Time;
            public LogLevel Level;
            public string   Message;
        }

        /// <summary>
        /// 命令队列元素。Done 为 null 表示 WebUI fire-and-forget；
        /// 非 null 时由主线程执行完后写入 ResultJson 并 Set。
        /// </summary>
        private sealed class PendingRequest
        {
            public string              Command;
            public ManualResetEventSlim Done;
            public string              ResultJson;
        }
    }
}
