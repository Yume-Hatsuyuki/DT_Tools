using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// 表驱动路由：登录 → 鉴权 → 路由表（方法+路径前缀）→ 静态资源 → 404。
    /// 路由按注册顺序匹配，精确路径请注册在前缀路径之前。
    /// </summary>
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

        public Router(Auth auth, string listenIp)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _listenIp = (listenIp ?? "").Trim();
            _localHostWhitelist = BuildLocalHostWhitelist();
        }

        /// <summary>注册路由；method 用 "*" 匹配任意方法（handler 内自行校验）。</summary>
        public void Add(string method, string prefix, Action<HttpListenerContext> handler)
        {
            _routes.Add(new Route { Method = method, Prefix = prefix, Handler = handler });
        }

        public void Dispatch(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            string path = req.Url.AbsolutePath;

            // 跨站防护：写请求（非 GET/HEAD）要求同源。浏览器发起的跨站请求必带 Origin
            // （简单 POST）或 Referer，与 Host 头不符即拒绝；非浏览器客户端（curl 等本机
            // 脚本）不带这两个头，直接放行。读请求靠同源策略已无法被跨站页面读取
            // （本服务器不再发送 Access-Control-Allow-Origin: *），无需校验。
            if (req.HttpMethod != "GET" && req.HttpMethod != "HEAD")
            {
                if (!IsSameOrigin(req))
                {
                    Log.Warn("WebConsole", $"已拒绝跨站写请求：{req.HttpMethod} {path}（Origin/Referer 与 Host 不符）。");
                    HttpServer.WriteText(resp, 403, "Forbidden");
                    return;
                }
                // DNS rebinding 防线：该攻击下 Origin 与 Host 同为攻击者域名（其解析被改写
                // 指向 127.0.0.1），同源校验形同虚设，空密码时无第二道防线——再校验 Host
                // 是否指向本机（白名单全部本地枚举，不做 DNS 解析）。
                if (!IsLocalHost(req))
                {
                    Log.Warn("WebConsole", $"已拒绝 Host 异常的写请求：{req.HttpMethod} {path}（Host 非本机地址，疑似 DNS rebinding）。");
                    HttpServer.WriteText(resp, 403, "Forbidden");
                    return;
                }
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

            if (StaticFiles.TryServe(req, resp))
                return;

            resp.StatusCode = 404;
            HttpServer.WriteText(resp, 404, "Not Found");
        }

        /// <summary>
        /// 同源校验：Origin（缺省回退 Referer）的 authority 与请求 Host 头一致才算同源。
        /// 两者都没有 → 非浏览器客户端，放行；Origin 为字面量 "null"（file:// 沙箱等）
        /// 解析失败 → 拒绝。
        /// </summary>
        private static bool IsSameOrigin(HttpListenerRequest req)
        {
            string origin = req.Headers["Origin"];
            if (string.IsNullOrEmpty(origin))
                origin = req.Headers["Referer"];
            if (string.IsNullOrEmpty(origin))
                return true;

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                return false;

            string host = req.Headers["Host"];
            return !string.IsNullOrEmpty(host) &&
                   string.Equals(uri.Authority, host.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Host 头是否指向本机：回环、配置的监听地址（非通配）、本机主机名与网卡地址。
        /// 全部在构造时本地枚举并缓存，绝不解析 Host 名（解析恰是 rebinding 的攻击路径）。
        /// 浏览器带 IPv6 Host 恒为方括号形式（[::1]:port），按方括号取地址；其余按最后
        /// 一个冒号去端口。
        /// </summary>
        private bool IsLocalHost(HttpListenerRequest req)
        {
            string host = req.Headers["Host"];
            if (string.IsNullOrEmpty(host))
                return true;   // HTTP/1.0 客户端可能不带 Host：无法判定，交由同源校验兜底

            string value = host.Trim();
            if (value.StartsWith("["))
            {
                int end = value.IndexOf(']');
                if (end > 0)
                    value = value.Substring(1, end - 1);
            }
            else
            {
                int colon = value.LastIndexOf(':');
                if (colon >= 0)
                    value = value.Substring(0, colon);
            }

            return _localHostWhitelist.Contains(value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>构造本机白名单。网卡枚举失败时仅退回回环三项（默认本机回环使用不受影响）。</summary>
        private string[] BuildLocalHostWhitelist()
        {
            var list = new List<string> { "127.0.0.1", "localhost", "::1" };
            if (_listenIp.Length > 0 && _listenIp != "0.0.0.0" && _listenIp != "*" && _listenIp != "::")
                list.Add(_listenIp);
            try
            {
                string machine = Dns.GetHostName();
                if (!string.IsNullOrEmpty(machine))
                {
                    list.Add(machine);
                    foreach (IPAddress ip in Dns.GetHostAddresses(machine))
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork ||
                            ip.AddressFamily == AddressFamily.InterNetworkV6)
                            list.Add(ip.ToString());
                    }
                }
            }
            catch (Exception)
            {
                // 主机名/网卡枚举不可用：白名单退化为回环三项，安全侧收敛
            }
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
