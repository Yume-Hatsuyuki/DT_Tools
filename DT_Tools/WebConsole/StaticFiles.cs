using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// WEBUI 静态资源服务：白名单（index/login/css/js）+ Mime 表 + 防目录穿越。
    /// WEBUI 缺失时页面返回 503，API 不受影响。
    /// </summary>
    public static class StaticFiles
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

        public static bool TryServe(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string path = req.Url.AbsolutePath;
            if (path == "/" || path == "/index.html")
                path = "/index.html";
            else if (path == "/login.html" || path == "/login")
                path = "/login.html";

            if (!(path == "/index.html" || path == "/login.html" ||
                  path.StartsWith("/css/", StringComparison.Ordinal) ||
                  path.StartsWith("/js/", StringComparison.Ordinal) ||
                  path.StartsWith("/favicon/", StringComparison.Ordinal)))
                return false;

            string root = WebRoot;
            if (!Directory.Exists(root))
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Log.Warn("WebConsole", $"WEBUI 目录缺失: {root}；页面返回 503，API 仍可用。");
                }
                HttpServer.WriteText(resp, 503, "WEBUI missing; API still available.");
                return true;
            }

            string rel = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(root, rel));
            if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            {
                HttpServer.WriteText(resp, 403, "Forbidden");
                return true;
            }

            if (!File.Exists(full))
            {
                HttpServer.WriteText(resp, 503, "WEBUI file missing; API still available.");
                return true;
            }

            byte[] bytes = File.ReadAllBytes(full);
            resp.StatusCode = 200;
            resp.ContentType = Mime(full);
            // 静态资源禁缓存：插件升级后浏览器必须拿新文件（协商缓存走 304）
            resp.Headers["Cache-Control"] = "no-cache";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
            return true;
        }

        /// <summary>未认证时返回登录页（401）；文件缺失返回 false 由调用方兜底。</summary>
        public static bool TryServeLogin(HttpListenerResponse resp)
        {
            string full = Path.Combine(WebRoot, "login.html");
            if (!File.Exists(full))
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Log.Warn("WebConsole", $"WEBUI/login.html 缺失: {full}");
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
                ".ico" => "image/x-icon",
                ".webmanifest" => "application/manifest+json",
                _ => "application/octet-stream"
            };
        }
    }
}
