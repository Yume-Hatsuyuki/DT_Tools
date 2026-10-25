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
    internal static class WsServer
    {
        private const int HeadLimit = 16 * 1024;
        private const int HandshakeTimeoutMs = 5000;
        private const string WsMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const string WsPath = "/api/log/ws";

        private static TcpListener _listener;
        private static string[] _localWhitelist = new string[0];
        private static string[] _allowedHosts = new string[0];
        private static Thread _thread;
        private static volatile bool _running;
        private static readonly ConcurrentDictionary<TcpClient, byte> Connections =
            new ConcurrentDictionary<TcpClient, byte>();

        public static void Start(string listenIp, int port, Auth auth)
        {
            if (port <= 0)
                return;
            try
            {
                _localWhitelist = HostPolicy.LocalWhitelist(listenIp);
                _allowedHosts = HostPolicy.ParseAllowed(WebConsoleOptions.AllowedHosts);
                _listener = new TcpListener(ResolveIp(listenIp), port);
                _listener.Start();
                _running = true;
                _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "DT_WS_Accept" };
                _thread.Start(auth);
                Log.Info("WebConsole", $"WebSocket 实时流通道已启动（{listenIp}:{port}）——前端经 /api/meta 自动发现端口。");
            }
            catch (Exception ex)
            {
                Log.Exception("WebConsole", ex, $"WebSocket 通道启动失败（监听 {listenIp}:{port}）——实时流不可用，前端将回退轮询 /api/log");
            }
        }

        public static void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            foreach (var tcp in Connections.Keys)
            {
                try { tcp.Close(); } catch { }
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
                if (!HostPolicy.IsAllowedHost(req.Host, _localWhitelist, _allowedHosts)
                    || !HostPolicy.IsAllowedWsOrigin(req.Origin, req.Host, WebConsoleOptions.Port, _allowedHosts))
                {
                    Log.Warn("WebConsole", "已拒绝 WebSocket 握手：Host 或 Origin 不在允许范围内（Host=" + (req.Host ?? "无") + "）。");
                    WriteRaw(stream, "HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n");
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
                    ws = WebSocket.CreateFromStream(stream, true, null, Timeout.InfiniteTimeSpan);
                }
                catch (Exception ex)
                {
                    Log.Warn("WebConsole", $"WebSocket.CreateFromStream 不可用，独立实时流通道关闭该连接：{ex.GetType().Name}: {ex.Message}");
                    return;
                }

                long.TryParse(req.QueryValue("since"), out var since);
                Api.LogStreamApi.HandleSocket(ws, since);
            }
            catch (WebSocketException)
            {
            }
            catch (Exception ex)
            {
                Log.Warn("WebConsole", $"WebSocket 连接异常结束：{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                try { tcp.Close(); } catch { }
            }
        }

        private static bool TryReadHead(NetworkStream stream, out string head)
        {
            head = null;
            var buf = new byte[HeadLimit];
            int total = 0;
            while (total < HeadLimit)
            {
                int n = stream.Read(buf, total, HeadLimit - total);
                if (n <= 0)
                    return false;
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

        private sealed class WsRequest
        {
            public string Path;
            public string Query;
            public string Key;
            public string CookieToken;
            public string Host;
            public string Origin;
            public bool Upgrade;

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
                if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    req.Host = value;
                else if (name.Equals("Origin", StringComparison.OrdinalIgnoreCase))
                    req.Origin = value;
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

        private static string ComputeAcceptKey(string key)
        {
            using (var sha = SHA1.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + WsMagic)));
        }

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
