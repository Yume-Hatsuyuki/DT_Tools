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
    /// <summary>
    /// 网易云音乐在线解析：经第三方中转接口取得歌曲信息与直链。协议参考 nextmusic-dl.mjs。
    /// </summary>
    /// <remarks>
    /// 直链约 25 分钟有效，调用方应在播放时才解析，不得缓存或落盘。
    /// 本类的网络方法只能在后台线程调用，不得在 Unity 主线程上阻塞等待。
    /// </remarks>
    public static class NeteaseMusic
    {
        /// <summary>中转接口根地址。</summary>
        public const string ApiBase = "https://nextmusic.toubiec.cn/api";

        /// <summary>公网 IP 查询地址（与脚本一致）。</summary>
        public const string PublicIpUrl = "https://ipinfo.io/json";

        /// <summary>公网 IP 获取失败时的默认回退地址。</summary>
        public const string DefaultFallbackIp = "1.1.1.1";

        /// <summary>默认音质。</summary>
        public const string DefaultLevel = "standard";

        /// <summary>默认 User-Agent（用户可在界面中自定义）。</summary>
        public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36 Edg/155.0.0.0";

        /// <summary>
        /// 可选音质（与 tools/nextmusic-dl.mjs 对齐，接口实测可用）。
        /// 值：standard / higher / exhigh / lossless / hires / sky / jyeffect / jymaster / dolby。
        /// </summary>
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

        /// <summary>音质显示名（界面下拉用）。</summary>
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

        /// <summary>歌曲元信息。</summary>
        public sealed class SongInfo
        {
            /// <summary>歌曲 id。</summary>
            public string Id = "";
            /// <summary>歌名。</summary>
            public string Name = "";
            /// <summary>歌手。</summary>
            public string Singer = "";
            /// <summary>时长文本（如 2:09）。</summary>
            public string Duration = "";
        }

        /// <summary>歌曲直链。</summary>
        public sealed class SongUrl
        {
            /// <summary>直链（可能带签名参数，约 25 分钟有效）。</summary>
            public string Url = "";
            /// <summary>接口实际返回的音质（可能低于请求的音质）。</summary>
            public string Level = "";
            /// <summary>码率（bps）。</summary>
            public long Bitrate;
            /// <summary>文件大小（字节）。</summary>
            public long Size;
        }

        /// <summary>
        /// 从用户输入中解析歌曲 id。支持：纯数字；netease:&lt;id&gt;；music.163.com 的 song 链接，
        /// id 位于查询串（?id=）、哈希路由（#/song?id=）或路径（/song/&lt;id&gt;）中，可带额外参数，可省略协议头。
        /// </summary>
        /// <param name="input">用户输入。</param>
        /// <param name="id">解析出的歌曲 id；失败时为 null。</param>
        /// <returns>解析成功为 true。</returns>
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

            // 查询串或哈希路由中的 id 只在 song 路由下有效，避免把 playlist?id= 等当作歌曲
            return (IsSongRoute(uri.AbsolutePath) && TryReadIdFromQuery(uri.Query, out id))
                || (IsSongRoute(uri.Fragment) && TryReadIdFromQuery(uri.Fragment, out id))
                || TryReadIdFromPath(uri.AbsolutePath, out id)
                || TryReadIdFromPath(uri.Fragment, out id);
        }

        /// <summary>
        /// 音质归一化：不在可选列表中的值回退为默认音质。
        /// </summary>
        /// <param name="level">请求的音质。</param>
        /// <returns>可用的音质名。</returns>
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

        /// <summary>
        /// 判断字符串是否为合法 IPv4/IPv6 地址。
        /// </summary>
        /// <param name="text">待检查的文本。</param>
        /// <returns>合法时为 true。</returns>
        public static bool IsValidIp(string text)
        {
            string t = (text ?? string.Empty).Trim();
            return t.Length > 0 && IPAddress.TryParse(t, out _);
        }

        /// <summary>
        /// 获取用于接口请求的公网 IP（与脚本一致，通过 ipinfo.io）。失败或返回非法值时使用回退地址并记日志。
        /// </summary>
        /// <param name="fallbackIp">回退地址；不合法时使用 DefaultFallbackIp。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>公网 IP 或回退地址。</returns>
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

        /// <summary>
        /// 获取歌曲元信息（名字、歌手、时长），用于为网易云条目预填名字。
        /// </summary>
        /// <param name="id">歌曲 id。</param>
        /// <param name="ip">请求使用的 IP。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>歌曲信息。</returns>
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

        /// <summary>
        /// 获取歌曲直链。直链时效约 25 分钟，调用方应在播放时调用。
        /// </summary>
        /// <param name="id">歌曲 id。</param>
        /// <param name="ip">请求使用的 IP。</param>
        /// <param name="level">请求的音质，不合法时使用默认音质。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>直链信息；接口未返回可用直链时抛出异常。</returns>
        public static async Task<SongUrl> GetSongUrlAsync(string id, string ip, string level, CancellationToken ct)
        {
            string lv = NormalizeLevel(level);
            JObject data = await PostAsync("getSongUrl", new JObject { ["id"] = id, ["level"] = lv, ["ip"] = ip }, ct);
            string url = data.Value<string>("url");
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("接口未返回可用直链（歌曲可能无版权、不可用，或该音质不支持）。");
            // 网易 CDN 直链常带 ?vuutv=… 等签名参数；UnityWebRequest 对部分编码/超长 query 加载失败。
            // 实测去掉 query 后路径仍可播，与用户反馈一致。
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

        /// <summary>IP 来源：通过 ipinfo.io 获取（默认）。</summary>
        public const string IpModeIpinfo = "ipinfo";

        /// <summary>IP 来源：使用自定义地址，不请求 ipinfo.io（ipinfo 限流时使用）。</summary>
        public const string IpModeCustom = "custom";

        /// <summary>
        /// IP 模式归一化：只接受 custom，其余一律按 ipinfo。
        /// </summary>
        /// <param name="mode">请求的模式。</param>
        /// <returns>IpModeCustom 或 IpModeIpinfo。</returns>
        public static string NormalizeIpMode(string mode)
        {
            return string.Equals((mode ?? string.Empty).Trim(), IpModeCustom, StringComparison.OrdinalIgnoreCase)
                ? IpModeCustom
                : IpModeIpinfo;
        }

        /// <summary>
        /// 按 IP 模式取得请求用 IP。custom 模式直接使用自定义地址，不请求 ipinfo.io；
        /// ipinfo 模式请求 ipinfo.io，失败时回退到同一个配置项（fallbackIp）。
        /// </summary>
        /// <param name="mode">IP 模式。</param>
        /// <param name="fallbackIp">自定义地址（custom 模式）或回退地址（ipinfo 模式）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>用于接口请求的 IP。</returns>
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

        /// <summary>
        /// 去掉 URL 的 query/fragment，仅保留 scheme://host/path。
        /// 网易云 m*.music.126.net 直链的 vuutv 等参数会导致引擎侧加载失败。
        /// </summary>
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
