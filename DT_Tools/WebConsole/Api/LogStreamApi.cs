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
    public static class LogStreamApi
    {
        private const int HeartbeatMs = 15000;
        private const int SlowConsumerCap = 1024;

        private static readonly ConcurrentDictionary<WsClient, bool> Clients =
            new ConcurrentDictionary<WsClient, bool>();

        private static int _wsCapability;

        public static void Broadcast(LogEntry entry)
        {
            foreach (var client in Clients.Keys)
                client.Enqueue(entry);
        }

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
                try { ctx.Response.Abort(); } catch { }
                return;
            }

            long.TryParse(ctx.Request.QueryString["since"], out var since);
            RunClient(ws, since);
        }

        internal static void HandleSocket(WebSocket ws, long since)
            => RunClient(ws, since);

        private static void RunClient(WebSocket ws, long since)
        {
            var client = new WsClient(ws, since);
            Clients[client] = true;
            try
            {
                client.Serve();
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
                        _closed = true;
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
                    catch { }
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
                            _closed = true;
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
                    else if (!SendPayload(Json.To(new { t = "ping" })))
                    {
                        return;
                    }
                }
            }

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
                    _closed = true;
                    return false;
                }
            }
        }
    }
}
