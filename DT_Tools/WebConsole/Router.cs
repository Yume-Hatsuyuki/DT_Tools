using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace DT_Tools.WebConsole
{
    public sealed class Router
    {
        private sealed class Route
        {
            public string Method;
            public string Prefix;
            public Action<HttpListenerContext> Handler;
        }

        private readonly Auth _auth;
        private readonly List<Route> _routes = new List<Route>();
        private readonly string _listenIp;
        private readonly string[] _localHostWhitelist;
        private readonly string[] _allowedHosts;
        private readonly bool _serveStaticFiles;

        public Router(Auth auth, string listenIp, bool serveStaticFiles = true)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _listenIp = (listenIp ?? "").Trim();
            _localHostWhitelist = HostPolicy.LocalWhitelist(_listenIp);
            _allowedHosts = HostPolicy.ParseAllowed(WebConsoleOptions.AllowedHosts);
            _serveStaticFiles = serveStaticFiles;
        }

        public void Add(string method, string prefix, Action<HttpListenerContext> handler)
        {
            _routes.Add(new Route { Method = method, Prefix = prefix, Handler = handler });
        }

        public void Dispatch(HttpListenerContext ctx)
        {
            try
            {
                DispatchCore(ctx);
            }
            catch (BodyTooLargeException)
            {
                Log.Warn("WebConsole", "已拒绝超大请求体：" + ctx.Request.HttpMethod + " " + ctx.Request.Url.AbsolutePath + "。");
                HttpServer.WriteText(ctx.Response, 413, "Payload Too Large");
            }
        }

        private void DispatchCore(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            string path = req.Url.AbsolutePath;

            if (!IsSameOrigin(req))
            {
                Log.Warn("WebConsole", $"已拒绝跨站请求：{req.HttpMethod} {path}（Origin/Referer 与 Host 不符，且不在 AllowedHosts 中）。");
                HttpServer.WriteText(resp, 403, "Forbidden");
                return;
            }
            if (!IsLocalHost(req))
            {
                Log.Warn("WebConsole", $"已拒绝 Host 异常的请求：{req.HttpMethod} {path}（Host 非本机地址，也不在 AllowedHosts 中，疑似 DNS rebinding）。");
                HttpServer.WriteText(resp, 403, "Forbidden");
                return;
            }

            if (path == "/login" && req.HttpMethod == "POST")
            {
                _auth.HandleLogin(ctx);
                return;
            }

            if (path.StartsWith("/favicon/", StringComparison.Ordinal))
            {
                if (!StaticFiles.TryServe(req, resp))
                    HttpServer.WriteText(resp, 404, "Not Found");
                return;
            }

            if (!_auth.Check(ctx))
            {
                resp.StatusCode = 401;
                if (!StaticFiles.TryServeLogin(resp))
                    HttpServer.WriteText(resp, 401, "Unauthorized");
                return;
            }

            foreach (var route in _routes)
            {
                if (route.Method != "*" && route.Method != req.HttpMethod)
                    continue;
                if (path == route.Prefix ||
                    path.StartsWith(route.Prefix, StringComparison.Ordinal))
                {
                    route.Handler(ctx);
                    return;
                }
            }

            if (_serveStaticFiles && StaticFiles.TryServe(req, resp))
                return;

            resp.StatusCode = 404;
            HttpServer.WriteText(resp, 404, "Not Found");
        }

        private bool IsSameOrigin(HttpListenerRequest req)
        {
            string origin = req.Headers["Origin"];
            if (string.IsNullOrEmpty(origin))
            {
                origin = req.Headers["Referer"];
            }
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }
            return HostPolicy.IsSameOriginHttp(origin, req.Headers["Host"], _allowedHosts);
        }

        private bool IsLocalHost(HttpListenerRequest req)
        {
            return HostPolicy.IsAllowedHost(req.Headers["Host"], _localHostWhitelist, _allowedHosts);
        }

        private string[] BuildLocalHostWhitelist()
        {
            return HostPolicy.LocalWhitelist(_listenIp);
        }
    }
}
