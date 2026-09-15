using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace DT_Tools.Console.Http
{
    internal static class StaticFiles
    {
        private static string _webRoot;
        private static bool _warnedMissing;

        public static string WebRoot
        {
            get
            {
                if (_webRoot != null) return _webRoot;
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _webRoot = Path.Combine(pluginDir ?? "", "WEBUI");
                return _webRoot;
            }
        }

        public static bool TryServe(HttpListenerRequest req, HttpListenerResponse resp, Action<string> warn)
        {
            string path = req.Url.AbsolutePath;
            if (path == "/" || path == "/index.html")
                path = "/index.html";
            else if (path == "/login.html" || path == "/login")
                path = "/login.html";

            if (!(path == "/index.html" || path == "/login.html" ||
                  path.StartsWith("/css/", StringComparison.Ordinal) ||
                  path.StartsWith("/js/", StringComparison.Ordinal)))
                return false;

            string root = WebRoot;
            if (!Directory.Exists(root))
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    warn?.Invoke($"[WebConsole] WEBUI 目录缺失: {root}；页面返回 503，API 仍可用。");
                }
                WriteText(resp, 503, "WEBUI missing; API still available.");
                return true;
            }

            string rel = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(root, rel));
            if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            {
                WriteText(resp, 403, "Forbidden");
                return true;
            }

            if (!File.Exists(full))
            {
                WriteText(resp, 503, "WEBUI file missing; API still available.");
                return true;
            }

            byte[] bytes = File.ReadAllBytes(full);
            resp.StatusCode = 200;
            resp.ContentType = Mime(full);
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
            return true;
        }

        public static bool TryServeLogin(HttpListenerResponse resp, Action<string> warn)
        {
            string root = WebRoot;
            string full = Path.Combine(root, "login.html");
            if (!File.Exists(full))
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    warn?.Invoke($"[WebConsole] WEBUI/login.html 缺失: {full}");
                }
                return false;
            }
            byte[] bytes = File.ReadAllBytes(full);
            resp.StatusCode = 401;
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
            return true;
        }

        private static string Mime(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".png" => "image/png",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        private static void WriteText(HttpListenerResponse resp, int code, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            resp.StatusCode = code;
            resp.ContentType = "text/plain; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
        }
    }
}
