using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// GET /api/log/ws — WebSocket 日志流。
    ///
    /// 协议（单向推送，客户端消息一律忽略）：
    ///   握手后先重放环形缓冲中 seq &gt; since 的条目（?since=N，缺省 0），
    ///   之后实时增量。每帧 = JSON 数组 [{seq,time,level,tag,msg,session}]；
    ///   15 秒无增量发心跳 {"t":"ping"}。
    ///
    /// 无缝隙衔接：先注册进广播表、再取快照重放，发送时按 lastSentSeq 去重——
    /// 注册与重放之间落盘的条目不会漏、重放与实时的交界不会重（旧 SSE 无 since
    /// 无去重，断线重连全量重放是已确认缺陷）。
    ///
    /// 线程模型：握手在 HTTP 请求线程上完成并阻塞该线程直到连接结束（每连接占用
    /// 一个请求线程）；发送由每连接独立的发送线程驱动（队列泵 + AutoResetEvent），
    /// 广播方（Log.EntryAppended，主线程/HTTP 线程）只入队绝不阻塞——慢消费超过
    /// 上限直接断开该客户端。鉴权由 Router 在分发前统一完成（未授权握手收 401）。
    /// </summary>
    public static class LogStreamApi
    {
        private const int HeartbeatMs = 15000;
        private const int SlowConsumerCap = 1024;   // 未发送帧上限：超过即断开慢客户端

        private static readonly ConcurrentDictionary<WsClient, bool> Clients =
            new ConcurrentDictionary<WsClient, bool>();

        /// <summary>
        /// 运行时 WS 能力探测：0=未知 1=可用 2=不支持。Unity 的 Mono 对
        /// HttpListener.AcceptWebSocketAsync 抛 NotImplementedException（实机确认）——
        /// 探测一次后：不支持时后续握手静默回 501（前端自动降级轮询 /api/log），
        /// 避免前端每次重连都打一条警告刷屏 BepInEx。
        /// </summary>
        private static int _wsCapability;

        /// <summary>WebConsole.Init 订阅到 Log.EntryAppended：只入队，绝不阻塞发出线程。</summary>
        public static void Broadcast(LogEntry entry)
        {
            foreach (var client in Clients.Keys)
                client.Enqueue(entry);
        }

        /// <summary>WebConsole.OnDestroy：关闭全部客户端连接。</summary>
        public static void ShutdownAll()
        {
            foreach (var client in Clients.Keys)
                client.Close();
        }

        public static void Handle(HttpListenerContext ctx)
        {
            if (Volatile.Read(ref _wsCapability) == 2)
            {
                HttpServer.WriteText(ctx.Response, 501, "websocket unsupported on this runtime");
                return;
            }

            WebSocket ws;
            try
            {
                // 项目禁 async/await 语法；专用请求线程上同步等待握手结果
                var wsCtx = ctx.AcceptWebSocketAsync(null).GetAwaiter().GetResult();
                ws = wsCtx.WebSocket;
                Volatile.Write(ref _wsCapability, 1);
            }
            catch (NotImplementedException)
            {
                if (Interlocked.CompareExchange(ref _wsCapability, 2, 0) == 0)
                    Log.Warn("WebConsole", "当前 Mono 运行时不支持 HttpListener WebSocket 升级（AcceptWebSocketAsync 未实现）——实时流不可用，前端已自动降级为轮询 /api/log（约 0.8s 增量，功能等价）。此提示只出现一次。");
                HttpServer.WriteText(ctx.Response, 501, "websocket unsupported on this runtime");
                return;
            }
            catch (Exception ex)
            {
                Log.Warn("WebConsole", $"WebSocket 握手失败: {ex.Message}");
                try { ctx.Response.Abort(); } catch { /* 已断开 */ }
                return;
            }

            long.TryParse(ctx.Request.QueryString["since"], out var since);
            RunClient(ws, since);
        }

        /// <summary>
        /// WsServer（独立端口自管 TCP 通道）完成 RFC6455 握手后的共用入口：
        /// 与 HTTP 升级路径共享同一份客户端注册/重放/心跳逻辑。
        /// 阻塞调用线程直到连接结束（每连接占用一个线程池线程，与 HTTP 路径同构）。
        /// </summary>
        internal static void HandleSocket(WebSocket ws, long since)
            => RunClient(ws, since);

        private static void RunClient(WebSocket ws, long since)
        {
            var client = new WsClient(ws, since);
            Clients[client] = true;
            try
            {
                client.Serve();   // 阻塞当前线程直到连接结束
            }
            finally
            {
                Clients.TryRemove(client, out _);
            }
        }

        private sealed class WsClient
        {
            private readonly WebSocket _ws;
            private readonly long _since;
            private readonly Queue<LogEntry> _outbox = new Queue<LogEntry>();
            private readonly object _outLock = new object();
            private readonly AutoResetEvent _signal = new AutoResetEvent(false);
            private long _lastSentSeq;
            private volatile bool _closed;

            internal WsClient(WebSocket ws, long since)
            {
                _ws = ws;
                _since = since;
            }

            internal void Enqueue(LogEntry entry)
            {
                if (_closed) return;
                lock (_outLock)
                {
                    if (_outbox.Count >= SlowConsumerCap)
                    {
                        _closed = true;   // 慢消费保护：断开而不是无限积压
                        _signal.Set();
                        return;
                    }
                    _outbox.Enqueue(entry);
                }
                _signal.Set();
            }

            internal void Close()
            {
                _closed = true;
                _signal.Set();
            }

            /// <summary>连接主体：注册后重放快照，随后在当前线程跑接收循环（探测断开），
            /// 发送交给独立线程。返回即连接结束。</summary>
            internal void Serve()
            {
                var sender = new Thread(SenderLoop) { IsBackground = true, Name = "DT_WS_Send" };
                sender.Start();
                try
                {
                    ReceiveLoop();
                }
                finally
                {
                    _closed = true;
                    _signal.Set();
                    sender.Join(2000);
                    try
                    {
                        if (_ws.State != WebSocketState.Closed && _ws.State != WebSocketState.Aborted)
                            _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                                .GetAwaiter().GetResult();
                    }
                    catch { /* 已断开 */ }
                    _ws.Dispose();
                }
            }

            private void ReceiveLoop()
            {
                var buf = new byte[8192];
                while (!_closed && _ws.State == WebSocketState.Open)
                {
                    var result = _ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None)
                        .GetAwaiter().GetResult();
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _closed = true;
                        _signal.Set();
                    }
                }
            }

            private void SenderLoop()
            {
                // 注册先行、重放在后、发送按 seq 去重：与广播表并发加入的条目不漏不重
                long since;
                lock (_outLock)
                {
                    _lastSentSeq = _since;
                    since = _since;
                }
                var (_, snapshot) = Log.SnapshotEntries();
                var replay = snapshot.Where(e => e.Seq > since).ToList();
                if (replay.Count > 0)
                {
                    if (!SendFrame(replay))
                        return;
                }

                while (!_closed)
                {
                    _signal.WaitOne(HeartbeatMs);
                    if (_closed) return;

                    List<LogEntry> batch;
                    lock (_outLock)
                    {
                        if (_outbox.Count >= SlowConsumerCap)
                        {
                            _closed = true;   // 消费太慢：放弃本连接
                            return;
                        }
                        batch = new List<LogEntry>(_outbox.Count);
                        while (_outbox.Count > 0)
                            batch.Add(_outbox.Dequeue());
                    }

                    if (batch.Count > 0)
                    {
                        if (!SendFrame(batch))
                            return;
                    }
                    else if (!SendPayload("{\"t\":\"ping\"}"))
                    {
                        return;   // 心跳即探活：写失败立刻结束
                    }
                }
            }

            /// <summary>按 seq 去重并发送一批条目；成功返回 true。</summary>
            private bool SendFrame(List<LogEntry> entries)
            {
                var batch = entries
                    .Where(e => e.Seq > _lastSentSeq)
                    .Select(e => new
                    {
                        seq = e.Seq,
                        time = e.Time.ToString("HH:mm:ss"),
                        level = e.Level,
                        tag = e.Tag,
                        msg = e.Message,
                        session = e.Session,
                    })
                    .ToList();
                if (batch.Count == 0)
                    return true;
                if (!SendPayload(Json.To(batch)))
                    return false;
                _lastSentSeq = batch[batch.Count - 1].seq;
                return true;
            }

            private bool SendPayload(string payload)
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(payload);
                    _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    return true;
                }
                catch
                {
                    _closed = true;   // 客户端断开 / 服务停止：正常结束
                    return false;
                }
            }
        }
    }
}
