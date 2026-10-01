using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// WebSocket 实时流的自管 TCP 通道。
    ///
    /// 背景：Unity(Mono) 的 HttpListener.AcceptWebSocketAsync 未实现（运行时探测见
    /// LogStreamApi._wsCapability），HTTP 端口上的 WebSocket 升级永远 501，前端只能
    /// 降级轮询。本类在独立端口（WebConsoleOptions.WsPort，0=禁用）用 TcpListener
    /// 自行完成 RFC6455 握手（101 + Sec-WebSocket-Accept），随后交给
    /// WebSocket.CreateFromStream（netstandard2.1 纯托管帧实现，isServer:true 要求
    /// 调用方先行完成握手）——握手后的注册/重放/心跳/慢消费保护与 HTTP 路径共用
    /// LogStreamApi 的同一套客户端逻辑。前端经 GET /api/meta 发现端口后直连本通道。
    ///
    /// 线程模型与 HttpServer 一致：监听线程只接受连接，每连接派发线程池并阻塞到
    /// 连接结束；鉴权语义与 HTTP 相同（密码非空时凭 dt_token cookie 或 ?token= 查询
    /// 串，Auth.CheckToken 常量时间比较）。运行时若连 CreateFromStream 也不可用
    /// （更老的 Mono），逐连接失败静默关闭，前端按既有逻辑降级轮询——不劣化现状。
    /// </summary>
    internal static class WsServer
    {
        private const int HeadLimit = 16 * 1024;      // 握手请求头上限（正常约 1KB）
        private const int HandshakeTimeoutMs = 5000;  // 握手读写超时：慢连接不长期占用线程
        private const string WsMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const string WsPath = "/api/log/ws";

        private static TcpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;
        private static readonly ConcurrentDictionary<TcpClient, byte> Connections =
            new ConcurrentDictionary<TcpClient, byte>();

        /// <summary>WebConsole.Init 调用；port≤0 表示配置禁用。</summary>
        public static void Start(string listenIp, int port, Auth auth)
        {
            if (port <= 0)
                return;
            try
            {
                _listener = new TcpListener(ResolveIp(listenIp), port);
                _listener.Start();
                _running = true;
                _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "DT_WS_Accept" };
                _thread.Start(auth);
                Log.Info("WebConsole", $"WebSocket 实时流通道已启动（{listenIp}:{port}）——前端经 /api/meta 自动发现端口。");
            }
            catch (Exception ex)
            {
                // 堆栈进 BepInEx 主日志（端口占用等需可诊断）；前端按既有逻辑回退轮询
                Log.Exception("WebConsole", ex, $"WebSocket 通道启动失败（监听 {listenIp}:{port}）——实时流不可用，前端将回退轮询 /api/log");
            }
        }

        /// <summary>WebConsole.OnDestroy：停止接受并断开全部连接（客户端注册表由 LogStreamApi.ShutdownAll 统一收尾）。</summary>
        public static void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { /* 关闭阶段忽略 */ }
            foreach (var tcp in Connections.Keys)
            {
                try { tcp.Close(); } catch { /* 已断开 */ }
            }
        }

        private static void AcceptLoop(object state)
        {
            var auth = (Auth)state;
            while (_running)
            {
                TcpClient tcp;
                try
                {
                    tcp = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    // 监听器已 Stop（对象释放/中断）→ 正常退出；其余瞬时异常稍候重试
                    // （与 HttpServer.Loop 的分类同构；TcpListener 无 IsListening 可查）
                    if (!_running) break;
                    Thread.Sleep(200);
                    continue;
                }
                Connections[tcp] = 0;
                ThreadPool.QueueUserWorkItem(job =>
                {
                    try { HandleClient(tcp, auth); }
                    finally { Connections.TryRemove(tcp, out _); }
                });
            }
        }

        private static void HandleClient(TcpClient tcp, Auth auth)
        {
            try
            {
                tcp.NoDelay = true;
                var stream = tcp.GetStream();
                stream.ReadTimeout = HandshakeTimeoutMs;
                stream.WriteTimeout = HandshakeTimeoutMs;

                if (!TryReadHead(stream, out var head))
                {
                    WriteRaw(stream, "HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
                    return;
                }
                var req = ParseRequest(head);
                if (req == null || req.Path != WsPath)
                {
                    WriteRaw(stream, "HTTP/1.1 404 Not Found\r\nConnection: close\r\n\r\n");
                    return;
                }
                if (!req.Upgrade || string.IsNullOrEmpty(req.Key))
                {
                    WriteRaw(stream, "HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
                    return;
                }
                if (!auth.CheckToken(req.CookieToken, req.QueryValue("token") ?? ""))
                {
                    WriteRaw(stream, "HTTP/1.1 401 Unauthorized\r\nConnection: close\r\n\r\n");
                    return;
                }

                WriteRaw(stream,
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Sec-WebSocket-Accept: " + ComputeAcceptKey(req.Key) + "\r\n\r\n");

                WebSocket ws;
                try
                {
                    // keepAlive 关闭：应用层 15s {"t":"ping"} 心跳已承担探活（WsClient.SenderLoop），
                    // 不与托管实现的协议层 ping 争抢发送状态
                    ws = WebSocket.CreateFromStream(stream, true, null, Timeout.InfiniteTimeSpan);
                }
                catch (Exception ex)
                {
                    // 极老 Mono 连 CreateFromStream 都没有：静默关闭，前端照旧降级轮询（不劣化现状）
                    Log.Warn("WebConsole", $"WebSocket.CreateFromStream 不可用，独立实时流通道关闭该连接：{ex.GetType().Name}: {ex.Message}");
                    return;
                }

                long.TryParse(req.QueryValue("since"), out var since);
                Api.LogStreamApi.HandleSocket(ws, since);
            }
            catch (WebSocketException)
            {
                // 帧层异常 = 客户端断开/网络中断的常规收尾，无需告警
            }
            catch (Exception ex)
            {
                Log.Warn("WebConsole", $"WebSocket 连接异常结束：{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                try { tcp.Close(); } catch { /* 已断开 */ }
            }
        }

        /// <summary>读取握手请求头（直到空行）；超时/断开/超限返回 false。</summary>
        private static bool TryReadHead(NetworkStream stream, out string head)
        {
            head = null;
            var buf = new byte[HeadLimit];
            int total = 0;
            while (total < HeadLimit)
            {
                int n = stream.Read(buf, total, HeadLimit - total);
                if (n <= 0)
                    return false;   // 客户端在握手完成前断开
                total += n;
                var text = Encoding.UTF8.GetString(buf, 0, total);
                int end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (end >= 0)
                {
                    head = text.Substring(0, end);
                    return true;
                }
            }
            return false;
        }

        private static void WriteRaw(NetworkStream stream, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        // ---- 握手请求解析（仅本通道使用的最小子集）----

        private sealed class WsRequest
        {
            public string Path;
            public string Query;
            public string Key;
            public string CookieToken;
            public bool Upgrade;   // Connection: upgrade 且 Upgrade: websocket

            public string QueryValue(string name)
            {
                if (string.IsNullOrEmpty(Query)) return null;
                foreach (var pair in Query.Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    string k = eq >= 0 ? pair.Substring(0, eq) : pair;
                    if (!string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) continue;
                    string v = eq >= 0 ? pair.Substring(eq + 1) : "";
                    try { return Uri.UnescapeDataString(v); }
                    catch (FormatException) { return v; }
                }
                return null;
            }
        }

        private static WsRequest ParseRequest(string head)
        {
            string[] lines = head.Replace('\r', '\n').Split('\n');
            if (lines.Length == 0)
                return null;
            string[] parts = lines[0].Split(' ');
            if (parts.Length < 2 || !parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase))
                return null;
            string target = parts[1];
            int q = target.IndexOf('?');
            var req = new WsRequest
            {
                Path = q >= 0 ? target.Substring(0, q) : target,
                Query = q >= 0 ? target.Substring(q + 1) : null,
            };
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                    req.Upgrade |= value.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) >= 0;
                else if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
                    req.Upgrade |= value.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0;
                else if (name.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                    req.Key = value;
                else if (name.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                    req.CookieToken = ExtractCookie(value, "dt_token");
            }
            return req;
        }

        private static string ExtractCookie(string cookieHeader, string name)
        {
            foreach (var piece in cookieHeader.Split(';'))
            {
                string item = piece.Trim();
                if (!item.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) continue;
                string v = item.Substring(name.Length + 1).Trim('"');
                return v;
            }
            return "";
        }

        /// <summary>RFC6455 握手接受键：Base64(SHA1(key + GUID))。</summary>
        private static string ComputeAcceptKey(string key)
        {
            using (var sha = SHA1.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + WsMagic)));
        }

        /// <summary>
        /// 监听地址解析：解释口径统一走 HttpServer.NormalizeIp（唯一权威，消除两份同构归一化），
        /// 这里只做字符串 → 套接字地址的转换（"*"/localhost 特判、IPv6 去方括号）。
        /// </summary>
        private static IPAddress ResolveIp(string raw)
        {
            string ip = HttpServer.NormalizeIp(raw);
            if (ip == "*") return IPAddress.Any;
            if (ip.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;
            string bare = ip.StartsWith("[") ? ip.Trim('[', ']') : ip;
            return IPAddress.TryParse(bare, out var parsed) ? parsed : IPAddress.Loopback;
        }
    }
}
