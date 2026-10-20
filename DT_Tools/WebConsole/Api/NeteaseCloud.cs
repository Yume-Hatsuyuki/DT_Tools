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
    /// 网易云音乐客户端（UnityWebRequest 协程实现，全部在 Unity 主线程执行）。
    /// </summary>
    /// <remarks>
    /// 音源直链走第三方解析服务 nextmusic.toubiec.cn（免登录、免 Cookie，2026-10 实测）：
    /// POST /api/getSongInfo {id, ip} → 歌曲名/歌手；POST /api/getSongUrl {id, ip, level, timestamp}
    /// → 128kbps 直链（带签名，约 25 分钟有效，URL 含 base64 参数，必须原样使用、不可转义）。
    /// 所以播放直链不落盘——播放哪一首就现场解析哪一首（参见 /api/mp3/play 播放 https 的实现）。
    /// 歌单详情仍用官方免登录接口 GET /api/v6/playlist/detail?id={id}（≤1000 首全量）。
    /// 请求头一律带桌面 UA 与 Referer=https://music.163.com/，规避基础风控。
    /// </remarks>
    internal static class NeteaseCloud
    {
        private const string Tag = "Netease";
        private const string Referer = "https://music.163.com/";
        private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";
        private const string NextMusicBase = "https://nextmusic.toubiec.cn/api";
        private const string DetailApi = "https://music.163.com/api/v6/playlist/detail?id={0}";
        private const string DefaultIp = "1.1.1.1";      // ipinfo.io 不可达时的兜底
        private const string StandardLevel = "standard"; // 128kbps，免费曲全量可播

        /// <summary>
        /// 解析结果：单曲或歌单。
        /// </summary>
        internal sealed class SongRef
        {
            /// <summary>"song"=单曲，"playlist"=歌单。</summary>
            public string Kind = "song";
            public string Id = "";
        }

        /// <summary>
        /// 单首歌条目。
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

        private static string _publicIp = "";   // 会话内缓存（ipinfo.io 失败回落默认）

        /// <summary>
        /// 将用户输入解析为单曲/歌单引用：纯数字、song?id= 单曲链接、playlist?id= 歌单链接、
        /// 分享短链（163cn.tv 等，跟随跳转取最终 URL 再提取）。
        /// </summary>
        /// <param name="input">用户输入。</param>
        /// <param name="onDone">解析结果回调：单曲或歌单引用；失败时为 null。</param>
        /// <returns>协程。</returns>
        internal static IEnumerator ResolveInput(string input, Action<SongRef> onDone)
        {
            var text = (input ?? "").Trim();
            if (text.Length == 0)
            {
                onDone?.Invoke(null);
                yield break;
            }

            var songRef = ExtractRef(text);
            if (songRef != null)
            {
                onDone?.Invoke(songRef);
                yield break;
            }

            // 非纯链接形态（可能是分享短链）：跟随跳转拿最终 URL 再提取
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var req = UnityWebRequest.Get(text);
                req.SetRequestHeader("User-Agent", Ua);
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    songRef = ExtractRef(req.url);
                    if (songRef != null)
                    {
                        onDone?.Invoke(songRef);
                        yield break;
                    }
                }
            }

            Log.Warn(Tag, $"网易云链接解析失败: {text}");
            onDone?.Invoke(null);
        }

        /// <summary>
        /// 从任意 URL/文本提取单曲或歌单引用（playlist?id= / /playlist/ 路径 / song?id= / /song/ 路径）。
        /// </summary>
        /// <param name="text">待解析文本。</param>
        /// <returns>单曲或歌单引用；未命中时为 null。</returns>
        private static SongRef ExtractRef(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var m = Regex.Match(text, @"playlist[/?#&=]*id[=:](?<id>\d+)", RegexOptions.IgnoreCase);
            if (!m.Success)
                m = Regex.Match(text, @"/playlist/(?<id>\d+)", RegexOptions.IgnoreCase);
            if (m.Success)
                return new SongRef { Kind = "playlist", Id = m.Groups["id"].Value };

            m = Regex.Match(text, @"song[/?#&=]*id[=:](?<id>\d+)", RegexOptions.IgnoreCase);
            if (!m.Success)
                m = Regex.Match(text, @"/song/(?<id>\d+)", RegexOptions.IgnoreCase);
            if (m.Success)
                return new SongRef { Kind = "song", Id = m.Groups["id"].Value };

            // 纯数字按单曲处理
            if (Regex.IsMatch(text, @"^\d{4,}$"))
                return new SongRef { Kind = "song", Id = text };
            return null;
        }

        /// <summary>
        /// 拉取歌曲元信息（nextmusic getSongInfo）。
        /// </summary>
        /// <param name="songId">歌曲 ID。</param>
        /// <param name="onDone">结果回调：歌曲条目；失败时 Name 为空。</param>
        /// <returns>协程。</returns>
        internal static IEnumerator FetchSongInfo(long songId, Action<NetTrack> onDone)
        {
            string ip = null;
            yield return EnsurePublicIp(v => ip = v);
            var req = PostJson("/getSongInfo", $"{{\"id\":\"{songId}\",\"ip\":\"{ip}\"}}");
            yield return req.SendWebRequest();

            var track = new NetTrack { Id = songId };
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var root = JObject.Parse(req.downloadHandler.text);
                    if ((int)(root["code"] ?? -1) == 200)
                    {
                        track.Name = (string)root["data"]?["name"] ?? "";
                        track.Artist = (string)root["data"]?["singer"] ?? "";
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"歌曲信息解析失败: {ex.Message}");
                }
            }
            else
            {
                Log.Warn(Tag, $"歌曲信息请求失败: {req.error} ({songId})");
            }

            onDone?.Invoke(track);
        }

        /// <summary>
        /// 获取歌曲直链（nextmusic getSongUrl，standard=128kbps）。
        /// 返回的 URL 带签名参数（含 base64 的 +/=/），必须原样使用、不可转义，且约 25 分钟有效——
        /// 因此调用方应「播放哪一首就解析哪一首」，不要落盘直链。
        /// </summary>
        /// <param name="songId">歌曲 ID。</param>
        /// <param name="onDone">结果回调：直链 URL；失败时为 null。</param>
        /// <returns>协程。</returns>
        internal static IEnumerator FetchSongUrl(long songId, Action<string> onDone)
        {
            string ip = null;
            yield return EnsurePublicIp(v => ip = v);
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var req = PostJson("/getSongUrl",
                $"{{\"id\":\"{songId}\",\"ip\":\"{ip}\",\"level\":\"{StandardLevel}\",\"timestamp\":{ts}}}");
            yield return req.SendWebRequest();

            string url = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var root = JObject.Parse(req.downloadHandler.text);
                    if ((int)(root["code"] ?? -1) == 200)
                        url = (string)root["data"]?["url"];
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"播放链接解析失败: {ex.Message}");
                }
            }
            else
            {
                Log.Warn(Tag, $"播放链接请求失败: {req.error} ({songId})");
            }

            onDone?.Invoke(url);
        }

        /// <summary>
        /// 拉取歌单歌曲列表（官方详情接口，免登录；tracks 为空时按"歌单过大"提示）。
        /// </summary>
        /// <param name="playlistId">歌单 ID。</param>
        /// <param name="onDone">结果回调：歌单详情（含歌单名与歌曲列表，可能为空）。</param>
        /// <returns>协程。</returns>
        internal static IEnumerator FetchPlaylist(string playlistId, Action<PlaylistResult> onDone)
        {
            var req = UnityWebRequest.Get(string.Format(DetailApi, playlistId));
            req.SetRequestHeader("Referer", Referer);
            req.SetRequestHeader("User-Agent", Ua);
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
        /// 获取本机公网 IP（nextmusic 要求带真实 IP，ipinfo.io 获取失败时回落默认）。
        /// 结果缓存在会话内，避免每次解析都外呼。
        /// </summary>
        /// <param name="onDone">结果回调：公网 IP。</param>
        /// <returns>协程。</returns>
        private static IEnumerator EnsurePublicIp(Action<string> onDone)
        {
            if (!string.IsNullOrEmpty(_publicIp))
            {
                onDone?.Invoke(_publicIp);
                yield break;
            }
            var req = UnityWebRequest.Get("https://ipinfo.io/json");
            req.SetRequestHeader("User-Agent", Ua);
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var ip = (string)JObject.Parse(req.downloadHandler.text)["ip"];
                    if (!string.IsNullOrWhiteSpace(ip))
                        _publicIp = ip.Trim();
                }
                catch
                {
                    // 忽略，走默认兜底
                }
            }
            if (string.IsNullOrEmpty(_publicIp))
                _publicIp = DefaultIp;
            onDone?.Invoke(_publicIp);
        }

        /// <summary>
        /// 构造 nextmusic POST JSON 请求（application/json + 桌面 UA）。
        /// </summary>
        /// <param name="path">接口路径（如 /getSongUrl）。</param>
        /// <param name="json">JSON 请求体。</param>
        /// <returns>待发送的请求。</returns>
        private static UnityWebRequest PostJson(string path, string json)
        {
            var req = new UnityWebRequest(NextMusicBase + path, "POST")
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "*/*");
            req.SetRequestHeader("User-Agent", Ua);
            req.timeout = 15;
            return req;
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
