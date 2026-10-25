using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DT_Tools.WebConsole
{
    internal static class HostPolicy
    {
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
                if (colon >= 0 && v.IndexOf(':') == colon)
                {
                    v = v.Substring(0, colon);
                }
            }
            return v.ToLowerInvariant();
        }

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

        public static bool IsAllowedHost(string hostHeader, string[] localWhitelist, string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(hostHeader))
            {
                return true;
            }
            string name = HostNameOf(hostHeader);
            return Contains(localWhitelist, name) || Contains(allowed, name);
        }

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
