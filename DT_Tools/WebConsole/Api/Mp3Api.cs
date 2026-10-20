using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Game;
using Newtonsoft.Json.Linq;

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
        private static string _playingListId;

        // 暂停书签（暂停=销毁引擎会话，恢复=PlayTrack 重建+起始偏移）。
        // 书签只在引擎 idle 时有效——其他入口（/mic_music）接管会话即作废。
        private static string _pausedPath;
        private static string _pausedListId;
        private static float _pausedPosition;
        private static float _pausedDuration;

        // 网易云导入状态（协程在主线程跑，字段只碰主线程，无需加锁）。
        // phase: idle=空闲 | importing=导入中 | done=结束（error 非空=失败）。
        private static string _netPhase = "idle";
        private static string _netPlaylistName = "";
        private static int _netTotal;        // 曲目总数（单曲=1）
        private static int _netImported;     // 成功加入歌单的
        private static int _netSkipped;      // 重复/无效跳过的
        private static string _netMessage = "";

        /// <summary>
        /// POST /api/mp3/netease/import {url} — 一键导入网易云到当前歌单：单曲或歌单链接 →
        /// 拉取曲目（歌单用官方详情接口；直链不落盘）→ 加入歌单（netease://{id} 标记，不写盘）→
        /// 自动播放第一首（播放时经 nextmusic 实时解析直链）。异步执行（协程），立即返回
        /// {importing:true}，进度/结果经 /api/mp3/netease/status 轮询。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleNeteaseImport(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string url = (body.Value<string>("url") ?? "").Trim();
            if (url.Length == 0)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing url"));
                return;
            }
            WriteMain(ctx, () =>
            {
                if (_netPhase == "importing")
                    return CommandResult.Fail("importing", new { message = "已有导入正在进行，请稍候" });
                StartNeteaseImport(url);
                return CommandResult.Success(new
                {
                    importing = true,
                    message = "开始解析网易云链接…",
                });
            });
        }

        /// <summary>
        /// GET /api/mp3/netease/status — 网易云歌单导入进度/结果。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        public static void HandleNeteaseStatus(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                if (_netPhase == "idle")
                    return CommandResult.Success(new { phase = "idle" });
                return CommandResult.Success(new
                {
                    phase = _netPhase,
                    playlistName = _netPlaylistName,
                    total = _netTotal,
                    imported = _netImported,
                    skipped = _netSkipped,
                    message = _netMessage,
                });
            });

        /// <summary>
        /// 启动网易云导入协程（协程完成回调在主线程更新状态并播放）。
        /// </summary>
        /// <param name="urlOrId">用户输入的网易云链接（单曲/歌单）或 ID。</param>
        private static void StartNeteaseImport(string urlOrId)
        {
            _netPhase = "importing";
            _netPlaylistName = "";
            _netTotal = 0;
            _netImported = 0;
            _netSkipped = 0;
            _netMessage = "正在解析链接…";
            Log.Info(Tag, $"网易云导入开始: {urlOrId}");
            CoroutineHost.Start(CoNeteaseImport(urlOrId));
        }

        private static IEnumerator CoNeteaseImport(string urlOrId)
        {
            // 1) 解析链接：单曲（song）/ 歌单（playlist）+ ID
            NeteaseCloud.SongRef songRef = null;
            yield return NeteaseCloud.ResolveInput(urlOrId, r => songRef = r);
            if (songRef == null)
            {
                FinishNeteaseImport(false, "链接解析失败（支持 song?id=… 单曲、playlist?id=… 歌单、分享短链或纯数字 ID）");
                yield break;
            }

            // 2) 拉取曲目列表
            List<NeteaseCloud.NetTrack> tracks = null;
            if (songRef.Kind == "playlist")
            {
                _netMessage = "正在拉取歌单歌曲…";
                NeteaseCloud.PlaylistResult detail = null;
                yield return NeteaseCloud.FetchPlaylist(songRef.Id, r => detail = r);
                if (detail == null || detail.Tracks.Count == 0)
                {
                    FinishNeteaseImport(false, "歌单拉取失败或为空（可能歌单过大超过 1000 首）");
                    yield break;
                }
                _netPlaylistName = detail.Name;
                tracks = detail.Tracks;
            }
            else
            {
                _netMessage = "正在解析歌曲信息…";
                NeteaseCloud.NetTrack info = null;
                yield return NeteaseCloud.FetchSongInfo(long.Parse(songRef.Id), t => info = t);
                if (info == null || info.Name.Length == 0)
                {
                    FinishNeteaseImport(false, "歌曲信息解析失败");
                    yield break;
                }
                _netPlaylistName = info.Name;
                tracks = new List<NeteaseCloud.NetTrack> { info };
            }
            _netTotal = tracks.Count;

            // 3) 加入当前歌单：路径为 netease://{id} 标记（直链不落盘，播放时现场解析）
            var entries = new List<(string Path, string Label)>();
            foreach (var t in tracks)
            {
                if (t.Id == 0L)
                    continue;
                entries.Add(("netease://" + t.Id,
                    string.IsNullOrEmpty(t.Artist) ? t.Name : $"{t.Name} - {t.Artist}"));
            }
            _netSkipped = _netTotal - entries.Count;
            if (entries.Count == 0)
            {
                FinishNeteaseImport(false, "没有可导入的曲目");
                yield break;
            }
            string listId = Mp3Playlist.Active().Id;
            _netImported = Mp3Playlist.ImportRange(listId, entries, persist: false);
            Log.Info(Tag, $"网易云「{_netPlaylistName}」导入完成: 新增 {_netImported} 首，跳过 {_netSkipped} 首");

            if (_netImported == 0)
            {
                FinishNeteaseImport(false, "曲目已都在歌单中（或导入失败）");
                yield break;
            }

            // 4) 自动播放第一首（netease:// 标记在播放时解析直链）
            var list = Mp3Playlist.Find(listId);
            string first = entries[0].Path;
            int index = list == null ? 0 : list.Items.FindIndex(i =>
                string.Equals(i.Path, first, StringComparison.OrdinalIgnoreCase));
            PlayInternal(listId, first, index < 0 ? 0 : index);
            FinishNeteaseImport(true, _netSkipped > 0
                ? $"「{_netPlaylistName}」已加入 {_netImported} 首并开始播放（跳过 {_netSkipped} 首）"
                : $"「{_netPlaylistName}」已加入 {_netImported} 首并开始播放");
        }

        /// <summary>
        /// 导入结束收尾：更新状态字段与日志。
        /// </summary>
        /// <param name="success">是否成功。</param>
        /// <param name="message">结果消息。</param>
        private static void FinishNeteaseImport(bool success, string message)
        {
            _netPhase = "done";
            _netMessage = message;
            Log.Info(Tag, $"网易云歌单导入{(success ? "成功" : "失败")}: {message}");
        }

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
                MicBroadcast.Stop();
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
                MicBroadcast.Stop();
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
                return CommandResult.Success(new { index, message = "已在歌单中" });
            });
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
                    MicBroadcast.Stop();
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
            // 网易云曲目：路径是 netease://{id} 标记，直链不落盘，播放时经 nextmusic 实时解析
            if (path.StartsWith("netease://", StringComparison.OrdinalIgnoreCase))
                return PlayNetease(listId, path, index, startSeconds);

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
        /// 播放网易云曲目：netease://{id} → 实时解析直链（getSongUrl，约 1~2 秒）→ 播放。
        /// 直链带签名且约 25 分钟有效，不可落盘，所以解析与播放之间不留中间态；
        /// 解析失败只记日志（音源服务偶发不可达，不影响歌单本身）。
        /// </summary>
        /// <param name="listId">曲目所属歌单的 id。</param>
        /// <param name="path">netease://{id} 路径。</param>
        /// <param name="index">曲目在该歌单中的下标。</param>
        /// <param name="startSeconds">起播位置，单位秒。</param>
        /// <returns>命令执行结果。</returns>
        private static CommandResult PlayNetease(string listId, string path, int index, float startSeconds)
        {
            string idText = path.Substring("netease://".Length).Trim();
            if (!long.TryParse(idText, out long songId))
                return CommandResult.Fail("invalid source", new { message = "网易云曲目标记无效: " + path });
            Log.Info(Tag, $"网易云音源解析开始: {path}");
            CoroutineHost.Start(CoResolveAndPlay(listId, path, songId, index, startSeconds));
            return CommandResult.Success(new
            {
                message = "正在解析网易云音源（约 1~2 秒）…",
                index,
            });
        }

        private static IEnumerator CoResolveAndPlay(string listId, string path, long songId, int index, float startSeconds)
        {
            string url = null;
            yield return NeteaseCloud.FetchSongUrl(songId, u => url = u);
            if (string.IsNullOrEmpty(url))
            {
                Log.Warn(Tag, $"网易云音源解析失败: {path}（nextmusic 服务不可达或该曲无 standard 音质）");
                yield break;
            }
            var store = Mp3Playlist.Get();
            if (!MicBroadcast.PlayTrack(url, store.Volume, store.Mic, startSeconds))
            {
                Log.Warn(Tag, $"网易云直链播放失败: {path}");
                yield break;
            }
            _currentPath = path;          // 保留 netease:// 标记，歌单索引/连播按它查表
            _playingListId = listId;
            _pausedPath = null;
            _pausedListId = null;
            MicBroadcast.TrackFinished = OnTrackFinished;
            MicBroadcast.SetLocalAudible(store.Local);
            Log.Info(Tag, $"网易云音源解析成功并开始播放: {path}");
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
