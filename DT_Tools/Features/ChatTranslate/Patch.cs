using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DT_Tools.Core;
using HarmonyLib;
using Newtonsoft.Json;

namespace DT_Tools.Features.ChatTranslate
{
    internal static class Patches
    {
        private const string BaiduApi = "https://api.fanyi.baidu.com/api/trans/vip/translate";

        private const string BaiduWebApi = "https://fanyi.baidu.com/ait/text/translate";

        private const string WebUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36";

        private const string DefaultTagColor = "#80D8FF";

        private static readonly HttpClient Http = new HttpClient();

        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();

        private static readonly object CacheGate = new object();

        private static bool _warnedNoChannel;

        private static DateTime _lastFailLogAt = DateTime.MinValue;

        private sealed class BaiduTrans
        {
            public string src { get; set; }

            public string dst { get; set; }
        }

        private sealed class BaiduResp
        {
            public string error_code { get; set; }

            public string error_msg { get; set; }

            public BaiduTrans[] trans_result { get; set; }
        }

        private sealed class WebList
        {
            public string dst { get; set; }
        }

        private sealed class WebData
        {
            [JsonProperty("event")]
            public string EventName { get; set; }

            public WebList[] list { get; set; }
        }

        private sealed class WebEvt
        {
            public int errno { get; set; }

            public string errmsg { get; set; }

            public WebData data { get; set; }
        }

