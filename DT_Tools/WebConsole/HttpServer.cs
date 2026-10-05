using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// HTTP 传输层：HttpListener 监听 + 监听线程 + 每请求 ThreadPool 派发 + 响应写出工具。
    /// 线程模型沿旧版：/api/run 在工作线程同步等待主线程结果，不能堵住监听线程。
    /// </summary>
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

            // Unity(Mono) 的 HttpListener 解析不了方括号 IPv6 的 Host 头（按首个冒号切割：
            // Host "[::]:19452" 会拼出 "http://[:19452/…" 直接 Invalid url；IPv4 的 Host
            // 又与 v6 前缀不匹配而报 Invalid host）——IPv6 前缀实际收不到任何标准客户端。
            // 补一条 IPv4 前缀保证可达：通配 [::]（"所有网卡"语义）补 http://*:port/；
            // 具体 v6 地址只补回环，不擅自扩大暴露面。
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
                // 堆栈进 BepInEx 主日志（端口占用/权限不足等常见原因需可诊断）
                Log.Exception("WebConsole", error, $"启动失败（监听 {listenIp}:{port}）");
                return;
            }

            // v6+v4 双前缀可能同端口冲突（v6 any 双栈收 v4 时占用 v4 any）——降级为仅 IPv4 副本
            Log.Warn("WebConsole", $"IPv6+IPv4 双前缀监听失败（{error.Message}），降级为仅 {companion}");
            if (!TryStart(new[] { companion }, out error))
                Log.Exception("WebConsole", error, $"启动失败（监听 {listenIp}:{port}）");
            else
                Log.Info("WebConsole", $"已启动 → {companion}（IPv6 前缀未生效）");
        }

        /// <summary>按序注册前缀并启动监听线程；失败时清理 listener 并带出异常。</summary>
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
                try { _listener.Close(); } catch { /* 启动失败清理忽略 */ }
                _listener = null;
                return false;
            }
        }

        /// <summary>
        /// 归一化监听地址："0.0.0.0"/"any"/"*" → "*"（IPv4 全网卡）；"::" → "[::]"（IPv6 全网卡，
        /// 通常双栈同时收 v4）；具体地址按 IPAddress 校验——IPv6 必须加方括号才是合法的
        /// HttpListener 前缀（如 http://[::1]:19450/）；localhost 原样；非法值回退 127.0.0.1 并告警。
        /// Unity(Mono) 的 HttpListener 不做 Windows URL ACL 校验，任意前缀可直接监听。
        /// 监听地址解释的唯一权威：WsServer.ResolveIp 亦经本方法（避免两份口径漂移）。
        /// </summary>
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
            try { _listener?.Stop(); } catch { /* 关闭阶段忽略 */ }
        }

        private void Loop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch (HttpListenerException hle)
                {
                    // 监听器已失效（Stop/句柄释放）时 GetContext 会持续抛——该状态无法自愈，
                    // 下线并告警；监听器仍健康时的瞬时异常不致命，稍候重试继续接受请求
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
                        catch { /* 关闭阶段忽略 */ }
                        try { captured.Response.Abort(); } catch { }
                    }
                });
            }
        }

        // ---- 响应写出工具（供 Router/Auth/Api 共用）----

        public static string ReadBody(HttpListenerRequest req)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
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
