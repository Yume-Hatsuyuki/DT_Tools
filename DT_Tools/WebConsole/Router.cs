using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// 表驱动路由：登录 → 鉴权 → 路由表（方法+路径前缀）→ 静态资源 → 404。
    /// </summary>
    /// <remarks>
    /// 路由按注册顺序匹配，精确路径请注册在前缀路径之前。
    /// </remarks>
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
            // MCP 独立监听等纯 API 门不服务 WebUI 静态文件（路由未命中直接 404）
            _serveStaticFiles = serveStaticFiles;
        }

        /// <summary>
        /// 注册路由；method 用 "*" 匹配任意方法（handler 内自行校验）。
        /// </summary>
        /// <param name="method">HTTP 方法，* 表示任意方法。</param>
        /// <param name="prefix">路由前缀。</param>
        /// <param name="handler">处理请求的回调，方法内自行校验方法与身份。</param>
        public void Add(string method, string prefix, Action<HttpListenerContext> handler)
        {
            _routes.Add(new Route { Method = method, Prefix = prefix, Handler = handler });
        }

        /// <summary>
        /// 请求分发入口：请求体超限统一回复 413，其它异常原样抛给上层。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
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

            // 跨站与 DNS rebinding 防线，所有方法都检查（读请求同样如此）：
            // 1) Origin 或 Referer 存在时必须同源，或来自反代域名（AllowedHosts）；
            // 2) Host 必须指向本机，或属于反代域名（AllowedHosts）。
            // DNS rebinding 时 Origin 与 Host 同为攻击者域名，同源校验会通过，由第 2 条拦截。
            // 非浏览器客户端（curl 等）不带 Origin 或 Referer，只受第 2 条约束。
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

            // 站点图标/清单：登录页就要显示图标，免鉴权放行（均为静态只读资源，无信息面）
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

        /// <summary>
        /// 同源校验：Origin（缺省回退 Referer）的 authority 与请求 Host 头一致才算同源。
        /// </summary>
        /// <remarks>
        /// 两者都没有 → 非浏览器客户端，放行；Origin 为字面量 "null"（file:// 沙箱等）
        /// 解析失败 → 拒绝。
        /// </remarks>
        /// <param name="req">HTTP 请求。</param>
        /// <returns>Origin（缺省时用 Referer）与 Host 一致时为 true。</returns>
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

        /// <summary>
        /// Host 头是否指向本机：回环、配置的监听地址（非通配）、本机主机名与网卡地址。
        /// </summary>
        /// <remarks>
        /// 全部在构造时本地枚举并缓存，绝不解析 Host 名（解析恰是 rebinding 的攻击路径）。
        /// 浏览器带 IPv6 Host 恒为方括号形式（[::1]:port），按方括号取地址；其余按最后
        /// 一个冒号去端口。
        /// </remarks>
        /// <param name="req">HTTP 请求。</param>
        /// <returns>Host 头指向本机时为 true。</returns>
        private bool IsLocalHost(HttpListenerRequest req)
        {
            return HostPolicy.IsAllowedHost(req.Headers["Host"], _localHostWhitelist, _allowedHosts);
        }

        /// <summary>
        /// 构造本机白名单。
        /// </summary>
        /// <remarks>
        /// 网卡枚举失败时仅退回回环三项（默认本机回环使用不受影响）。
        /// </remarks>
        /// <returns>本机地址白名单（回环、监听地址、本机主机名与网卡地址）。</returns>
        private string[] BuildLocalHostWhitelist()
        {
            return HostPolicy.LocalWhitelist(_listenIp);
        }
    }
}
