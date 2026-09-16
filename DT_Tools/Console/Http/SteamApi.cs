using System;
using System.IO;
using System.Net;
using System.Text;

namespace DT_Tools.Console.Http
{
    /// <summary>
    /// Steam WebAPI 代理：转发 GetNumberOfCurrentPlayers，供 WebUI 显示游戏当前在线人数。
    /// 浏览器直连 Steam 受 CORS 限制；工作线程上同步请求，结果缓存 <see cref="CacheTtl"/>。
    /// </summary>
    internal static class SteamApi
    {
        private const string Endpoint =
            "https://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid=";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private static readonly object Gate = new object();

        private static string _cachedJson;
        private static DateTime _cachedAt;

        static SteamApi()
        {
            // 部分 Unity Mono 运行环境默认协议偏旧，Steam 仅接受 TLS1.2+
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { /* 枚举值在运行时不可用时忽略 */ }
        }

        /// <summary>返回 {"ok":true,"appid":..,"players":N}；失败且有旧缓存时降级返回缓存。</summary>
        public static string GetCurrentPlayersJson(Action<string> warn = null)
        {
            lock (Gate)
            {
                if (_cachedJson != null && DateTime.UtcNow - _cachedAt < CacheTtl)
                    return _cachedJson;

                try
                {
                    _cachedJson = Fetch();
                    _cachedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    warn?.Invoke($"[WebConsole] Steam 在线人数获取失败: {ex.Message}");
                    if (_cachedJson != null) return _cachedJson;
                    return "{\"ok\":false,\"error\":" + JsonEscape(ex.Message) + "}";
                }
                return _cachedJson;
            }
        }

        private static string Fetch()
        {
            var request = (HttpWebRequest)WebRequest.Create(Endpoint + Define.STEAM_RELEASE_APP_ID);
            request.Method = "GET";
            request.Timeout = 6000;
            request.ReadWriteTimeout = 6000;
            request.UserAgent = "DT_Tools-WebConsole";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string body = reader.ReadToEnd();
                int players = ExtractPlayerCount(body);
                if (players < 0)
                    throw new Exception("unexpected Steam response: " + Truncate(body, 200));

                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return "{\"ok\":true,\"appid\":" + Define.STEAM_RELEASE_APP_ID.ToString(inv) +
                       ",\"players\":" + players.ToString(inv) + "}";
            }
        }

        /// <summary>解析 {"response":{"result":1,"player_count":N}}；失败返回 -1。</summary>
        private static int ExtractPlayerCount(string json)
        {
            if (string.IsNullOrEmpty(json)) return -1;
            int key = json.IndexOf("\"player_count\"", StringComparison.Ordinal);
            if (key < 0) return -1;
            int colon = json.IndexOf(':', key + "\"player_count\"".Length);
            if (colon < 0) return -1;

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            int start = i;
            while (i < json.Length && char.IsDigit(json[i])) i++;
            if (i == start) return -1;

            return int.TryParse(json.Substring(start, i - start),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int count)
                ? count
                : -1;
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max) + "…";

        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
