using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// Host 与 Origin 的放行规则，HTTP 与 WebSocket 共用。
    /// </summary>
    /// <remarks>
    /// 防线的目的：挡住 DNS rebinding（Host 指向攻击者域名）与跨站页面（Origin 为其它网站）。
    /// 允许的来源有两类：本机（回环、监听地址、本机主机名与网卡地址），以及配置项 AllowedHosts 中的反代域名。
    /// 非浏览器客户端（curl 等）不发送 Origin 或 Referer，只受 Host 规则约束。
    /// </remarks>
    internal static class HostPolicy
    {
        /// <summary>
        /// 解析 AllowedHosts 配置：逗号、分号或空白分隔；允许带协议与路径，只保留主机名（小写，无端口）。
        /// </summary>
        /// <param name="raw">配置原文，可为空。</param>
        /// <returns>主机名数组，可能为空。</returns>
        public static string[] ParseAllowed(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new string[0];
            }
            var result = new List<string>();
            foreach (var part in raw.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string e = part.Trim();
                int scheme = e.IndexOf("://", StringComparison.Ordinal);
                if (scheme >= 0)
                {
                    e = e.Substring(scheme + 3);
                }
                int slash = e.IndexOf('/');
                if (slash >= 0)
                {
                    e = e.Substring(0, slash);
                }
                string name = HostNameOf(e);
                if (name.Length > 0 && !result.Contains(name))
                {
                    result.Add(name);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 取出 Host 头或 URL 授权部分中的主机名：去端口、去 IPv6 方括号、转小写。
        /// </summary>
        /// <param name="hostHeader">Host 头原文，或 host:port 形式的文本。</param>
        /// <returns>主机名；输入为空时返回空串。</returns>
        public static string HostNameOf(string hostHeader)
        {
            if (string.IsNullOrWhiteSpace(hostHeader))
            {
                return "";
            }
            string v = hostHeader.Trim();
            if (v.StartsWith("[", StringComparison.Ordinal))
            {
                int end = v.IndexOf(']');
                v = end > 0 ? v.Substring(1, end - 1) : v.Substring(1);
            }
            else
            {
                int colon = v.LastIndexOf(':');
                // 只有一个冒号时才是 host:port；多个冒号是裸 IPv6，不做端口剥离
                if (colon >= 0 && v.IndexOf(':') == colon)
                {
                    v = v.Substring(0, colon);
                }
            }
            return v.ToLowerInvariant();
        }

        /// <summary>
        /// 本机白名单：回环地址、监听地址（非通配）、本机主机名与网卡地址。
        /// </summary>
        /// <param name="listenIp">监听地址配置。</param>
        /// <returns>小写、无方括号的主机名数组。</returns>
        public static string[] LocalWhitelist(string listenIp)
        {
            var list = new List<string> { "127.0.0.1", "localhost", "::1" };
            string ip = HostNameOf(listenIp ?? "");
            if (ip.Length > 0 && ip != "0.0.0.0" && ip != "*" && ip != "::")
            {
                list.Add(ip);
            }
            try
            {
                string machine = Dns.GetHostName();
                if (!string.IsNullOrEmpty(machine))
                {
                    list.Add(machine.ToLowerInvariant());
                    foreach (IPAddress addr in Dns.GetHostAddresses(machine))
                    {
                        if (addr.AddressFamily == AddressFamily.InterNetwork ||
                            addr.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            list.Add(addr.ToString().ToLowerInvariant());
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 主机名与网卡枚举不可用：白名单退化为回环三项，安全侧收敛
            }
            var distinct = new List<string>();
            foreach (var x in list)
            {
                if (!distinct.Contains(x))
                {
                    distinct.Add(x);
                }
            }
            return distinct.ToArray();
        }

        /// <summary>
        /// Host 头是否指向本机或反代域名。缺失 Host 头时放行（HTTP/1.0 兼容），交由 Origin 规则兜底。
        /// </summary>
        /// <param name="hostHeader">Host 头原文。</param>
        /// <param name="localWhitelist">本机白名单。</param>
        /// <param name="allowed">反代域名列表。</param>
        /// <returns>允许时为 true。</returns>
        public static bool IsAllowedHost(string hostHeader, string[] localWhitelist, string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(hostHeader))
            {
                return true;
            }
            string name = HostNameOf(hostHeader);
            return Contains(localWhitelist, name) || Contains(allowed, name);
        }

        /// <summary>
        /// HTTP 的 Origin 或 Referer 是否可以接受：与 Host 同源，或来自反代域名。
        /// </summary>
        /// <param name="origin">Origin 或 Referer 的值。</param>
        /// <param name="hostHeader">Host 头原文。</param>
        /// <param name="allowed">反代域名列表。</param>
        /// <returns>允许时为 true。</returns>
        public static bool IsSameOriginHttp(string origin, string hostHeader, string[] allowed)
        {
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }
            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri uri))
            {
                return false;
            }
            if (Contains(allowed, HostNameOf(uri.Host)))
            {
                return true;
            }
            return !string.IsNullOrEmpty(hostHeader) &&
                   string.Equals(uri.Authority, hostHeader.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// WebSocket 的 Origin 是否可以接受。WebSocket 端口与 WebUI 端口不同，所以同源要求：
        /// 主机名与 Host 相同，且端口等于 WebUI 端口；或来自反代域名。
        /// 非浏览器客户端不发送 Origin，直接放行（Host 规则仍需通过）。
        /// </summary>
        /// <param name="origin">Origin 头的值。</param>
        /// <param name="hostHeader">WebSocket 握手的 Host 头。</param>
        /// <param name="webPort">WebUI 的 HTTP 端口。</param>
        /// <param name="allowed">反代域名列表。</param>
        /// <returns>允许时为 true。</returns>
        public static bool IsAllowedWsOrigin(string origin, string hostHeader, int webPort, string[] allowed)
        {
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }
            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri uri))
            {
                return false;
            }
            string originHost = HostNameOf(uri.Host);
            if (Contains(allowed, originHost))
            {
                return true;
            }
            string hostName = HostNameOf(hostHeader);
            return hostName.Length > 0 &&
                   string.Equals(originHost, hostName, StringComparison.Ordinal) &&
                   uri.Port == webPort;
        }

        private static bool Contains(string[] items, string name)
        {
            if (items == null || string.IsNullOrEmpty(name))
            {
                return false;
            }
            foreach (var x in items)
            {
                if (string.Equals(x, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
