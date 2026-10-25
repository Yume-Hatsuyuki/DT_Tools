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
    public sealed class WebConsole : MonoBehaviour
    {
        public static WebConsole Instance { get; private set; }

        private const int RunTimeoutMs = 5000;

        private readonly Queue<PendingRequest> _pending = new Queue<PendingRequest>();
        private readonly object _pendLock = new object();
        private HttpServer _server;

        public static void Post(Action action)
        {
            if (Instance == null || action == null) return;
            CoroutineHost.Post(action);
        }

        public static CommandResult RunOnMain(Func<CommandResult> work, int timeoutMs = RunTimeoutMs)
        {
            if (work == null) return CommandResult.Fail("empty action");
            var self = Instance;
            if (self == null) return CommandResult.Fail("webconsole offline");

            var done = new ManualResetEventSlim(false);
            CommandResult result = null;
            bool timedOut = false;
            CoroutineHost.Post(() =>
            {
                if (timedOut)
                    Log.Warn("WebConsole", "主线程动作在超时回包后仍被延迟执行（如 /api/dummy/create 的写操作可能已实际生效）——结果与用户预期可能背离。");
                DeferredCompletion.Begin(r =>
                {
                    result = r;
                    if (timedOut)
                        Log.Warn("WebConsole", "延迟完成的结果在超时回包后到达，已丢弃。");
                    try { done.Set(); }
                    catch (ObjectDisposedException) { }
                });
                try { result = work(); }
                catch (Exception ex)
                {
                    Log.Exception("WebConsole", ex, "主线程动作执行异常");
                    result = CommandResult.Fail("action error");
                }
                finally { DeferredCompletion.End(); }
                if (result != null && result.IsDeferred)
                    return;
                try { done.Set(); }
                catch (ObjectDisposedException) { }
            });

            if (!done.Wait(timeoutMs))
            {
                timedOut = true;
                Log.Warn("WebConsole", $"主线程动作 {timeoutMs / 1000} 秒未被执行（主线程卡顿或失焦停摆？）");
                return CommandResult.Fail("timeout");
            }
            done.Dispose();
            return result ?? CommandResult.Fail("action error");
        }

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
            var router = new Router(auth, WebConsoleOptions.ListenIp);
            RegisterRoutes(router, auth);

            _server = new HttpServer(router);
            _server.Start(WebConsoleOptions.ListenIp, WebConsoleOptions.Port);
            WsServer.Start(WebConsoleOptions.ListenIp, WebConsoleOptions.WsPort, auth);
            Log.EntryAppended += Api.LogStreamApi.Broadcast;
            ApplyRuntimeToggles();
        }

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
            router.Add("GET", "/api/log/ws", Api.LogStreamApi.Handle);
            router.Add("GET", "/api/log", Api.LogsApi.Handle);
            router.Add("GET", "/api/commands", Api.CommandsApi.HandleList);
            router.Add("*", "/api/config/section/", Api.ConfigApi.HandleSectionLog);
            router.Add("GET", "/api/config/list", Api.ConfigApi.HandleList);
            router.Add("GET", "/api/config/tags", Api.ConfigApi.HandleTags);
            router.Add("POST", "/api/config/update", Api.ConfigApi.HandleUpdate);
            router.Add("POST", "/api/config/save", Api.ConfigApi.HandleSave);
            router.Add("POST", "/api/config/reset", Api.ConfigApi.HandleReset);
            router.Add("POST", "/api/config/import", Api.ConfigApi.HandleImport);
            router.Add("GET", "/api/config/export.cfg", Api.ConfigApi.HandleExport);
            router.Add("GET", "/api/automation/status", Api.AutomationApi.HandleStatus);
            router.Add("POST", "/api/automation/host", Api.AutomationApi.HandleHost);
            router.Add("*", "/api/automation/modules/", Api.AutomationApi.HandleModule);
            router.Add("GET", "/api/steam/players", Api.SteamApi.Handle);
            router.Add("GET", "/api/steam/news", Api.SteamNewsApi.Handle);
            router.Add("GET", "/api/update/status", Api.UpdateApi.HandleStatus);
            router.Add("POST", "/api/update/check", Api.UpdateApi.HandleCheck);
            router.Add("POST", "/api/update/download", Api.UpdateApi.HandleDownload);
            router.Add("POST", "/api/update/reveal", Api.UpdateApi.HandleReveal);
            router.Add("GET", "/api/fs/list", Api.FsApi.HandleList);
            router.Add("POST", "/api/mp3/playlist/add", Api.Mp3Api.HandlePlaylistAdd);
            router.Add("POST", "/api/mp3/playlist/remove", Api.Mp3Api.HandlePlaylistRemove);
            router.Add("POST", "/api/mp3/playlist/move", Api.Mp3Api.HandlePlaylistMove);
            router.Add("POST", "/api/mp3/playlist/clear", Api.Mp3Api.HandlePlaylistClear);
            router.Add("POST", "/api/mp3/playlist/alias", Api.Mp3Api.HandlePlaylistAlias);
            router.Add("POST", "/api/mp3/playlist/update", Api.Mp3Api.HandlePlaylistUpdate);
            router.Add("POST", "/api/mp3/netease/info", Api.Mp3Api.HandleNeteaseInfo);
            router.Add("GET", "/api/mp3/netease/settings", Api.Mp3Api.HandleNeteaseSettings);
            router.Add("POST", "/api/mp3/netease/settings", Api.Mp3Api.HandleNeteaseSettingsSet);
            router.Add("POST", "/api/mp3/lists/create", Api.Mp3Api.HandleListCreate);
            router.Add("POST", "/api/mp3/lists/rename", Api.Mp3Api.HandleListRename);
            router.Add("POST", "/api/mp3/lists/delete", Api.Mp3Api.HandleListDelete);
            router.Add("POST", "/api/mp3/lists/select", Api.Mp3Api.HandleListSelect);
            router.Add("GET", "/api/gamepad/state", Api.GamepadApi.HandleState);
            router.Add("POST", "/api/gamepad/bind", Api.GamepadApi.HandleBind);
            router.Add("POST", "/api/gamepad/toggle", Api.GamepadApi.HandleToggle);
            router.Add("POST", "/api/gamepad/reset", Api.GamepadApi.HandleReset);
            router.Add("GET", "/api/tablet/state", Api.WebTabletApi.HandleState);
            router.Add("POST", "/api/tablet/send", Api.WebTabletApi.HandleSend);
            router.Add("POST", "/api/tablet/photo", Api.WebTabletApi.HandlePhotoUpload);
            router.Add("GET", "/api/tablet/photo", Api.WebTabletApi.HandlePhotoGet);
            router.Add("POST", "/api/tablet/clear", Api.WebTabletApi.HandleClear);
            router.Add("GET", "/api/mp3/state", Api.Mp3Api.HandleState);
            router.Add("POST", "/api/mp3/play", Api.Mp3Api.HandlePlay);
            router.Add("POST", "/api/mp3/index", Api.Mp3Api.HandleIndex);
            router.Add("POST", "/api/mp3/next", Api.Mp3Api.HandleNext);
            router.Add("POST", "/api/mp3/pause", Api.Mp3Api.HandlePause);
            router.Add("POST", "/api/mp3/resume", Api.Mp3Api.HandleResume);
            router.Add("POST", "/api/mp3/seek", Api.Mp3Api.HandleSeek);
            router.Add("POST", "/api/mp3/volume", Api.Mp3Api.HandleVolume);
            router.Add("POST", "/api/mp3/mic", Api.Mp3Api.HandleMic);
            router.Add("POST", "/api/mp3/local", Api.Mp3Api.HandleLocal);
            router.Add("POST", "/api/mp3/mode", Api.Mp3Api.HandleMode);
            router.Add("POST", "/api/mp3/stop", Api.Mp3Api.HandleStop);
            router.Add("POST", "/api/dummy/remove-all", Api.DummyApi.HandleRemoveAll);
            router.Add("GET", "/api/dummy/state", Api.DummyApi.HandleState);
            router.Add("GET", "/api/dummy/characters", Api.DummyApi.HandleCharacters);
            router.Add("POST", "/api/dummy/create", Api.DummyApi.HandleCreate);
            router.Add("POST", "/api/dummy/remove", Api.DummyApi.HandleRemove);
            router.Add("POST", "/api/dummy/ready", Api.DummyApi.HandleReady);
            router.Add("POST", "/api/dummy/pick", Api.DummyApi.HandlePick);
            router.Add("GET", "/api/meta", Api.SystemApi.HandleMeta);
            router.Add("POST", "/api/game/exit", Api.SystemApi.HandleExit);

            Mcp.McpServer.RegisterRoutes(router);
        }

        private void OnDestroy()
        {
            Log.EntryAppended -= Api.LogStreamApi.Broadcast;
            Api.LogStreamApi.ShutdownAll();
            WsServer.Stop();
            _server?.Stop();
            if (Instance == this) Instance = null;
        }

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
                Session = ctx.Request.Headers["X-DT-Session"],
                Done = new ManualResetEventSlim(false),
            };
            lock (_pendLock)
                _pending.Enqueue(pending);

            if (pending.Done.Wait(RunTimeoutMs))
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Success(pending.Result));
            }
            else
            {
                Log.Warn("WebConsole", $"命令 {RunTimeoutMs / 1000} 秒未被执行（主线程卡顿或失焦停摆？）：{pending.Command}");
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("timeout"));
            }

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
            string session = pending.Session;
            if (!string.IsNullOrEmpty(session))
                CommandSession.Begin(session);
            DeferredCompletion.Begin(r => Complete(pending, r));
            try
            {
                Complete(pending, ExecuteCommandText(raw));
            }
            catch (Exception ex)
            {
                Log.Exception("WebConsole", ex, $"命令主线程执行异常: {raw}");
                Complete(pending, CommandResult.Fail("command error"));
            }
            finally
            {
                DeferredCompletion.End();
                if (!string.IsNullOrEmpty(session))
                    CommandSession.End();
            }
        }

        internal static CommandResult ExecuteCommandText(string raw)
        {
            if (raw.StartsWith("/") || raw.StartsWith("!"))
                raw = raw.Substring(1);

            if (string.IsNullOrWhiteSpace(raw))
                return CommandResult.Fail("empty command");

            var parts = CommandTokenizer.Tokenize(raw);
            if (!CommandRegistry.TryGet(parts[0], out var command))
            {
                Log.Warn("WebConsole", $"未知命令: {parts[0]}");
                return CommandResult.Fail("unknown command");
            }

            string[] args = parts.Length > 1
                ? parts.Skip(1).ToArray()
                : Array.Empty<string>();

            return CommandRegistry.Execute(command, args);
        }

        private static void Complete(PendingRequest pending, CommandResult result)
        {
            if (result != null && result.IsDeferred)
                return;
            pending.Result = result;
            try
            {
                pending.Done?.Set();
            }
            catch (ObjectDisposedException)
            {
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
