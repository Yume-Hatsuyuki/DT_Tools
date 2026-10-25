using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.WebConsole
{
    public sealed class HttpServer
    {
        private readonly Router _router;
        private readonly string _threadName;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public HttpServer(Router router, string threadName = "DT_WebConsole")
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _threadName = threadName;
        }

        public void Start(string listenIp, int port)
        {
            string ip = NormalizeIp(listenIp);
            string primary = $"http://{ip}:{port}/";

            string companion = ip == "[::]" ? $"http://*:{port}/"
                : ip.StartsWith("[") ? $"http://127.0.0.1:{port}/"
                : null;

            if (TryStart(new[] { primary, companion }, out var error))
            {
                Log.Info("WebConsole", $"已启动 → {primary}" + (companion != null ? $"（补 IPv4 → {companion}）" : ""));
                return;
            }

            if (companion == null)
            {
                Log.Exception("WebConsole", error, $"启动失败（监听 {listenIp}:{port}）");
                return;
            }

            Log.Warn("WebConsole", $"IPv6+IPv4 双前缀监听失败（{error.Message}），降级为仅 {companion}");
            if (!TryStart(new[] { companion }, out error))
                Log.Exception("WebConsole", error, $"启动失败（监听 {listenIp}:{port}）");
            else
                Log.Info("WebConsole", $"已启动 → {companion}（IPv6 前缀未生效）");
        }

        private bool TryStart(string[] prefixes, out Exception error)
        {
            error = null;
            _listener = new HttpListener();
            foreach (string prefix in prefixes)
            {
                if (prefix == null) continue;
                _listener.Prefixes.Add(prefix);
            }
            try
            {
                _listener.Start();
                _running = true;
                _thread = new Thread(Loop) { IsBackground = true, Name = _threadName };
                _thread.Start();
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                _running = false;
                try { _listener.Close(); } catch { }
                _listener = null;
                return false;
            }
        }

        public static string NormalizeIp(string raw)
        {
            string ip = (raw ?? "").Trim();
            if (ip.Length == 0
                || ip.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase)
                || ip.Equals("any", StringComparison.OrdinalIgnoreCase)
                || ip == "*")
                return "*";
            if (ip.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return "localhost";
            if (System.Net.IPAddress.TryParse(ip, out var parsed))
                return parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                    ? $"[{ip}]"
                    : ip;
            Log.Warn("WebConsole", $"ListenIp 无效: \"{raw}\"，回退 127.0.0.1");
            return "127.0.0.1";
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        private void Loop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch (HttpListenerException hle)
                {
                    if (!_running || !_listener.IsListening)
                    {
                        if (_running)
                            Log.Warn("WebConsole", $"监听循环中断，停止接受请求：{hle.ErrorCode} {hle.Message}");
                        break;
                    }
                    Thread.Sleep(200);
                    continue;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Log.Warn("WebConsole", $"GetContext 异常（监听继续）：{ex.GetType().Name}: {ex.Message}");
                    Thread.Sleep(200);
                    continue;
                }

                var captured = ctx;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        _router.Dispatch(captured);
                    }
                    catch (Exception ex)
                    {
                        try { Log.Warn("WebConsole", $"请求处理错误: {ex.Message}"); }
                        catch { }
                        try { captured.Response.Abort(); } catch { }
                    }
                });
            }
        }

        public static string ReadBody(HttpListenerRequest req, long maxBytes = BodyLimit.Default)
        {
            return BodyLimit.ReadLimited(req.InputStream, req.ContentEncoding, maxBytes, req.ContentLength64);
        }

        public static void WriteJson(HttpListenerResponse resp, object dto)
            => WriteRaw(resp, 200, "application/json; charset=utf-8", Json.To(dto));

        public static void WriteText(HttpListenerResponse resp, int code, string text)
            => WriteRaw(resp, code, "text/plain; charset=utf-8", text);

        public static void WriteRaw(HttpListenerResponse resp, int code, string contentType, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? "");
            resp.StatusCode = code;
            resp.ContentType = contentType;
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
        }
    }
}
