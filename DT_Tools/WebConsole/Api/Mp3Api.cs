using System;
using System.Net;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Game;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// /api/mp3/* — 随身MP3 应用后端：驱动 Game/MicBroadcast 的会话（与 /mic_music、
    /// StageMusic 共用同一引擎，后触发者接管）加传输控制（暂停/跳转/响度/麦克风混入
    /// 开关/本地静音），歌单库（多歌单）持久化在 Mp3Playlist（DtPaths.Mp3Playlist）。
    /// </summary>
    /// <remarks>
    /// 播放即入歌单（同一歌单内去重）；自然播完经 MicBroadcast.TrackFinished 按播放上下文的歌单自动连播。
    /// 播放上下文（正在播放的歌单）与界面当前显示的歌单相互独立：浏览别的歌单不打断播放。
    /// 全部端点经 WebConsole.RunOnMain 在 Unity 主线程执行（引擎与歌单都只碰主线程），
    /// 响应沿用命令信封 {ok, error, data}；错误码小写英文短词，中文详情在 data.message。
    /// </remarks>
    public static class Mp3Api
    {
        private const string Tag = "Mp3";

        private static readonly Random _rnd = new Random();

        // 当前播放曲目（原始路径，非 file:/// URI）及其所属歌单；会话空闲时无意义
        private static string _currentPath;
        private static long _resolveToken;
        private static CancellationTokenSource _resolveCts;
        private static int _resolveFailStreak;
        private static string _playingListId;

        // 暂停书签（暂停=销毁引擎会话，恢复=PlayTrack 重建+起始偏移）。
        // 书签只在引擎 idle 时有效——其他入口（/mic_music）接管会话即作废。
        private static string _pausedPath;
        private static string _pausedListId;
        private static float _pausedPosition;
        private static float _pausedDuration;

        /// <summary>
        /// GET /api/mp3/state — 引擎会话快照 + 暂停书签 + 歌单库 + 播放上下文。
        /// </summary>
        /// <remarks>
        /// volume / mic / local / mode 取持久化偏好（空闲时引擎不持有这些值，且开关必须反映用户意图）；
        /// micActive 才是当前会话是否真的在混入麦克风。
        /// playlist 为界面当前显示的歌单；index 与 playingListId 描述的是「正在播放的那一首」，
        /// 客户端只有在 playingListId 等于 activeId 时才应高亮 index。
        /// </remarks>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleState(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                var store = Mp3Playlist.Get();
                var active = Mp3Playlist.Active();
                var engine = MicBroadcast.Snapshot();
                bool paused = _pausedPath != null && engine.Phase == "idle";
                if (!paused && _pausedPath != null)
                    _pausedPath = null;   // 会话被接管/正在重建，书签过期

                string playingListId = paused ? _pausedListId : (OwnsSession() ? _playingListId : null);
                string playingPath = paused ? _pausedPath : (OwnsSession() ? _currentPath : null);
                var playingList = Mp3Playlist.Find(playingListId);
                int index = IndexOfPath(playingList, playingPath);
                string playingLabel = index >= 0 ? playingList.Items[index].Label : "";

                return CommandResult.Success(new
                {
                    Phase = paused ? "paused" : engine.Phase,
                    Position = paused ? _pausedPosition : engine.Position,
                    Duration = paused ? _pausedDuration : engine.Duration,
                    Volume = store.Volume,
                    Mic = store.Mic,
                    MicActive = engine.Mic,
                    Local = store.Local,
                    Mode = store.Mode,
                    ActiveId = active.Id,
                    Lists = store.Lists.ConvertAll(l => new { l.Id, l.Name, Count = l.Items.Count }).ToArray(),
                    PlayingListId = playingListId ?? "",
                    PlayingLabel = playingLabel,
                    Index = index,
                    Playlist = active.Items.ToArray(),
                });
            });

        /// <summary>
        /// POST /api/mp3/play {path, listId?} — 播放（本地路径或 http(s)），入指定歌单（缺省为当前歌单）并去重。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandlePlay(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string path = (body.Value<string>("path") ?? "").Trim();
            if (path.Length == 0)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing path"));
                return;
            }
            string listId = body.Value<string>("listId");
            WriteMain(ctx, () =>
            {
                var list = Mp3Playlist.Resolve(listId);
                if (list == null)
                    return ListError("歌单不存在");
                int index = Mp3Playlist.Ensure(list.Id, path);
                return PlayInternal(list.Id, path, index);
            });
        }

        /// <summary>
        /// POST /api/mp3/index {index, listId?} — 播放指定歌单的第 index 首（缺省为当前歌单）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleIndex(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int index = ReadInt(body, "index");
            string listId = body.Value<string>("listId");
            WriteMain(ctx, () =>
            {
                var list = Mp3Playlist.Resolve(listId);
                if (list == null)
                    return ListError("歌单不存在");
                if (index < 0 || index >= list.Items.Count)
                    return ListError("歌单序号越界");
                return PlayInternal(list.Id, list.Items[index].Path, index);
            });
        }

        /// <summary>
        /// POST /api/mp3/next {delta} — 跳转 ±1（默认 +1），在播放上下文的歌单内循环。
        /// </summary>
        /// <remarks>
        /// 空闲时在当前歌单内跳转；暂停时以暂停的那首为起点。
        /// </remarks>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleNext(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int delta = body["delta"] == null ? 1 : ReadInt(body, "delta");
            if (delta == 0) delta = 1;
            WriteMain(ctx, () =>
            {
                var list = PlaybackList(out int index);
                var items = list.Items;
                if (items.Count == 0)
                    return CommandResult.Fail("empty playlist", new { message = "歌单为空" });
                int target = index < 0
                    ? (delta > 0 ? 0 : items.Count - 1)
                    : ((index + delta) % items.Count + items.Count) % items.Count;
                return PlayInternal(list.Id, items[target].Path, target);
            });
        }

        /// <summary>
        /// POST /api/mp3/pause — 暂停：记住进度并销毁引擎会话（还原语音管线，不留挂起状态）；恢复时从书签位置重建。
        /// </summary>
        /// <remarks>
        /// 幂等：已暂停返回成功。只能暂停本入口发起的会话。
        /// </remarks>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandlePause(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                if (_pausedPath != null)
                    return CommandResult.Success(new { message = "已暂停" });
                var engine = MicBroadcast.Snapshot();
                if (engine.Phase != "playing" || string.IsNullOrEmpty(_currentPath) || !OwnsSession())
                    return CommandResult.Fail("not playing", new { message = "当前没有可暂停的会话" });
                _pausedPath = _currentPath;
                _pausedListId = _playingListId;
                _pausedPosition = engine.Position;
                _pausedDuration = engine.Duration;
                CancelPendingResolve(); MicBroadcast.Stop();
                Log.Info(Tag, $"已暂停：{System.IO.Path.GetFileName(_pausedPath)} @ {_pausedPosition:0.#}s（会话已销毁）");
                return CommandResult.Success(new { message = "已暂停" });
            });

        /// <summary>
        /// POST /api/mp3/resume — 恢复：从书签位置重建会话。
        /// </summary>
        /// <remarks>
        /// 幂等：已在播放返回成功。暂停所在的歌单若已被删除，则无法恢复并清除书签。
        /// </remarks>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleResume(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                if (_pausedPath != null)
                {
                    string path = _pausedPath;
                    float pos = _pausedPosition;
                    var list = Mp3Playlist.Find(_pausedListId);
                    if (list == null)
                    {
                        _pausedPath = null;
                        _pausedListId = null;
                        return ListError("歌单不存在");
                    }
                    return PlayInternal(list.Id, path, IndexOfPath(list, path), pos);
                }
                if (MicBroadcast.Snapshot().Phase != "idle")
                    return CommandResult.Success(new { message = "已在播放中" });
                return CommandResult.Fail("nothing to resume", new { message = "没有可恢复的暂停会话" });
            });

        /// <summary>
        /// POST /api/mp3/seek {position} — 跳转（秒）：播放中走引擎；暂停中更新书签。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleSeek(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            float position = ReadFloat(body, "position");
            WriteMain(ctx, () =>
            {
                if (_pausedPath != null)
                {
                    _pausedPosition = Math.Clamp(position, 0f, Math.Max(0f, _pausedDuration - 0.05f));
                    return CommandResult.Success(new { position = _pausedPosition });
                }
                return MicBroadcast.Seek(position)
                    ? CommandResult.Success(new { position })
                    : CommandResult.Fail("no session", new { message = "当前没有播放中的会话" });
            });
        }

        /// <summary>
        /// POST /api/mp3/volume {volume} — 响度 0~1（实时生效并持久化）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleVolume(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            float volume = ReadFloat(body, "volume");
            WriteMain(ctx, () =>
            {
                MicBroadcast.SetVolume(volume);
                Mp3Playlist.Get().Volume = volume;
                Mp3Playlist.Save();
                return CommandResult.Success(new { volume });
            });
        }

        /// <summary>
        /// POST /api/mp3/mic {enabled} — 麦克风混入开关（偏好持久化；播放中即时挂/卸）。
        /// </summary>
        /// <remarks>
        /// 开关表示用户意图，偏好总是先记录。语音未连接时混入只是待命，本地播放不受影响。
        /// 加载中不切换（本曲的混入状态已在开始加载时确定）。
        /// </remarks>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleMic(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            bool? enabled = ApiUtil.ReadBool(body, "enabled");
            if (enabled == null)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing enabled"));
                return;
            }
            WriteMain(ctx, () =>
            {
                var engine = MicBroadcast.Snapshot();
                if (engine.Phase == "loading")
                    return CommandResult.Fail("loading", new { message = "曲目加载中，加载完成后再切换混入" });

                var store = Mp3Playlist.Get();
                store.Mic = enabled.Value;
                Mp3Playlist.Save();
                if (engine.Phase == "idle")
                    return CommandResult.Success(new { message = "已记录混入偏好（下次播放生效）", mic = enabled.Value, micActive = false });

                MicBroadcast.SetMicMix(enabled.Value);
                bool active = MicBroadcast.Snapshot().Mic;
                string message = !enabled.Value ? "麦克风混入已关闭（本地继续播放）"
                    : active ? "麦克风混入已开启"
                    : "语音未连接，混入待命（本地继续播放；语音就绪后关开一次即可混入）";
                return CommandResult.Success(new { message, mic = enabled.Value, micActive = active });
            });
        }

        /// <summary>
        /// POST /api/mp3/local {enabled} — 本地喇叭出声开关（mute 实现，持久化）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleLocal(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            bool? enabled = ApiUtil.ReadBool(body, "enabled");
            if (enabled == null)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing enabled"));
                return;
            }
            WriteMain(ctx, () =>
            {
                MicBroadcast.SetLocalAudible(enabled.Value);
                Mp3Playlist.Get().Local = enabled.Value;
                Mp3Playlist.Save();
                return CommandResult.Success(new
                {
                    message = enabled.Value ? "本地喇叭已出声" : "本地喇叭已静音（进度照走）",
                    local = enabled.Value,
                });
            });
        }

        /// <summary>
        /// POST /api/mp3/mode {mode} — 播放方式：list=列表循环 | single=单曲循环 | random=随机播放。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleMode(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string mode = (body.Value<string>("mode") ?? "").Trim();
            WriteMain(ctx, () =>
            {
                if (mode != "list" && mode != "single" && mode != "random")
                    return CommandResult.Fail("invalid mode", new { message = "播放方式仅支持 list/single/random" });
                Mp3Playlist.Get().Mode = mode;
                Mp3Playlist.Save();
                return CommandResult.Success(new { message = ModeText(mode), mode });
            });
        }

        private static string ModeText(string mode)
            => mode == "single" ? "单曲循环（重播当前）" : mode == "random" ? "随机播放" : "列表循环";

        /// <summary>
        /// POST /api/mp3/stop — 停止会话并清除暂停书签与播放上下文。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleStop(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                CancelPendingResolve(); MicBroadcast.Stop();
                _currentPath = null;
                _playingListId = null;
                _pausedPath = null;
                _pausedListId = null;
                return CommandResult.Success(new { message = "已停止" });
            });

        /// <summary>
        /// POST /api/mp3/playlist/add {path, listId?} — 入指定歌单（缺省为当前歌单），同一歌单内按路径去重。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandlePlaylistAdd(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string kind = (body.Value<string>("kind") ?? string.Empty).Trim().ToLowerInvariant();
            string input = (body.Value<string>("path") ?? body.Value<string>("input") ?? string.Empty).Trim();
            string name = (body.Value<string>("name") ?? string.Empty).Trim();
            string listId = body.Value<string>("listId");
            if (kind.Length == 0)
                kind = Mp3Playlist.InferKind(input);   // 旧客户端只传 path

            string path;
            string label;
            if (kind == "netease")
            {
                if (!NeteaseMusic.TryParseSongId(input, out string songId))
                {
                    HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid netease link", new { message = "无法识别网易云歌曲链接或 ID" }));
                    return;
                }
                path = "netease:" + songId;
                label = name.Length > 0 ? name : "网易云 " + songId;
            }
            else if (kind == "https")
            {
                if (!Uri.TryCreate(input, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid url", new { message = "请输入 http(s) 链接" }));
                    return;
                }
                if (name.Length == 0)
                {
                    HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing name", new { message = "请填写名字" }));
                    return;
                }
                path = input;
                label = name;
            }
            else
            {
                if (input.Length == 0)
                {
                    HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing path"));
                    return;
                }
                kind = "local";
                path = input;
                // 可选名字：不填则 Ensure 内用文件名；填写则作为展示 Label
                label = name.Length > 0 ? name : null;
            }

            WriteMain(ctx, () =>
            {
                var list = Mp3Playlist.Resolve(listId);
                if (list == null)
                    return ListError("歌单不存在");
                string level = (body.Value<string>("level") ?? string.Empty).Trim();
                int index = Mp3Playlist.Ensure(list.Id, path, kind, label, level);
                return CommandResult.Success(new { index, message = "已在歌单中" });
            });
        }

        /// <summary>
        /// POST /api/mp3/netease/info {input, ipMode?, fallbackIp?} — 解析网易云链接并返回歌曲信息（用于预填名字）。在 HTTP 线程执行网络请求。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandleNeteaseInfo(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string input = (body.Value<string>("input") ?? string.Empty).Trim();
            if (!NeteaseMusic.TryParseSongId(input, out string songId))
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid netease link", new { message = "无法识别网易云歌曲链接或 ID" }));
                return;
            }
            string mode = body.Value<string>("ipMode") ?? NeteaseMusic.IpModeIpinfo;
            string fallback = body.Value<string>("fallbackIp") ?? NeteaseMusic.DefaultFallbackIp;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25)))
                {
                    string ip = NeteaseMusic.ResolveRequestIpAsync(mode, fallback, cts.Token).GetAwaiter().GetResult();
                    var info = NeteaseMusic.GetSongInfoAsync(songId, ip, cts.Token).GetAwaiter().GetResult();
                    HttpServer.WriteJson(ctx.Response, CommandResult.Success(new { id = songId, name = info.Name, singer = info.Singer, duration = info.Duration }));
                }
            }
            catch (Exception e)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("netease info failed", new { message = "获取歌曲信息失败：" + e.Message }));
            }
        }

        /// <summary>
        /// GET /api/mp3/netease/settings — 读取网易云设置（IP 模式、回退/自定义 IP、音质、UA）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandleNeteaseSettings(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                var s = Mp3Playlist.Get();
                // levels：兼容旧前端；levelOptions：带中文显示名，与 nextmusic-dl.mjs 一致
                var levelOptions = new System.Collections.Generic.List<object>();
                foreach (string lv in NeteaseMusic.Levels)
                    levelOptions.Add(new { value = lv, label = NeteaseMusic.LevelLabel(lv) });
                return CommandResult.Success(new
                {
                    ipMode = s.NeteaseIpMode,
                    fallbackIp = s.NeteaseFallbackIp,
                    level = s.NeteaseLevel,
                    userAgent = s.NeteaseUserAgent,
                    levels = NeteaseMusic.Levels,
                    levelOptions = levelOptions,
                    defaultFallbackIp = NeteaseMusic.DefaultFallbackIp,
                    defaultUserAgent = NeteaseMusic.DefaultUserAgent,
                });
            });

        /// <summary>
        /// POST /api/mp3/netease/settings {ipMode?, fallbackIp?, level?, userAgent?} — 保存网易云设置。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandleNeteaseSettingsSet(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string mode = body.Value<string>("ipMode");
            string fallback = body.Value<string>("fallbackIp");
            string level = body.Value<string>("level");
            string ua = body.Value<string>("userAgent");
            WriteMain(ctx, () =>
            {
                if (fallback != null && !NeteaseMusic.IsValidIp(fallback))
                    return CommandResult.Fail("invalid ip", new { message = "IP 格式不正确" });
                var s = Mp3Playlist.Get();
                if (mode != null) s.NeteaseIpMode = NeteaseMusic.NormalizeIpMode(mode);
                if (fallback != null) s.NeteaseFallbackIp = fallback.Trim();
                if (level != null) s.NeteaseLevel = NeteaseMusic.NormalizeLevel(level);
                if (ua != null) s.NeteaseUserAgent = ua.Trim().Length > 0 ? ua.Trim() : NeteaseMusic.DefaultUserAgent;
                Mp3Playlist.Save();
                return CommandResult.Success(new { message = "网易云设置已保存" });
            });
        }

        /// <summary>
        /// POST /api/mp3/playlist/alias {listId?, index, alias} — 设置曲目的自定义标签（不改文件名）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文。</param>
        public static void HandlePlaylistAlias(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string listId = body.Value<string>("listId");
            int index = body.Value<int?>("index") ?? -1;
            string alias = body.Value<string>("alias") ?? string.Empty;
            WriteMain(ctx, () =>
            {
                if (!Mp3Playlist.SetAlias(listId, index, alias, out string error))
                    return ListError(error);
                return CommandResult.Success(new { message = "标签已更新" });
            });
        }

        /// <summary>
        /// POST /api/mp3/playlist/update {listId?, index, name?, alias?, path?, level?}
        /// 编辑曲目：名字/标签；https 可改链接；网易云可改链接或 ID 与音质。
        /// </summary>
        public static void HandlePlaylistUpdate(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string listId = body.Value<string>("listId");
            int index = body.Value<int?>("index") ?? -1;
            // null = 本次不改该字段；缺省字段在 JSON 里为 null
            bool hasName = body["name"] != null;
            bool hasAlias = body["alias"] != null;
            bool hasPath = body["path"] != null || body["input"] != null;
            bool hasLevel = body["level"] != null;
            string name = hasName ? (body.Value<string>("name") ?? "") : null;
            string alias = hasAlias ? (body.Value<string>("alias") ?? "") : null;
            string level = hasLevel ? (body.Value<string>("level") ?? "") : null;
            string newPath = null;
            if (hasPath)
            {
                string input = (body.Value<string>("path") ?? body.Value<string>("input") ?? "").Trim();
                // 根据当前曲目类型或输入推断；网易云输入可写链接或 ID
                WriteMain(ctx, () =>
                {
                    var list = Mp3Playlist.Resolve(listId);
                    if (list == null)
                        return ListError("歌单不存在");
                    if (index < 0 || index >= list.Items.Count)
                        return ListError("曲目下标无效");
                    var item = list.Items[index];
                    string kind = string.IsNullOrEmpty(item.Kind) ? Mp3Playlist.InferKind(item.Path) : item.Kind;
                    string pathToSet = null;
                    if (input.Length > 0)
                    {
                        if (kind == "netease" || NeteaseMusic.TryParseSongId(input, out _))
                        {
                            if (!NeteaseMusic.TryParseSongId(input, out string songId))
                                return CommandResult.Fail("invalid netease link", new { message = "无法识别网易云歌曲链接或 ID" });
                            pathToSet = "netease:" + songId;
                        }
                        else if (kind == "https" || input.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (!System.Uri.TryCreate(input, System.UriKind.Absolute, out var uri) ||
                                (uri.Scheme != System.Uri.UriSchemeHttp && uri.Scheme != System.Uri.UriSchemeHttps))
                                return CommandResult.Fail("invalid url", new { message = "请输入有效的 http(s) 链接" });
                            pathToSet = input;
                        }
                        else if (kind == "local")
                        {
                            pathToSet = input; // 一般不改本地路径，但允许
                        }
                        else
                        {
                            return CommandResult.Fail("invalid path", new { message = "无法识别的路径或链接" });
                        }
                    }
                    if (!Mp3Playlist.UpdateItem(list.Id, index, name, alias, pathToSet, level, out string error))
                        return ListError(error);
                    return CommandResult.Success(new { message = "曲目已更新" });
                });
                return;
            }
            WriteMain(ctx, () =>
            {
                if (!Mp3Playlist.UpdateItem(listId, index, name, alias, null, level, out string error))
                    return ListError(error);
                return CommandResult.Success(new { message = "曲目已更新" });
            });
        }

        /// <summary>
        /// 网易云条目的播放：后台线程解析直链，结果回到主线程后再交给引擎播放（不阻塞主线程）。
        /// </summary>
        /// <param name="listId">曲目所属歌单的 id。</param>
        /// <param name="path">netease:&lt;id&gt;。</param>
        /// <param name="index">曲目下标。</param>
        /// <param name="startSeconds">起播位置，单位秒。</param>
        /// <returns>已开始解析的结果。</returns>
        private static CommandResult StartNeteaseTrack(string listId, string path, int index, float startSeconds)
        {
            var store = Mp3Playlist.Get();
            string songId = path.Substring("netease:".Length);
            string ipMode = store.NeteaseIpMode;
            string fallback = store.NeteaseFallbackIp;
            var list = Mp3Playlist.Find(listId);
            var item = (list != null && index >= 0 && index < list.Items.Count) ? list.Items[index] : null;
            // 曲目级音质优先，空则回退全局 NeteaseLevel
            string level = Mp3Playlist.ResolvePlayLevel(item, store);
            long token = ++_resolveToken;
            _resolveCts?.Cancel();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _resolveCts = cts;
            Task<NeteaseMusic.SongUrl> task = Task.Run(async () =>
            {
                string ip = await NeteaseMusic.ResolveRequestIpAsync(ipMode, fallback, cts.Token).ConfigureAwait(false);
                return await NeteaseMusic.GetSongUrlAsync(songId, ip, level, cts.Token).ConfigureAwait(false);
            });
            CoroutineHost.Start(CoAwaitNetease(task, token, listId, path, startSeconds));
            return CommandResult.Success(new { message = $"正在解析网易云音乐 {songId}…", index });
        }

        private static IEnumerator CoAwaitNetease(Task<NeteaseMusic.SongUrl> task, long token, string listId, string path, float startSeconds)
        {
            while (!task.IsCompleted)
                yield return null;
            if (token != _resolveToken)
                yield break;   // 已被停止或被新的播放取代
            if (task.IsFaulted || task.IsCanceled)
            {
                string reason = task.IsFaulted ? task.Exception?.GetBaseException().Message : "超时";
                Log.Warn(Tag, $"网易云解析失败（{path}）：{reason}");
                ContinueAfterFailure(listId, path);
                yield break;
            }
            var url = task.Result;
            var store = Mp3Playlist.Get();
            if (!MicBroadcast.PlayTrack(url.Url, store.Volume, store.Mic, startSeconds, store.NeteaseUserAgent))
            {
                Log.Warn(Tag, $"网易云直链无法播放（{path}）");
                ContinueAfterFailure(listId, path);
                yield break;
            }
            _resolveFailStreak = 0;
            _currentPath = path;
            _playingListId = listId;
            _pausedPath = null;
            _pausedListId = null;
            MicBroadcast.TrackFinished = OnTrackFinished;
            MicBroadcast.SetLocalAudible(store.Local);
            Log.Info(Tag, $"网易云直链已就绪：{path}（{url.Level}，{url.Bitrate / 1000} kbps）");
        }

        /// <summary>
        /// 解析或播放失败后跳过这一首继续连播；连续失败数达到歌单长度时停止，防止死循环。
        /// </summary>
        /// <param name="listId">歌单 id。</param>
        /// <param name="path">失败的曲目路径。</param>
        private static void ContinueAfterFailure(string listId, string path)
        {
            var list = Mp3Playlist.Find(listId);
            _resolveFailStreak++;
            if (list == null || _resolveFailStreak >= list.Items.Count)
            {
                _resolveFailStreak = 0;
                Log.Warn(Tag, "连续解析失败，已停止自动连播。");
                return;
            }
            _currentPath = path;
            _playingListId = listId;
            OnTrackFinished();
        }

        /// <summary>
        /// 取消未完成的网易云解析：令牌递增，过期结果会被丢弃。
        /// </summary>
        private static void CancelPendingResolve()
        {
            _resolveToken++;
            _resolveCts?.Cancel();
            _resolveCts = null;
        }

        /// <summary>
        /// POST /api/mp3/playlist/remove {index, listId?} — 从指定歌单移除（当前播放中的曲目也可移除，播放不受影响）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandlePlaylistRemove(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int index = ReadInt(body, "index");
            string listId = body.Value<string>("listId");
            WriteMain(ctx, () =>
            {
                if (!Mp3Playlist.RemoveAt(listId, index, out string error))
                    return ListError(error);
                return CommandResult.Success(new { message = "已移除" });
            });
        }

        /// <summary>
        /// POST /api/mp3/playlist/move {from, to, listId?} — 在指定歌单内调序。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandlePlaylistMove(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int from = ReadInt(body, "from");
            int to = ReadInt(body, "to");
            string listId = body.Value<string>("listId");
            WriteMain(ctx, () =>
            {
                if (!Mp3Playlist.Move(listId, from, to, out string error))
                    return ListError(error);
                return CommandResult.Success(new { message = "已调整顺序" });
            });
        }

        /// <summary>
        /// POST /api/mp3/playlist/clear {listId?} — 清空指定歌单（不停止当前播放）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandlePlaylistClear(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                string listId = ApiUtil.ParseBody(ctx)?.Value<string>("listId");
                if (!Mp3Playlist.Clear(listId, out string error))
                    return ListError(error);
                return CommandResult.Success(new { message = "歌单已清空" });
            });

        /// <summary>
        /// POST /api/mp3/lists/create {name} — 新建歌单，并设为当前歌单。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleListCreate(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string name = body.Value<string>("name") ?? "";
            WriteMain(ctx, () =>
            {
                var list = Mp3Playlist.CreateList(name, out string error);
                if (list == null)
                    return ListError(error);
                return CommandResult.Success(new { id = list.Id, name = list.Name, message = $"已新建歌单「{list.Name}」" });
            });
        }

        /// <summary>
        /// POST /api/mp3/lists/rename {id, name} — 重命名歌单。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleListRename(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string id = body.Value<string>("id") ?? "";
            string name = body.Value<string>("name") ?? "";
            WriteMain(ctx, () =>
            {
                if (!Mp3Playlist.RenameList(id, name, out string error))
                    return ListError(error);
                return CommandResult.Success(new { message = "已重命名" });
            });
        }

        /// <summary>
        /// POST /api/mp3/lists/delete {id} — 删除歌单（至少保留一个）。
        /// </summary>
        /// <remarks>
        /// 若被删歌单正在播放，先停止播放；若是暂停所在的歌单，清除暂停书签。
        /// </remarks>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleListDelete(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string id = body.Value<string>("id") ?? "";
            WriteMain(ctx, () =>
            {
                bool playingHere = OwnsSession() && _playingListId == id;
                if (!Mp3Playlist.DeleteList(id, out string error))
                    return ListError(error);
                if (playingHere)
                {
                    CancelPendingResolve(); MicBroadcast.Stop();
                    _currentPath = null;
                    _playingListId = null;
                }
                if (_pausedListId == id)
                {
                    _pausedPath = null;
                    _pausedListId = null;
                }
                return CommandResult.Success(new { activeId = Mp3Playlist.Active().Id, message = "歌单已删除" });
            });
        }

        /// <summary>
        /// POST /api/mp3/lists/select {id} — 切换界面当前显示的歌单（不影响播放）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleListSelect(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string id = body.Value<string>("id") ?? "";
            WriteMain(ctx, () =>
            {
                if (!Mp3Playlist.SelectList(id))
                    return ListError("歌单不存在");
                return CommandResult.Success(new { activeId = id });
            });
        }

        /// <summary>
        /// 启动播放并挂连播回调；静音偏好立即生效（加载完成应用 mute，避免先响一下）。
        /// </summary>
        /// <remarks>
        /// 任何新播放都使暂停书签作废；startSeconds&gt;0 时从该位置重建（恢复暂停）。
        /// 播放上下文记录为 listId。
        /// </remarks>
        /// <param name="listId">曲目所属歌单的 id。</param>
        /// <param name="path">曲目路径或链接。</param>
        /// <param name="index">曲目在该歌单中的下标。</param>
        /// <param name="startSeconds">起播位置，单位秒。</param>
        /// <returns>命令执行结果。</returns>
        private static CommandResult PlayInternal(string listId, string path, int index, float startSeconds = 0f)
        {
            CancelPendingResolve();
            if (Mp3Playlist.InferKind(path) == "netease")
                return StartNeteaseTrack(listId, path, index, startSeconds);
            var store = Mp3Playlist.Get();
            if (!MicBroadcast.PlayTrack(path, store.Volume, store.Mic, startSeconds))
                return CommandResult.Fail("invalid source", new { message = "播放失败：音频来源无效（文件不存在/路径非法，原因见 MicBroadcast 日志）" });
            _currentPath = path;
            _playingListId = listId;
            _pausedPath = null;
            _pausedListId = null;
            MicBroadcast.TrackFinished = OnTrackFinished;
            MicBroadcast.SetLocalAudible(store.Local);
            return CommandResult.Success(new
            {
                message = store.Mic ? "开始播放（语音已连接时混入麦克风，否则仅本地）" : "开始播放（仅本地）",
                index,
            });
        }

        /// <summary>
        /// 自然播完自动连播：在播放上下文的歌单内，按播放方式选下一首。
        /// </summary>
        /// <remarks>
        /// 注意：引擎回调发生在 Stop() 之后（phase 已 idle），当前曲目必须直接按路径查表，
        /// 不能依赖会话状态守卫。播放方式的选择逻辑见 <see cref="Mp3Playlist.NextIndex"/>。
        /// </remarks>
        private static void OnTrackFinished()
        {
            var list = Mp3Playlist.Find(_playingListId) ?? Mp3Playlist.Active();
            var items = list.Items;
            if (items.Count == 0)
                return;
            int index = IndexOfPath(list, _currentPath);
            int target = Mp3Playlist.NextIndex(items.Count, Mp3Playlist.Get().Mode, index, _rnd);
            Log.Info(Tag, $"自动连播：{items[target].Label}");
            PlayInternal(list.Id, items[target].Path, target);
        }

        /// <summary>
        /// 当前「播放上下文」：暂停中为暂停的那首，播放中为正在播的那首，空闲时为当前显示的歌单（下标 -1）。
        /// </summary>
        /// <param name="index">上下文中当前曲目的下标；没有当前曲目时为 -1。</param>
        /// <returns>播放上下文所属的歌单。</returns>
        private static Mp3Playlist.ListEntry PlaybackList(out int index)
        {
            if (_pausedPath != null && MicBroadcast.Snapshot().Phase == "idle")
            {
                var paused = Mp3Playlist.Find(_pausedListId) ?? Mp3Playlist.Active();
                index = IndexOfPath(paused, _pausedPath);
                return paused;
            }
            if (OwnsSession())
            {
                var playing = Mp3Playlist.Find(_playingListId) ?? Mp3Playlist.Active();
                index = IndexOfPath(playing, _currentPath);
                return playing;
            }
            index = -1;
            return Mp3Playlist.Active();
        }

        /// <summary>
        /// 当前会话是否由随身MP3 发起。/mic_music 等其他入口接管引擎时，引擎的 TrackFinished 会被清空。
        /// </summary>
        /// <returns>会话属于随身MP3 时为 true。</returns>
        private static bool OwnsSession()
            => MicBroadcast.TrackFinished != null && MicBroadcast.Snapshot().Phase != "idle";

        /// <summary>
        /// 按路径在指定歌单中查下标（不做会话守卫，只认路径）。
        /// </summary>
        /// <param name="list">歌单；为 null 时返回 -1。</param>
        /// <param name="path">曲目路径。</param>
        /// <returns>歌单中的下标；不在歌单时为 -1。</returns>
        private static int IndexOfPath(Mp3Playlist.ListEntry list, string path)
        {
            if (list == null || string.IsNullOrEmpty(path))
                return -1;
            return list.Items.FindIndex(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 把歌单层的错误转换为命令信封：错误码小写英文短词，中文详情放在 data.message。
        /// </summary>
        /// <param name="message">歌单层返回的中文原因。</param>
        /// <returns>失败的命令结果。</returns>
        private static CommandResult ListError(string message)
        {
            string code;
            switch (message)
            {
                case "歌单不存在":
                    code = "list not found";
                    break;
                case "歌单序号越界":
                    code = "index out of range";
                    break;
                case "至少保留一个歌单":
                    code = "last list";
                    break;
                default:
                    code = "invalid name";
                    break;
            }
            return CommandResult.Fail(code, new { message });
        }

        /// <summary>
        /// 统一执行壳：RunOnMain 包裹（结果信封原样回写）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        /// <param name="work">要在主线程执行的命令逻辑。</param>
        private static void WriteMain(HttpListenerContext ctx, Func<CommandResult> work)
            => HttpServer.WriteJson(ctx.Response, WebConsole.RunOnMain(work));

        private static int ReadInt(JObject body, string key)
        {
            var token = body[key];
            if (token == null) return 0;
            try { return token.Value<int>(); }
            catch { return 0; }
        }

        private static float ReadFloat(JObject body, string key)
        {
            var token = body[key];
            if (token == null) return 0f;
            try { return token.Value<float>(); }
            catch { return 0f; }
        }

        /// <summary>
        /// 解析 JSON 请求体；缺失/非法直接回 "invalid body"（统一解析在 ApiUtil）。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        /// <param name="body">解析出的请求体；失败时为 null。</param>
        /// <returns>解析成功时为 true；失败时为 false。</returns>
        private static bool ParseBody(HttpListenerContext ctx, out JObject body)
        {
            body = ApiUtil.ParseBody(ctx);
            if (body != null) return true;
            HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid body"));
            return false;
        }
    }
}