        static Patches()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
            }
        }

        [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.EnqueueNormalChat))]
        internal static class ChatTranslateReceivePatch
        {
            private static void Postfix(int playerId, string message, bool isDeadByHost)
            {
                try
                {
                    if (!Engine.Enabled<ChatTranslateFeature>())
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(message))
                    {
                        return;
                    }

                    if (IsChinese(message))
                    {
                        return;
                    }

                    string translated = TranslateTo(message, ChatTranslateFeature.TargetLang);
                    if (string.IsNullOrEmpty(translated) || string.Equals(translated, message, StringComparison.Ordinal))
                    {
                        return;
                    }

                    Managers.Voice?.EnqueueNormalChat(playerId, BuildTranslationMessage(translated), isDeadByHost);
                }
                catch (Exception ex)
                {
                    Log.Warn<ChatTranslateFeature>(ex, "接收翻译处理异常");
                }
            }
        }

        [HarmonyPatch(typeof(VoiceManager), nameof(VoiceManager.SendChatMessage))]
        internal static class ChatTranslateSendPatch
        {
            private static void Prefix(ref string message)
            {
                try
                {
                    if (!Engine.Enabled<ChatTranslateFeature>())
                    {
                        return;
                    }

                    if (!ChatTranslateFeature.TranslateOutgoing)
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(message))
                    {
                        return;
                    }

                    if (message[0] == '\u200B')
                    {
                        return;
                    }

                    if (!IsChinese(message))
                    {
                        return;
                    }

                    string translated = TranslateTo(message, ChatTranslateFeature.OutgoingLang);
                    if (!string.IsNullOrEmpty(translated) && !string.Equals(translated, message, StringComparison.Ordinal))
                    {
                        Log.Info<ChatTranslateFeature>("发送翻译：" + message + " → " + translated);
                        message = translated;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn<ChatTranslateFeature>(ex, "发送翻译处理异常");
                }
            }
        }

        private static string TranslateTo(string text, string targetLang)
        {
            string cacheKey = text + "\u0001" + targetLang;
            lock (CacheGate)
            {
                if (Cache.TryGetValue(cacheKey, out string hit))
                {
                    return hit;
                }
            }

            bool hasKey = !string.IsNullOrEmpty(ChatTranslateFeature.BaiduAppId)
                && !string.IsNullOrEmpty(ChatTranslateFeature.BaiduSecretKey);

            string dst = hasKey ? TranslateByApi(text, targetLang) : TranslateByWeb(text, targetLang);

            if (!string.IsNullOrEmpty(dst) && !string.Equals(dst, text, StringComparison.Ordinal))
            {
                lock (CacheGate)
                {
                    if (Cache.Count >= ChatTranslateFeature.CacheCapacity)
                    {
                        Cache.Clear();
                    }
                    Cache[cacheKey] = dst;
                }
                return dst;
            }
            return text;
        }

        private static string TranslateByApi(string text, string targetLang)
        {
            string appId = ChatTranslateFeature.BaiduAppId;
            string secret = ChatTranslateFeature.BaiduSecretKey;
            string salt = Guid.NewGuid().ToString("N").Substring(0, 8);
            string sign = Md5Hex(appId + text + salt + secret);

            FormUrlEncodedContent content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["q"] = text,
                ["from"] = ChatTranslateFeature.SourceLang,
                ["to"] = targetLang,
                ["appid"] = appId,
                ["salt"] = salt,
                ["sign"] = sign
            });

            try
            {
                int timeoutMs = Math.Max(500, ChatTranslateFeature.RequestTimeoutMs);
                using (CancellationTokenSource cts = new CancellationTokenSource(timeoutMs))
                {
                    HttpResponseMessage resp = Http.PostAsync(BaiduApi, content, cts.Token).GetAwaiter().GetResult();
                    string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (Json.TryFrom<BaiduResp>(body, out BaiduResp parsed) && parsed != null
                        && parsed.trans_result != null && parsed.trans_result.Length > 0)
                    {
                        return parsed.trans_result[0].dst;
                    }

                    if (parsed != null && !string.IsNullOrEmpty(parsed.error_msg))
                    {
                        LogFail("百度正式 API 返回错误 " + parsed.error_code + ": " + parsed.error_msg);
                    }
                    else
                    {
                        LogFail("正式 API 响应解析失败");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogFail("正式 API 请求失败：" + ex.Message);
                return null;
            }
        }

        private static string TranslateByWeb(string text, string targetLang)
        {
            if (!ChatTranslateFeature.WebApiFallback)
            {
                if (!_warnedNoChannel)
                {
                    _warnedNoChannel = true;
                    Log.Warn<ChatTranslateFeature>("未配置百度 appid/密钥且免 key 模式已关闭，翻译不生效。");
                }
                return null;
            }

            string fromCode = IsChinese(text) ? "zh" : DetectLang(text);
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            object payload = new
            {
                needNewlineCombine = false,
                disableCache = false,
                isAi = false,
                sseStartTime = ts,
                query = text,
                from = fromCode,
                to = targetLang,
                reference = "",
                corpusIds = new int[0],
                needPhonetic = false,
                domain = "common",
                detectLang = "",
                isIncognitoAI = false,
                milliTimestamp = ts
            };

            StringContent content = new StringContent(Json.To(payload), Encoding.UTF8, "application/json");
            string referer = "https://fanyi.baidu.com/mtpe-individual/transText?query="
                + Uri.EscapeDataString(text) + "&lang=" + fromCode + "2" + targetLang;

            try
            {
                int timeoutMs = Math.Max(500, ChatTranslateFeature.RequestTimeoutMs);
                using (CancellationTokenSource cts = new CancellationTokenSource(timeoutMs))
                {
                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, BaiduWebApi);
                    req.Headers.TryAddWithoutValidation("accept", "text/event-stream");
                    req.Headers.TryAddWithoutValidation("acs-token", "400");
                    req.Headers.TryAddWithoutValidation("referer", referer);
                    req.Headers.TryAddWithoutValidation("user-agent", WebUserAgent);
                    req.Content = content;

                    HttpResponseMessage resp = Http.SendAsync(req, cts.Token).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode)
                    {
                        LogFail("网页接口 HTTP " + (int)resp.StatusCode);
                        return null;
                    }

                    using (Stream stream = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        List<string> parts = null;
                        string lastErr = null;
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!line.StartsWith("data:"))
                            {
                                continue;
                            }
                            string json = line.Substring(5).Trim();
                            if (json.Length == 0)
                            {
                                continue;
                            }
                            if (!Json.TryFrom<WebEvt>(json, out WebEvt ev) || ev == null)
                            {
                                continue;
                            }
                            if (ev.errno != 0)
                            {
                                lastErr = ev.errmsg;
                                break;
                            }
                            if (ev.data != null && ev.data.EventName == "Translating" && ev.data.list != null)
                            {
                                if (parts == null)
                                {
                                    parts = new List<string>();
                                }
                                foreach (WebList item in ev.data.list)
                                {
                                    if (!string.IsNullOrEmpty(item.dst))
                                    {
                                        parts.Add(item.dst);
                                    }
                                }
                            }
                        }

                        if (parts != null && parts.Count > 0)
                        {
                            return string.Join("\n", parts);
                        }
                        if (lastErr != null)
                        {
                            LogFail("网页接口返回错误：" + lastErr);
                        }
                        else
                        {
                            LogFail("网页接口没有返回译文（可能被风控）");
                        }
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogFail("网页接口请求失败：" + ex.Message);
                return null;
            }
        }

        private static string BuildTranslationMessage(string translated)
        {
            string color = ChatTranslateFeature.TranslationTagColor;
            if (string.IsNullOrEmpty(color) || color.Length != 7 || color[0] != '#')
            {
                color = DefaultTagColor;
            }
            else
            {
                for (int i = 1; i < color.Length; i++)
                {
                    char c = color[i];
                    bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                    if (!hex)
                    {
                        color = DefaultTagColor;
                        break;
                    }
                }
            }
            return "<color=" + color + ">[翻译版]</color>" + translated;
        }

        private static string Md5Hex(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static bool IsChinese(string s)
        {
            int han = 0;
            int total = 0;
            foreach (char c in s)
            {
                if (c >= '\u3040' && c <= '\u30ff')
                {
                    return false;
                }
                if (c >= '\uac00' && c <= '\ud7af')
                {
                    return false;
                }
                if (c < '\u0080')
                {
                    continue;
                }
                total++;
                if (c >= '\u4e00' && c <= '\u9fff')
                {
                    han++;
                }
            }
            return total > 0 && (double)han / total > 0.4;
        }

        private static string DetectLang(string s)
        {
            foreach (char c in s)
            {
                if (c >= '\u3040' && c <= '\u30ff')
                {
                    return "jp";
                }
                if (c >= '\uac00' && c <= '\ud7af')
                {
                    return "kor";
                }
                if (c >= '\u0400' && c <= '\u04ff')
                {
                    return "ru";
                }
                if (c >= '\u0600' && c <= '\u06ff')
                {
                    return "ara";
                }
                if (c >= '\u0e00' && c <= '\u0e7f')
                {
                    return "th";
                }
                if (c >= '\u0590' && c <= '\u05ff')
                {
                    return "he";
                }
                if (c >= '\u0370' && c <= '\u03ff')
                {
                    return "el";
                }
            }
            return "en";
        }

        private static void LogFail(string detail)
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastFailLogAt).TotalSeconds < 30)
            {
                return;
            }
            _lastFailLogAt = now;
            Log.Warn<ChatTranslateFeature>(detail + "（显示原文）");
        }
    }
}
