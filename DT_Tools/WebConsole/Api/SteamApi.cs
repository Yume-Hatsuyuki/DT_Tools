using System;
using System.IO;
using System.Net;
using System.Threading;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// Steam WebAPI 代理：转发 GetNumberOfCurrentPlayers，供 WebUI 显示游戏当前在线人数。
    /// 浏览器直连 Steam 受 CORS 限制；工作线程上同步请求，结果缓存 60s，失败 3 次重试。
    /// </summary>
    public static class SteamApi
    {
        private const string Endpoint =
            "https://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid=";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private const int MaxAttempts = 3;
        private const int RequestTimeoutMs = 6000;
        private const int RetryDelayMs = 400;
        private static readonly object Gate = new object();

        private static string _cachedJson;
        private static DateTime _cachedAt;

        static SteamApi()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { /* 运行时不支持时忽略 */ }
        }

        public static void Handle(HttpListenerContext ctx)
        {
            HttpServer.WriteRaw(ctx.Response, 200, "application/json; charset=utf-8", GetCurrentPlayersJson());
        }

        /// <summary>返回 {"ok":true,"appid":..,"players":N}；失败且有旧缓存时降级返回缓存。</summary>
        private static string GetCurrentPlayersJson()
        {
            lock (Gate)
            {
                if (_cachedJson != null && DateTime.UtcNow - _cachedAt < CacheTtl)
                    return _cachedJson;

                Exception last = null;
                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    try
                    {
                        _cachedJson = Fetch();
                        _cachedAt = DateTime.UtcNow;
                        return _cachedJson;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        if (attempt < MaxAttempts)
                            Thread.Sleep(RetryDelayMs * attempt);
                    }
                }

                Log.Warn("WebConsole", $"Steam 在线人数获取失败（已重试 {MaxAttempts} 次）: {last?.Message}");
                if (_cachedJson != null) return _cachedJson;
                return Json.To(new { ok = false, error = last?.Message ?? "unknown" });
            }
        }

        private static string Fetch()
        {
            var request = (HttpWebRequest)WebRequest.Create(Endpoint + Define.STEAM_RELEASE_APP_ID);
            request.Method = "GET";
            request.Timeout = RequestTimeoutMs;
            request.ReadWriteTimeout = RequestTimeoutMs;
            request.UserAgent = "DT_Tools-WebConsole";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                var body = Json.From<SteamResponse>(new StreamReader(stream, System.Text.Encoding.UTF8).ReadToEnd());
                int players = body?.Response?.PlayerCount ?? -1;
                if (players < 0)
                    throw new Exception("unexpected Steam response");

                return Json.To(new { ok = true, appid = Define.STEAM_RELEASE_APP_ID, players });
            }
        }

        // Steam 响应形状：{"response":{"player_count":N,"result":0}}
        private sealed class SteamResponse
        {
            [Newtonsoft.Json.JsonProperty("response")]
            public SteamPlayerCount Response;
        }

        private sealed class SteamPlayerCount
        {
            [Newtonsoft.Json.JsonProperty("player_count")]
            public int PlayerCount;
        }
    }
}
