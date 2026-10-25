using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DT_Tools.Game
{
    public static class NeteaseMusic
    {
        public const string ApiBase = "https://nextmusic.toubiec.cn/api";

        public const string PublicIpUrl = "https://ipinfo.io/json";

        public const string DefaultFallbackIp = "1.1.1.1";

        public const string DefaultLevel = "standard";

        public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36 Edg/155.0.0.0";

        public static readonly IReadOnlyList<string> Levels = new[]
        {
            "standard",
            "higher",
            "exhigh",
            "lossless",
            "hires",
            "sky",
            "jyeffect",
            "jymaster",
            "dolby",
        };

        public static string LevelLabel(string level)
        {
            switch ((level ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "standard": return "标准音质";
                case "higher": return "较高音质";
                case "exhigh": return "极高音质";
                case "lossless": return "无损音质";
                case "hires": return "Hi-Res音质";
                case "sky": return "沉浸环绕声";
                case "jyeffect": return "高清环绕声";
                case "jymaster": return "超清母带";
                case "dolby": return "杜比全景声";
                default: return level ?? DefaultLevel;
            }
        }

        private static readonly HttpClient Http = CreateClient();

        public sealed class SongInfo
        {
            public string Id = "";
            public string Name = "";
            public string Singer = "";
            public string Duration = "";
        }

        public sealed class SongUrl
        {
            public string Url = "";
            public string Level = "";
            public long Bitrate;
            public long Size;
        }

        public static bool TryParseSongId(string input, out string id)
        {
            id = null;
            string s = (input ?? string.Empty).Trim();
            if (s.Length == 0)
                return false;
            if (IsDigits(s))
            {
                id = s;
                return true;
            }

            const string prefix = "netease:";
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string rest = s.Substring(prefix.Length).Trim();
                if (!IsDigits(rest))
                    return false;
                id = rest;
                return true;
            }

            if (s.IndexOf("163.com", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            string candidate = s.IndexOf("://", StringComparison.Ordinal) < 0 ? "https://" + s : s;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri))
                return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;
            string host = uri.Host.ToLowerInvariant();
            if (host != "163.com" && !host.EndsWith(".163.com", StringComparison.Ordinal))
                return false;

            return (IsSongRoute(uri.AbsolutePath) && TryReadIdFromQuery(uri.Query, out id))
                || (IsSongRoute(uri.Fragment) && TryReadIdFromQuery(uri.Fragment, out id))
                || TryReadIdFromPath(uri.AbsolutePath, out id)
                || TryReadIdFromPath(uri.Fragment, out id);
        }

        public static string NormalizeLevel(string level)
        {
            string l = (level ?? string.Empty).Trim().ToLowerInvariant();
            foreach (string candidate in Levels)
            {
                if (candidate == l)
                    return l;
            }
            return DefaultLevel;
        }

        public static bool IsValidIp(string text)
        {
            string t = (text ?? string.Empty).Trim();
            return t.Length > 0 && IPAddress.TryParse(t, out _);
        }

        public static async Task<string> ResolvePublicIpAsync(string fallbackIp, CancellationToken ct)
        {
            string fallback = IsValidIp(fallbackIp) ? fallbackIp.Trim() : DefaultFallbackIp;
            try
            {
                using (var resp = await Http.GetAsync(PublicIpUrl, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    string text = await resp.Content.ReadAsStringAsync();
                    string ip = JObject.Parse(text).Value<string>("ip");
                    if (IsValidIp(ip))
                        return ip.Trim();
                }
            }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested)
                    throw;
                Log.Warn("Netease", $"公网 IP 获取失败，使用回退地址 {fallback}：{e.Message}");
                return fallback;
            }
            Log.Warn("Netease", $"公网 IP 返回内容无效，使用回退地址 {fallback}。");
            return fallback;
        }

        public static async Task<SongInfo> GetSongInfoAsync(string id, string ip, CancellationToken ct)
        {
            JObject data = await PostAsync("getSongInfo", new JObject { ["id"] = id, ["ip"] = ip }, ct);
            return new SongInfo
            {
                Id = id,
                Name = data.Value<string>("name") ?? string.Empty,
                Singer = data.Value<string>("singer") ?? string.Empty,
                Duration = data.Value<string>("duration") ?? string.Empty,
            };
        }

        public static async Task<SongUrl> GetSongUrlAsync(string id, string ip, string level, CancellationToken ct)
        {
            string lv = NormalizeLevel(level);
            JObject data = await PostAsync("getSongUrl", new JObject { ["id"] = id, ["level"] = lv, ["ip"] = ip }, ct);
            string url = data.Value<string>("url");
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("接口未返回可用直链（歌曲可能无版权、不可用，或该音质不支持）。");
            url = StripUrlQuery(url);
            return new SongUrl
            {
                Url = url,
                Level = data.Value<string>("level") ?? lv,
                Bitrate = data.Value<long?>("br") ?? 0,
                Size = data.Value<long?>("size") ?? 0,
            };
        }

        private static async Task<JObject> PostAsync(string action, JObject payload, CancellationToken ct)
        {
            payload["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using (var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (var resp = await Http.PostAsync($"{ApiBase}/{action}", content, ct))
            {
                string text = await resp.Content.ReadAsStringAsync();
                JObject root;
                try
                {
                    root = JObject.Parse(text);
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException($"接口 {action} 返回的不是 JSON（HTTP {(int)resp.StatusCode}）。");
                }
                int code = root.Value<int?>("code") ?? (int)resp.StatusCode;
                if (!resp.IsSuccessStatusCode || code != 200)
                    throw new InvalidOperationException($"接口 {action} 返回异常（{code}）。");
                return root["data"] as JObject
                    ?? throw new InvalidOperationException($"接口 {action} 缺少 data 字段。");
            }
        }

        public const string IpModeIpinfo = "ipinfo";

        public const string IpModeCustom = "custom";

        public static string NormalizeIpMode(string mode)
        {
            return string.Equals((mode ?? string.Empty).Trim(), IpModeCustom, StringComparison.OrdinalIgnoreCase)
                ? IpModeCustom
                : IpModeIpinfo;
        }

        public static async Task<string> ResolveRequestIpAsync(string mode, string fallbackIp, CancellationToken ct)
        {
            if (NormalizeIpMode(mode) == IpModeCustom)
            {
                if (IsValidIp(fallbackIp))
                    return fallbackIp.Trim();
                Log.Warn("Netease", $"自定义 IP 不合法，使用默认地址 {DefaultFallbackIp}。");
                return DefaultFallbackIp;
            }
            return await ResolvePublicIpAsync(fallbackIp, ct);
        }

        internal static string StripUrlQuery(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                int q = url.IndexOfAny(new[] { '?', '#' });
                return q >= 0 ? url.Substring(0, q) : url;
            }
            return uri.GetLeftPart(UriPartial.Path);
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", DefaultUserAgent);
            return client;
        }

        private static bool IsSongRoute(string pathOrFragment)
        {
            if (string.IsNullOrEmpty(pathOrFragment))
                return false;
            string p = pathOrFragment.TrimStart('#');
            int q = p.IndexOf('?');
            if (q >= 0)
                p = p.Substring(0, q);
            foreach (string seg in p.Split('/'))
            {
                if (string.Equals(seg, "song", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool TryReadIdFromQuery(string part, out string id)
        {
            id = null;
            if (string.IsNullOrEmpty(part))
                return false;
            int q = part.IndexOf('?');
            string query = q >= 0 ? part.Substring(q + 1) : part.TrimStart('?');
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0)
                    continue;
                if (!string.Equals(pair.Substring(0, eq), "id", StringComparison.OrdinalIgnoreCase))
                    continue;
                string value = pair.Substring(eq + 1);
                if (!IsDigits(value))
                    return false;
                id = value;
                return true;
            }
            return false;
        }

        private static bool TryReadIdFromPath(string path, out string id)
        {
            id = null;
            if (string.IsNullOrEmpty(path))
                return false;
            string[] segs = path.Split('/');
            for (int i = 0; i + 1 < segs.Length; i++)
            {
                if (string.Equals(segs[i], "song", StringComparison.OrdinalIgnoreCase) && IsDigits(segs[i + 1]))
                {
                    id = segs[i + 1];
                    return true;
                }
            }
            return false;
        }

        private static bool IsDigits(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 20)
                return false;
            foreach (char c in s)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }
    }
}
