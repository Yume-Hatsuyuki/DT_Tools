using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// Steam 新闻代理：转发 /feeds/news/app/{AppID}?l=语言，解析为 JSON 供游戏新闻挂件使用。
    /// </summary>
    /// <remarks>
    /// 浏览器直连 Steam 受 CORS 限制，因此由后端中转。工作线程上同步请求，按语言缓存 50 秒（短于挂件 1 分钟的轮询间隔）；
    /// 失败时重试 3 次，仍失败则降级返回该语言的旧缓存。语言白名单须与前端 i18n/index.js 的 LOCALES.steam 保持一致。
    /// 所选语言没有公告时回退英文，并在结果中标注 fallback。
    /// </remarks>
    public static class SteamNewsApi
    {
        /// <summary>
        /// 允许的语言代码（Steam 新闻 RSS 的 l= 参数）。与游戏的 9 种语言一一对应，须与前端 i18n/index.js 的 LOCALES.steam 一致。
        /// </summary>
        public static readonly string[] Languages =
        {
            "schinese", "tchinese", "english", "spanish", "french", "japanese", "koreana", "brazilian", "russian",
        };

        private const string DefaultLanguage = "english";
        private const string Endpoint = "https://store.steampowered.com/feeds/news/app/";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(50);
        private const int MaxAttempts = 3;
        private const int RequestTimeoutMs = 8000;
        private const int RetryDelayMs = 400;
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, CacheEntry> Cache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        static SteamNewsApi()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { /* 运行时不支持时忽略 */ }
        }

        private sealed class CacheEntry
        {
            public string Json;
            public DateTime At;
        }

        /// <summary>
        /// GET /api/steam/news?lang=语言代码。语言不在白名单内时返回 400。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void Handle(HttpListenerContext ctx)
        {
            string raw = ctx.Request.QueryString["lang"];
            if (string.IsNullOrEmpty(raw)) raw = DefaultLanguage;
            string lang = Languages.FirstOrDefault(l => string.Equals(l, raw, StringComparison.OrdinalIgnoreCase));
            if (lang == null)
            {
                HttpServer.WriteRaw(ctx.Response, 400, "application/json; charset=utf-8",
                    Json.To(new { ok = false, error = "unsupported language", lang = raw }));
                return;
            }
            HttpServer.WriteRaw(ctx.Response, 200, "application/json; charset=utf-8", GetNewsJson(lang));
        }

        private static string GetNewsJson(string lang)
        {
            lock (Gate)
            {
                CacheEntry hit;
                if (Cache.TryGetValue(lang, out hit) && DateTime.UtcNow - hit.At < CacheTtl)
                    return hit.Json;

                Exception last = null;
                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    try
                    {
                        string json = Build(lang);
                        Cache[lang] = new CacheEntry { Json = json, At = DateTime.UtcNow };
                        return json;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        if (attempt < MaxAttempts)
                            Thread.Sleep(RetryDelayMs * attempt);
                    }
                }

                Log.Warn("WebConsole", $"Steam 新闻获取失败（语言 {lang}，已重试 {MaxAttempts} 次）: {last?.Message}");
                if (Cache.TryGetValue(lang, out hit))
                    return hit.Json;
                return Json.To(new { ok = false, error = last?.Message ?? "unknown", lang });
            }
        }

        /// <summary>
        /// 拉取并组装响应。所选语言没有公告时回退英文。
        /// </summary>
        private static string Build(string lang)
        {
            var items = Fetch(lang);
            string used = lang;
            bool fallback = false;
            if (items.Count == 0 && !string.Equals(lang, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                items = Fetch(DefaultLanguage);
                used = DefaultLanguage;
                fallback = true;
            }

            return Json.To(new
            {
                ok = true,
                requested = lang,
                lang = used,
                fallback,
                items = items.Select(i => new
                {
                    title = i.Title,
                    url = i.Url,
                    date = i.Date == DateTimeOffset.MinValue
                        ? ""
                        : i.Date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    summary = i.Summary,
                    image = i.Image ?? "",
                }).ToArray(),
            });
        }

        private static List<SteamNewsItem> Fetch(string lang)
        {
            var request = (HttpWebRequest)WebRequest.Create(Endpoint + Define.STEAM_RELEASE_APP_ID + "/?l=" + lang);
            request.Method = "GET";
            request.Timeout = RequestTimeoutMs;
            request.ReadWriteTimeout = RequestTimeoutMs;
            request.UserAgent = "DT_Tools-WebConsole";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return SteamNewsParser.Parse(reader.ReadToEnd());
            }
        }
    }
}
