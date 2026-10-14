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
    /// <summary>
    /// 聊天翻译补丁：
    /// 接收翻译——注入 VoiceManager.EnqueueNormalChat（0.1.17a VoiceManager.cs:1331，
    /// 普通聊天接收入口，自己发出的消息经房主广播回显也会走这里）。
    /// 原文照常入队显示（不拼接、不超长、不改变广播）；译文作为第二条本地消息追加
    /// （带色[翻译版]标签 + 译文，仅自己可见，别人收不到）。
    /// 发送翻译——注入 VoiceManager.SendChatMessage（0.1.17a VoiceManager.cs:1239，发包入口），
    /// 把发出的中文消息翻译成目标语言再广播，让所有人看到译文。
    /// 翻译通道：填了百度开放平台 appid/密钥走正式 API（稳定）；未填且开启免 key 模式
    /// 走百度网页翻译接口（SSE 流式，免费，逆向实现、可能随官方调整失效）；都不可用时回退原文。
    /// 百度正式 API：POST https://api.fanyi.baidu.com/api/trans/vip/translate，
    /// 参数 q/from/to/appid/salt/sign，sign = MD5(appid + q + salt + 密钥)（q 用原始文本）。
    /// 百度网页接口：POST https://fanyi.baidu.com/ait/text/translate（SSE），参数参考 bt.mjs。
    /// </summary>
    internal static class Patches
    {
        /// <summary>百度开放平台正式 API。</summary>
        private const string BaiduApi = "https://api.fanyi.baidu.com/api/trans/vip/translate";

        /// <summary>百度网页版翻译接口（免 key，SSE 流式）。</summary>
        private const string BaiduWebApi = "https://fanyi.baidu.com/ait/text/translate";

        /// <summary>网页接口需要浏览器 UA。</summary>
        private const string WebUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36";

        /// <summary>翻译版标签默认颜色（配置非法时回退）。</summary>
        private const string DefaultTagColor = "#80D8FF";

        /// <summary>共享 HttpClient；每次请求用独立 CancellationToken 控制超时。</summary>
        private static readonly HttpClient Http = new HttpClient();

        /// <summary>翻译结果缓存：原文+目标语言 → 译文。</summary>
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();

        private static readonly object CacheGate = new object();

        /// <summary>无 key 且免 key 模式关闭时的提醒（只提醒一次）。</summary>
        private static bool _warnedNoChannel;

        /// <summary>失败日志节流：30 秒内最多打一条，避免离线/限流时刷屏。</summary>
        private static DateTime _lastFailLogAt = DateTime.MinValue;

        // ===== 正式 API 响应模型 =====

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

        // ===== 网页接口 SSE 事件模型 =====

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
                // 运行时不支持时忽略，走系统默认 TLS
            }
        }

        // ===== 1. 接收翻译 =====

        /// <summary>
        /// 聊天接收入口：原文照常入队；译文作为第二条本地消息追加（仅自己可见）。
        /// 译文消息含汉字，再次入队时被本补丁的 IsChinese 判定跳过，不会递归翻译。
        /// </summary>
        /// <summary>
        /// 聊天接收入口：原文先入队显示；译文在原文之后作为第二条本地消息追加（仅自己可见）。
        /// Postfix 时机 = 原文已入队，译文排在原文下边；译文消息含汉字，再次入队时
        /// 被本补丁的 IsChinese 判定跳过，不会递归翻译。
        /// </summary>
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

                    // 已是中文直接显示原文（省额度、防重复翻译）
                    if (IsChinese(message))
                    {
                        return;
                    }

                    string translated = TranslateTo(message, ChatTranslateFeature.TargetLang);
                    if (string.IsNullOrEmpty(translated) || string.Equals(translated, message, StringComparison.Ordinal))
                    {
                        return;
                    }

                    // 追加第二条本地消息：带色[翻译版]标签 + 译文，只有本机显示，排在原文下边
                    Managers.Voice?.EnqueueNormalChat(playerId, BuildTranslationMessage(translated), isDeadByHost);
                }
                catch (Exception ex)
                {
                    Log.Warn<ChatTranslateFeature>(ex, "接收翻译处理异常");
                }
            }
        }

        // ===== 2. 发送翻译 =====

        /// <summary>发送入口：中文消息 → 目标语言广播，让所有人看到译文。</summary>
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

                    // 跳过内部指令广播（零宽前缀，如动作同步指令），避免误翻译
                    if (message[0] == '\u200B')
                    {
                        return;
                    }

                    // 只翻译中文消息；英文/韩文消息按原文发送
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

        // ===== 3. 翻译核心 =====

        /// <summary>
        /// 翻译：先查缓存（按原文+目标语言）；未命中时选择通道——
        /// 填了 appid/密钥走正式 API，否则免 key 网页接口；失败或译文与原文一致返回原文。
        /// </summary>
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

        /// <summary>百度正式 API 翻译；失败返回 null（调用方回退原文）。</summary>
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

        /// <summary>
        /// 百度网页接口翻译（免 key，SSE 流式）；未开免 key 模式或失败返回 null。
        /// 源语言本地检测（不传 auto），目标语言直接传百度代码。
        /// </summary>
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

                    // SSE 逐行解析：data: 行内是 JSON 事件；Translating 事件的 list 累积为译文
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

        // ===== 4. 工具方法 =====

        /// <summary>
        /// 构造第二条本地译文消息：带色[翻译版]标签 + 译文，走 TMP 富文本渲染；
        /// 颜色配置非法（非 #RRGGBB）时回退默认色。
        /// </summary>
        private static string BuildTranslationMessage(string translated)
        {
            string color = ChatTranslateFeature.TranslationTagColor;
            if (string.IsNullOrEmpty(color) || color.Length != 7 || color[0] != '#')
            {
                color = DefaultTagColor;
            }
            else
            {
                // 校验十六进制位
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

        /// <summary>MD5 十六进制小写（百度 sign 签名用）。</summary>
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

        /// <summary>
        /// 判断文本是否为中文：统计 CJK 相关字符，含日文假名或韩文谚文视为非中文；
        /// 汉字占非 ASCII 字符比例 &gt; 40% 才算中文（避免把日文/夹带汉字的消息当中文）。
        /// </summary>
        private static bool IsChinese(string s)
        {
            int han = 0;
            int total = 0;
            foreach (char c in s)
            {
                if (c >= '\u3040' && c <= '\u30ff')
                {
                    return false; // 含假名 → 日文
                }
                if (c >= '\uac00' && c <= '\ud7af')
                {
                    return false; // 含谚文 → 韩文
                }
                if (c < '\u0080')
                {
                    continue; // ASCII 不计入比例
                }
                total++;
                if (c >= '\u4e00' && c <= '\u9fff')
                {
                    han++;
                }
            }
            return total > 0 && (double)han / total > 0.4;
        }

        /// <summary>本地语言检测（网页接口用）：脚本正则识别，拉丁系一律按英文。</summary>
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

        /// <summary>失败日志节流：30 秒内只打一条，避免刷屏 BepInEx。</summary>
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
