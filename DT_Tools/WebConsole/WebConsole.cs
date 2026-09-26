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
        private readonly object _pendLock = new object();
        private HttpServer _server;

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
            _server.Start(WebConsoleOptions.Port);
        }

        private void RegisterRoutes(Router router, Auth auth)
        {
            router.Add("POST", "/api/run", HandleRun);
            router.Add("GET", "/api/log/stream", Api.LogsApi.HandleStream);   // 必须先于 /api/log（前缀匹配）
            router.Add("GET", "/api/log", Api.LogsApi.Handle);
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
        }

        private void OnDestroy()
        {
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

            var pending = new PendingRequest { Command = raw, Done = new ManualResetEventSlim(false) };
            lock (_pendLock)
                _pending.Enqueue(pending);

            // 超时保护：主线程卡死时不无限挂起 HTTP 线程
            if (pending.Done.Wait(RunTimeoutMs))
                HttpServer.WriteJson(ctx.Response, CommandResult.Success(pending.Result));
            else
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("timeout"));

            pending.Done.Dispose();
        }

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
                ExecuteOnMainThread(pending);
            }
        }

        private void ExecuteOnMainThread(PendingRequest pending)
        {
            string raw = pending.Command ?? "";

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
            public ManualResetEventSlim Done;
            public CommandResult Result;
        }
    }
}
