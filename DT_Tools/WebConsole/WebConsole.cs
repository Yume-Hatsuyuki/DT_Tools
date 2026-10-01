using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using BepInEx.Logging;
using DT_Tools.Commands;
using DT_Tools.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// 内嵌 WebUI 控制台组件：装配（Auth/Router/HttpServer）+ 命令主线程队列。
    /// 线程模型：/api/run 工作线程入队并同步等待（≤5s），Update() 在主线程消费执行，
    /// 命令因此可以安全访问 Unity API 与游戏单例。
    /// </summary>
    public sealed class WebConsole : MonoBehaviour
    {
        public static WebConsole Instance { get; private set; }

        private const int RunTimeoutMs = 5000;

        private readonly Queue<PendingRequest> _pending = new Queue<PendingRequest>();
        private readonly Queue<Action> _actions = new Queue<Action>();
        private readonly object _pendLock = new object();
        private HttpServer _server;

        /// <summary>
        /// 供 API 层（HTTP 线程）把需要 Unity API 的动作投递到主线程执行，
        /// 与 /api/run 的命令队列共用同一把锁与 Update() 泵。
        /// </summary>
        public static void Post(Action action)
        {
            var self = Instance;
            if (self == null || action == null) return;
            lock (self._pendLock)
                self._actions.Enqueue(action);
        }

        /// <summary>
        /// 供 API 层在 HTTP 线程同步执行一段需要主线程的工作并取回结果（复用 /api/run 的
        /// 队列 + 超时保护）。成功返回 work 的结果；主线程停摆超时 / WebConsole 未装配时
        /// 返回 Fail。注意：超时后 work 仍会在主线程稍后执行（结果被丢弃），work 必须可重入无害。
        /// </summary>
        public static CommandResult RunOnMain(Func<CommandResult> work, int timeoutMs = RunTimeoutMs)
        {
            if (work == null) return CommandResult.Fail("empty action");
            var self = Instance;
            if (self == null) return CommandResult.Fail("webconsole offline");

            var done = new ManualResetEventSlim(false);
            CommandResult result = null;
            lock (self._pendLock)
                self._actions.Enqueue(() =>
                {
                    try { result = work(); }
                    catch (Exception ex)
                    {
                        Log.Exception("WebConsole", ex, "主线程动作执行异常");
                        result = CommandResult.Fail("action error");
                    }
                    // 竞态防护同 Complete()：HTTP 线程超时后已 Dispose，晚到的 Set() 必须吞掉
                    try { done.Set(); }
                    catch (ObjectDisposedException) { /* 超时路径已回包 Fail("timeout") */ }
                });

            if (!done.Wait(timeoutMs))
            {
                Log.Warn("WebConsole", $"主线程动作 {timeoutMs / 1000} 秒未被执行（主线程卡顿或失焦停摆？）");
                return CommandResult.Fail("timeout");
            }
            done.Dispose();
            return result ?? CommandResult.Fail("action error");
        }

        /// <summary>由 Plugin 在启用 WebConsole 时调用（读取 WebConsoleOptions 已由引擎绑定）。</summary>
        public void Init(ManualLogSource log)
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var auth = new Auth(WebConsoleOptions.Password);
            var router = new Router(auth);
            RegisterRoutes(router, auth);

            _server = new HttpServer(router);
            _server.Start(WebConsoleOptions.ListenIp, WebConsoleOptions.Port);
            // WebSocket 实时流独立通道（Mono 的 HttpListener 不支持 WS 升级）：
            // 自管 TCP 完成握手后与 HTTP 路径共用 LogStreamApi 的客户端逻辑
            WsServer.Start(WebConsoleOptions.ListenIp, WebConsoleOptions.WsPort, auth);
            // 日志实时流：Log 门面每写一条即触发，广播到全部 WebSocket 客户端（只入队不阻塞）
            Log.EntryAppended += Api.LogStreamApi.Broadcast;
            ApplyRuntimeToggles();
        }

        /// <summary>
        /// 远程/后台使用的前置条件：游戏窗口失焦时 Unity 默认可能暂停主线程（玩家循环停摆），
        /// 队列泵随之停转——表现为所有 /api/run 统一 5 秒超时。按配置强制开启后台运行。
        /// 非回环监听 + 空密码时给出暴露面警告。
        /// </summary>
        private void ApplyRuntimeToggles()
        {
            if (WebConsoleOptions.RunInBackground && !Application.runInBackground)
            {
                Application.runInBackground = true;
                Log.Info("WebConsole", "已开启 Unity 后台运行（RunInBackground）：游戏窗口失焦时 WebUI 命令仍会执行。");
            }
            string ip = WebConsoleOptions.ListenIp.Trim();
            bool loopback = ip == "127.0.0.1" || ip == "::1" || ip == "localhost";
            if (!loopback && string.IsNullOrEmpty(WebConsoleOptions.Password))
                Log.Warn("WebConsole", "监听地址为非回环 IP 且未设置访问密码——局域网内任何人都可以操作 WebUI，建议在配置中设置 Password。");
        }

        private void RegisterRoutes(Router router, Auth auth)
        {
            router.Add("POST", "/api/run", HandleRun);
            router.Add("GET", "/api/log/ws", Api.LogStreamApi.Handle);   // WebSocket 实时流（先于 /api/log 前缀匹配）
            router.Add("GET", "/api/log", Api.LogsApi.Handle);           // 增量兜底（首屏/断线补齐）
            router.Add("GET", "/api/commands", Api.CommandsApi.HandleList);
            router.Add("*", "/api/config/section/", Api.ConfigApi.HandleSectionLog);
            router.Add("GET", "/api/config/list", Api.ConfigApi.HandleList);
            router.Add("POST", "/api/config/update", Api.ConfigApi.HandleUpdate);
            router.Add("POST", "/api/config/save", Api.ConfigApi.HandleSave);
            router.Add("POST", "/api/config/reset", Api.ConfigApi.HandleReset);
            router.Add("POST", "/api/config/import", Api.ConfigApi.HandleImport);
            router.Add("GET", "/api/config/export.cfg", Api.ConfigApi.HandleExport);
            router.Add("GET", "/api/automation/status", Api.AutomationApi.HandleStatus);
            router.Add("POST", "/api/automation/host", Api.AutomationApi.HandleHost);
            router.Add("*", "/api/automation/modules/", Api.AutomationApi.HandleModule);
            router.Add("GET", "/api/steam/players", Api.SteamApi.Handle);
            router.Add("GET", "/api/pick-file", Api.FilePickerApi.Handle);
            // 假人管理（假人应用）：全部经 RunOnMain 在主线程读写 GameRoom。
            // remove-all 必须注册在 remove 之前——Router 按注册顺序做前缀匹配，
            // 否则 /api/dummy/remove-all 会被更短的 /api/dummy/remove 截走报 invalid body
            router.Add("POST", "/api/dummy/remove-all", Api.DummyApi.HandleRemoveAll);
            router.Add("GET", "/api/dummy/state", Api.DummyApi.HandleState);
            router.Add("GET", "/api/dummy/characters", Api.DummyApi.HandleCharacters);
            router.Add("POST", "/api/dummy/create", Api.DummyApi.HandleCreate);
            router.Add("POST", "/api/dummy/remove", Api.DummyApi.HandleRemove);
            router.Add("POST", "/api/dummy/ready", Api.DummyApi.HandleReady);
            router.Add("POST", "/api/dummy/pick", Api.DummyApi.HandlePick);
            router.Add("GET", "/api/meta", Api.SystemApi.HandleMeta);
            router.Add("POST", "/api/game/exit", Api.SystemApi.HandleExit);
        }

        private void OnDestroy()
        {
            Log.EntryAppended -= Api.LogStreamApi.Broadcast;
            Api.LogStreamApi.ShutdownAll();
            WsServer.Stop();
            _server?.Stop();
            if (Instance == this) Instance = null;
        }

        // ---- /api/run：入队 → 主线程执行 → 回填结果 ----

        private void HandleRun(HttpListenerContext ctx)
        {
            string raw = HttpServer.ReadBody(ctx.Request).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("empty command"));
                return;
            }

            var pending = new PendingRequest
            {
                Command = raw,
                // 会话 id 由前端随 /api/run 头携带：执行期间 Log 写出的条目带 Session，
                // 前端据此把输出隔离回发起命令的那个控制台窗口
                Session = ctx.Request.Headers["X-DT-Session"],
                Done = new ManualResetEventSlim(false),
            };
            lock (_pendLock)
                _pending.Enqueue(pending);

            // 超时保护：主线程卡死时不无限挂起 HTTP 线程
            if (pending.Done.Wait(RunTimeoutMs))
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Success(pending.Result));
            }
            else
            {
                // 超时必留痕：最常见原因是游戏窗口失焦后主线程停摆（RunInBackground 被关）
                Log.Warn("WebConsole", $"命令 {RunTimeoutMs / 1000} 秒未被执行（主线程卡顿或失焦停摆？）：{pending.Command}");
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("timeout"));
            }

            pending.Done.Dispose();
        }

        private void Update()
        {
            while (true)
            {
                PendingRequest pending = null;
                Action action = null;
                lock (_pendLock)
                {
                    if (_pending.Count == 0 && _actions.Count == 0) break;
                    if (_pending.Count > 0)
                    {
                        pending = _pending.Dequeue();
                    }
                    else
                    {
                        action = _actions.Dequeue();
                    }
                }
                if (action != null)
                {
                    // 锁外执行（与命令路径同构）：动作耗时不能堵住 HTTP 线程的入队锁
                    action();
                    continue;
                }
                ExecuteOnMainThread(pending);
            }
        }

        private void ExecuteOnMainThread(PendingRequest pending)
        {
            string raw = pending.Command ?? "";
            string session = pending.Session;
            if (!string.IsNullOrEmpty(session))
                CommandSession.Begin(session);
            try
            {
                // 去掉前导 / 或 !
                if (raw.StartsWith("/") || raw.StartsWith("!"))
                    raw = raw.Substring(1);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    Complete(pending, CommandResult.Fail("empty command"));
                    return;
                }

                var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (!CommandRegistry.TryGet(parts[0], out var command))
                {
                    Log.Warn("WebConsole", $"未知命令: {parts[0]}");
                    Complete(pending, CommandResult.Fail("unknown command"));
                    return;
                }

                string[] args = parts.Length > 1
                    ? parts.Skip(1).ToArray()
                    : Array.Empty<string>();

                Complete(pending, CommandRegistry.Execute(command, args));
            }
            catch (Exception ex)
            {
                // 泵内未预期异常不能吞掉挂起请求——否则 HTTP 线程必然白等 5 秒超时
                Log.Exception("WebConsole", ex, $"命令主线程执行异常: {raw}");
                Complete(pending, CommandResult.Fail("command error"));
            }
            finally
            {
                if (!string.IsNullOrEmpty(session))
                    CommandSession.End();
            }
        }

        private static void Complete(PendingRequest pending, CommandResult result)
        {
            pending.Result = result;
            // 竞态防护：HTTP 线程 Wait(5000) 超时后已 Dispose 掉 Done，而主线程卡顿（如加载
            // 场景）时命令仍会稍后执行完毕，这里晚到的 Set() 若不吞 ObjectDisposedException，
            // 异常会抛在 Unity 主线程 Update()（无 try/catch），中断当帧队列后续项。
            try
            {
                pending.Done?.Set();
            }
            catch (ObjectDisposedException)
            {
                // 超时路径已回包 Fail("timeout")，无需处理
            }
        }

        private sealed class PendingRequest
        {
            public string Command;
            public string Session;
            public ManualResetEventSlim Done;
            public CommandResult Result;
        }
    }
}
