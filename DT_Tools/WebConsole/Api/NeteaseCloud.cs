using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DT_Tools.Core;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// 网易云音乐公开接口客户端（歌单详情 + 批量播放链接，UnityWebRequest 协程实现，
    /// 全部在 Unity 主线程执行）。播放链接接口受网易风控：未登录 Cookie 时返回 url=null
    /// （code -110），必须配置 NeteaseOptions.Cookie（网页版登录态）才能取到直链。
    /// </summary>
    /// <remarks>
    /// 接口（2026-10 实测）：
    /// 歌单详情 GET /api/v6/playlist/detail?id={id} —— 免登录可用，返回 playlist.tracks 全量（≤1000 首）；
    /// 播放链接 GET /api/song/enhance/player/url?ids=[a,b,c]&amp;br=320000 —— 批量（每批 ≤50），
    /// 需 Cookie 与 Referer，VIP/无权限歌曲返回 url=null 会被跳过。
    /// 请求头一律带 Referer=https://music.163.com/ 与桌面 UA，规避基础风控。
    /// </remarks>
    internal static class NeteaseCloud
    {
        private const string Tag = "Netease";
        private const string Referer = "https://music.163.com/";
        private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";
        private const string DetailApi = "https://music.163.com/api/v6/playlist/detail?id={0}";
        private const string PlayUrlApi = "https://music.163.com/api/song/enhance/player/url?ids={0}&br=320000";
        private const int BatchSize = 50;   // 播放链接接口单次批量上限

        /// <summary>
        /// 单首歌条目（详情接口解析结果）。
        /// </summary>
        internal sealed class NetTrack
        {
            public long Id;
            public string Name = "";
            public string Artist = "";
        }

        /// <summary>
        /// 歌单详情结果。
        /// </summary>
        internal sealed class PlaylistResult
        {
            public string Name = "";
            public List<NetTrack> Tracks = new List<NetTrack>();
        }

        private static void ApplyHeaders(UnityWebRequest req)
        {
            req.SetRequestHeader("Referer", Referer);
            req.SetRequestHeader("User-Agent", Ua);
            var cookie = NeteaseOptions.Cookie;
            if (!string.IsNullOrWhiteSpace(cookie))
                req.SetRequestHeader("Cookie", cookie.Trim());
        }

        /// <summary>
        /// 将用户输入解析为歌单 ID：纯数字、标准链接（playlist?id=）、hash 链接（#/playlist?id=）、
        /// 分享短链（163cn.tv 等，跟随跳转取最终 URL 再提取）。
        /// </summary>
        /// <param name="input">用户输入。</param>
        /// <param name="onDone">解析结果回调：歌单 ID；失败时为 null。</param>
        /// <returns>协程。</returns>
        internal static IEnumerator ResolvePlaylistId(string input, Action<string> onDone)
        {
            var text = (input ?? "").Trim();
            if (text.Length == 0)
            {
                onDone?.Invoke(null);
                yield break;
            }

            var id = ExtractId(text);
            if (id != null)
            {
                onDone?.Invoke(id);
                yield break;
            }

            // 非纯链接形态（可能是分享短链）：跟随跳转拿最终 URL 再提取
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var req = UnityWebRequest.Get(text);
                ApplyHeaders(req);
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    id = ExtractId(req.url);
                    if (id != null)
                    {
                        onDone?.Invoke(id);
                        yield break;
                    }
                }
            }

            Log.Warn(Tag, $"歌单链接解析失败: {text}");
            onDone?.Invoke(null);
        }

        /// <summary>
        /// 从任意 URL/文本提取歌单 ID（playlist?id= 或 /playlist/ 路径形式）。
        /// </summary>
        /// <param name="text">待解析文本。</param>
        /// <returns>歌单 ID；未命中时为 null。</returns>
        private static string ExtractId(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var m = Regex.Match(text, @"playlist[/?#&=]*id[=:](?<id>\d+)", RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups["id"].Value;
            // 纯数字
            if (Regex.IsMatch(text, @"^\d{4,}$"))
                return text;
            return null;
        }

        /// <summary>
        /// 拉取歌单歌曲列表（详情接口；tracks 为空时按"歌单过大"提示）。
        /// </summary>
        /// <param name="playlistId">歌单 ID。</param>
        /// <param name="onDone">结果回调：歌单详情（含歌单名与歌曲列表，可能为空）。</param>
        /// <returns>协程。</returns>
        internal static IEnumerator FetchPlaylist(string playlistId, Action<PlaylistResult> onDone)
        {
            var req = UnityWebRequest.Get(string.Format(DetailApi, playlistId));
            ApplyHeaders(req);
            req.timeout = 15;
            yield return req.SendWebRequest();

            var result = new PlaylistResult();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var root = JObject.Parse(req.downloadHandler.text);
                    if ((int)(root["code"] ?? -1) == 200)
                    {
                        result.Name = (string)root["playlist"]?["name"] ?? "";
                        var arr = root["playlist"]?["tracks"] as JArray;
                        if (arr != null)
                        {
                            foreach (var t in arr)
                            {
                                result.Tracks.Add(new NetTrack
                                {
                                    Id = (long)(t["id"] ?? 0L),
                                    Name = (string)t["name"] ?? "",
                                    Artist = JoinArtists(t["ar"] as JArray),
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"歌单详情解析失败: {ex.Message}");
                }
            }
            else
            {
                Log.Warn(Tag, $"歌单详情请求失败: {req.error} ({playlistId})");
            }

            onDone?.Invoke(result);
        }

        /// <summary>
        /// 批量获取歌曲播放链接（每批 ≤50，串行请求；无权限/未登录返回 null 的歌曲跳过）。
        /// </summary>
        /// <param name="ids">歌曲 ID 列表。</param>
        /// <param name="onDone">结果回调：id → 播放链接（仅包含解析成功的歌曲）。</param>
        /// <returns>协程。</returns>
        internal static IEnumerator FetchPlayUrls(IReadOnlyList<long> ids, Action<Dictionary<long, string>> onDone)
        {
            var urls = new Dictionary<long, string>();
            for (int offset = 0; offset < ids.Count; offset += BatchSize)
            {
                int take = Math.Min(BatchSize, ids.Count - offset);
                var batch = new List<long>();
                for (int i = 0; i < take; i++)
                    batch.Add(ids[offset + i]);

                string query = "[" + string.Join(",", batch) + "]";
                var req = UnityWebRequest.Get(string.Format(PlayUrlApi, Uri.EscapeDataString(query)));
                ApplyHeaders(req);
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var root = JObject.Parse(req.downloadHandler.text);
                        var arr = root["data"] as JArray;
                        if (arr != null)
                        {
                            foreach (var item in arr)
                            {
                                long id = (long)(item["id"] ?? 0L);
                                string url = (string)item["url"];
                                if (id != 0L && !string.IsNullOrEmpty(url))
                                    urls[id] = url;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"播放链接解析失败（批次 {offset / BatchSize + 1}）: {ex.Message}");
                    }
                }
                else
                {
                    Log.Warn(Tag, $"播放链接请求失败（批次 {offset / BatchSize + 1}）: {req.error}");
                }
            }

            onDone?.Invoke(urls);
        }

        private static string JoinArtists(JArray artists)
        {
            if (artists == null || artists.Count == 0)
                return "";
            var names = new List<string>();
            foreach (var a in artists)
            {
                var name = (string)a?["name"];
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
            return string.Join("/", names);
        }
    }
}
