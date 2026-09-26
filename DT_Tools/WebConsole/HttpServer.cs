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
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public HttpServer(Router router)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public void Start(int port)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                _listener.Start();
                _running = true;
                _thread = new Thread(Loop) { IsBackground = true, Name = "DT_WebConsole" };
                _thread.Start();
                Log.Info("WebConsole", $"已启动 → http://127.0.0.1:{port}/");
            }
            catch (Exception ex)
            {
                // 堆栈进 BepInEx 主日志（端口占用/权限不足等常见原因需可诊断）
                Log.Exception("WebConsole", ex, $"启动失败（端口 {port}）");
            }
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
                catch (Exception ex)
                {
                    if (_running)
                        Log.Warn("WebConsole", $"监听循环中断，停止接受请求：{ex.GetType().Name}: {ex.Message}");
                    break;
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
