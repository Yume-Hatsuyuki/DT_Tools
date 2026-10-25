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
    public static class Mp3Api
    {
        private const string Tag = "Mp3";

        private static readonly Random _rnd = new Random();

        private static string _currentPath;
        private static long _resolveToken;
        private static CancellationTokenSource _resolveCts;
        private static int _resolveFailStreak;
        private static string _playingListId;

        private static string _pausedPath;
        private static string _pausedListId;
        private static float _pausedPosition;
        private static float _pausedDuration;

        public static void HandleState(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                var store = Mp3Playlist.Get();
                var active = Mp3Playlist.Active();
                var engine = MicBroadcast.Snapshot();
                bool paused = _pausedPath != null && engine.Phase == "idle";
                if (!paused && _pausedPath != null)
                    _pausedPath = null;

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

        public static void HandlePlaylistAdd(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string kind = (body.Value<string>("kind") ?? string.Empty).Trim().ToLowerInvariant();
            string input = (body.Value<string>("path") ?? body.Value<string>("input") ?? string.Empty).Trim();
            string name = (body.Value<string>("name") ?? string.Empty).Trim();
            string listId = body.Value<string>("listId");
            if (kind.Length == 0)
                kind = Mp3Playlist.InferKind(input);

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

        public static void HandleNeteaseSettings(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                var s = Mp3Playlist.Get();
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

        public static void HandlePlaylistUpdate(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string listId = body.Value<string>("listId");
            int index = body.Value<int?>("index") ?? -1;
            bool hasName = body["name"] != null;
            bool hasAlias = body["alias"] != null;
            bool hasPath = body["path"] != null || body["input"] != null;
            bool hasLevel = body["level"] != null;
            string name = hasName ? (body.Value<string>("name") ?? "") : null;
            string alias = hasAlias ? (body.Value<string>("alias") ?? "") : null;
            string level = hasLevel ? (body.Value<string>("level") ?? "") : null;
            if (hasPath)
            {
                string input = (body.Value<string>("path") ?? body.Value<string>("input") ?? "").Trim();
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
                            pathToSet = input;
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

        private static CommandResult StartNeteaseTrack(string listId, string path, int index, float startSeconds)
        {
            var store = Mp3Playlist.Get();
            string songId = path.Substring("netease:".Length);
            string ipMode = store.NeteaseIpMode;
            string fallback = store.NeteaseFallbackIp;
            var list = Mp3Playlist.Find(listId);
            var item = (list != null && index >= 0 && index < list.Items.Count) ? list.Items[index] : null;
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
                yield break;
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

        private static void CancelPendingResolve()
        {
            _resolveToken++;
            _resolveCts?.Cancel();
            _resolveCts = null;
        }

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

        public static void HandlePlaylistClear(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                string listId = ApiUtil.ParseBody(ctx)?.Value<string>("listId");
                if (!Mp3Playlist.Clear(listId, out string error))
                    return ListError(error);
                return CommandResult.Success(new { message = "歌单已清空" });
            });

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

        private static bool OwnsSession()
            => MicBroadcast.TrackFinished != null && MicBroadcast.Snapshot().Phase != "idle";

        private static int IndexOfPath(Mp3Playlist.ListEntry list, string path)
        {
            if (list == null || string.IsNullOrEmpty(path))
                return -1;
            return list.Items.FindIndex(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        }

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

        private static bool ParseBody(HttpListenerContext ctx, out JObject body)
        {
            body = ApiUtil.ParseBody(ctx);
            if (body != null) return true;
            HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid body"));
            return false;
        }
    }
}
