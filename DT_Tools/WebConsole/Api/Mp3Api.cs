using System;
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
    /// 开关/本地静音），歌单持久化在 Mp3Playlist（BepInEx/config JSON）。
    /// 播放即入歌单（去重）；自然播完经 MicBroadcast.TrackFinished 自动连播下一首（循环）。
    /// 全部端点经 WebConsole.RunOnMain 在 Unity 主线程执行（引擎与歌单都只碰主线程），
    /// 响应沿用命令信封 {ok, error, data}；错误码小写英文短词，中文详情在 data.message。
    /// </summary>
    public static class Mp3Api
    {
        private const string Tag = "Mp3";

        // 当前播放曲目（原始路径，非 file:/// URI）；phase=idle 时视为无
        private static string _currentPath;

        // 暂停书签（暂停=销毁引擎会话，恢复=PlayTrack 重建+起始偏移）。
        // 书签只在引擎 idle 时有效——其他入口（/mic_music）接管会话即作废。
        private static string _pausedPath;
        private static float _pausedPosition;
        private static float _pausedDuration;

        // ── 查询 ──

        /// <summary>GET /api/mp3/state — 引擎会话快照 + 暂停书签合并 + 歌单 + 当前曲目下标。</summary>
        public static void HandleState(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                var store = Mp3Playlist.Get();
                var engine = MicBroadcast.Snapshot();
                int index;
                object data;
                if (_pausedPath != null && engine.Phase == "idle")
                {
                    // 暂停中（引擎会话已销毁，书签即真相）；引擎被其他入口占用时书签作废
                    index = IndexOfPath(_pausedPath);
                    data = new
                    {
                        Phase = "paused",
                        Position = _pausedPosition,
                        Duration = _pausedDuration,
                        engine.Volume,
                        engine.Mic,
                        engine.Local,
                        mode = store.Mode,
                        index,
                        playlist = store.Items.ToArray(),
                    };
                }
                else
                {
                    if (_pausedPath != null)
                        _pausedPath = null;   // 会话被接管/正在重建，书签过期
                    index = CurrentIndex();
                    data = new
                    {
                        engine.Phase,
                        engine.Position,
                        engine.Duration,
                        engine.Volume,
                        engine.Mic,
                        engine.Local,
                        mode = store.Mode,
                        index,
                        playlist = store.Items.ToArray(),
                    };
                }
                return CommandResult.Success(data);
            });

        // ── 播放控制 ──

        /// <summary>POST /api/mp3/play {path} — 播放（本地路径或 http(s)），自动入歌单去重。</summary>
        public static void HandlePlay(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string path = (body.Value<string>("path") ?? "").Trim();
            if (path.Length == 0)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing path"));
                return;
            }
            WriteMain(ctx, () =>
            {
                int index = Mp3Playlist.Ensure(path);
                return PlayInternal(path, index);
            });
        }

        /// <summary>POST /api/mp3/index {index} — 播放歌单第 index 首。</summary>
        public static void HandleIndex(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int index = ReadInt(body, "index");
            WriteMain(ctx, () =>
            {
                var items = Mp3Playlist.Get().Items;
                if (index < 0 || index >= items.Count)
                    return CommandResult.Fail("index out of range", new { message = "歌单序号越界" });
                return PlayInternal(items[index].Path, index);
            });
        }

        /// <summary>POST /api/mp3/next {delta} — 跳转 ±1（默认 +1），歌单循环。</summary>
        public static void HandleNext(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int delta = body["delta"] == null ? 1 : ReadInt(body, "delta");
            if (delta == 0) delta = 1;
            WriteMain(ctx, () =>
            {
                var items = Mp3Playlist.Get().Items;
                if (items.Count == 0)
                    return CommandResult.Fail("empty playlist", new { message = "歌单为空" });
                int index = CurrentIndex();
                int target = index < 0
                    ? (delta > 0 ? 0 : items.Count - 1)
                    : ((index + delta) % items.Count + items.Count) % items.Count;
                return PlayInternal(items[target].Path, target);
            });
        }

        /// <summary>
        /// POST /api/mp3/pause — 暂停：记住进度并销毁引擎会话（还原语音管线，
        /// 不留挂起状态）；恢复时从书签位置重建。幂等：已暂停返回成功。
        /// </summary>
        public static void HandlePause(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                if (_pausedPath != null)
                    return CommandResult.Success(new { message = "已暂停" });
                var engine = MicBroadcast.Snapshot();
                if (engine.Phase != "playing" || string.IsNullOrEmpty(_currentPath))
                    return CommandResult.Fail("not playing", new { message = "当前没有可暂停的会话" });
                _pausedPath = _currentPath;
                _pausedPosition = engine.Position;
                _pausedDuration = engine.Duration;
                MicBroadcast.Stop();
                Log.Info(Tag, $"已暂停：{System.IO.Path.GetFileName(_pausedPath)} @ {_pausedPosition:0.#}s（会话已销毁）");
                return CommandResult.Success(new { message = "已暂停" });
            });

        /// <summary>POST /api/mp3/resume — 恢复：从书签位置重建会话。幂等：已在播放返回成功。</summary>
        public static void HandleResume(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                if (_pausedPath != null)
                {
                    string path = _pausedPath;
                    float pos = _pausedPosition;
                    int index = IndexOfPath(path);
                    _pausedPath = null;
                    return PlayInternal(path, index, pos);
                }
                if (MicBroadcast.Snapshot().Phase != "idle")
                    return CommandResult.Success(new { message = "已在播放中" });
                return CommandResult.Fail("nothing to resume", new { message = "没有可恢复的暂停会话" });
            });

        /// <summary>POST /api/mp3/seek {position} — 跳转（秒）：播放中走引擎；暂停中更新书签。</summary>
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

        /// <summary>POST /api/mp3/volume {volume} — 响度 0~1（实时生效并持久化）。</summary>
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

        /// <summary>POST /api/mp3/mic {enabled} — 麦克风混入开关（会话中即时挂/卸；持久化）。</summary>
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
                // 无会话时只记偏好（下次播放生效）；有会话时即时挂/卸，失败不落盘
                if (MicBroadcast.Snapshot().Phase == "idle")
                {
                    Mp3Playlist.Get().Mic = enabled.Value;
                    Mp3Playlist.Save();
                    return CommandResult.Success(new { message = "已记录混入偏好（下次播放生效）", mic = enabled.Value });
                }
                if (!MicBroadcast.SetMicMix(enabled.Value))
                    return CommandResult.Fail("mic unavailable", new { message = "开启失败：麦克风采集未运行（设备不可用或语音未连接）" });
                Mp3Playlist.Get().Mic = enabled.Value;
                Mp3Playlist.Save();
                return CommandResult.Success(new
                {
                    message = enabled.Value ? "麦克风混入已开启" : "麦克风混入已关闭",
                    mic = enabled.Value,
                });
            });
        }

        /// <summary>POST /api/mp3/local {enabled} — 本地喇叭出声开关（mute 实现，持久化）。</summary>
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

        /// <summary>POST /api/mp3/mode {mode} — 播放方式：list=列表循环 | single=单曲循环 | random=随机播放。</summary>
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

        /// <summary>POST /api/mp3/stop — 停止会话并清除暂停书签。</summary>
        public static void HandleStop(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                MicBroadcast.Stop();
                _currentPath = null;
                _pausedPath = null;
                return CommandResult.Success(new { message = "已停止" });
            });

        // ── 歌单管理 ──

        /// <summary>POST /api/mp3/playlist/add {path} — 添加（按路径去重）。</summary>
        public static void HandlePlaylistAdd(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string path = (body.Value<string>("path") ?? "").Trim();
            if (path.Length == 0)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing path"));
                return;
            }
            WriteMain(ctx, () =>
            {
                int index = Mp3Playlist.Ensure(path);
                return CommandResult.Success(new { index, message = "已在歌单中" });
            });
        }

        /// <summary>POST /api/mp3/playlist/remove {index} — 移除（当前播放中的曲目也可移除，播放不受影响）。</summary>
        public static void HandlePlaylistRemove(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int index = ReadInt(body, "index");
            WriteMain(ctx, () =>
            {
                var items = Mp3Playlist.Get().Items;
                if (index < 0 || index >= items.Count)
                    return CommandResult.Fail("index out of range", new { message = "歌单序号越界" });
                items.RemoveAt(index);
                Mp3Playlist.Save();
                return CommandResult.Success(new { message = "已移除" });
            });
        }

        /// <summary>POST /api/mp3/playlist/move {from,to} — 调序。</summary>
        public static void HandlePlaylistMove(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            int from = ReadInt(body, "from");
            int to = ReadInt(body, "to");
            WriteMain(ctx, () =>
            {
                var items = Mp3Playlist.Get().Items;
                if (from < 0 || from >= items.Count || to < 0 || to >= items.Count)
                    return CommandResult.Fail("index out of range", new { message = "歌单序号越界" });
                var item = items[from];
                items.RemoveAt(from);
                items.Insert(to, item);
                Mp3Playlist.Save();
                return CommandResult.Success(new { message = "已调整顺序" });
            });
        }

        /// <summary>POST /api/mp3/playlist/clear — 清空歌单（不停止当前播放）。</summary>
        public static void HandlePlaylistClear(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                Mp3Playlist.Get().Items.Clear();
                Mp3Playlist.Save();
                return CommandResult.Success(new { message = "歌单已清空" });
            });

        // ---- 内部 ----

        /// <summary>
        /// 启动播放并挂连播回调；静音偏好立即生效（加载完成应用 mute，避免先响一下）。
        /// 任何新播放都使暂停书签作废；startSeconds&gt;0 时从该位置重建（恢复暂停）。
        /// </summary>
        private static CommandResult PlayInternal(string path, int index, float startSeconds = 0f)
        {
            var store = Mp3Playlist.Get();
            if (!MicBroadcast.PlayTrack(path, store.Volume, store.Mic, startSeconds))
                return CommandResult.Fail("invalid source", new { message = "播放失败：音频来源无效（文件不存在/路径非法，原因见 MicBroadcast 日志）" });
            _currentPath = path;
            _pausedPath = null;
            MicBroadcast.TrackFinished = OnTrackFinished;
            MicBroadcast.SetLocalAudible(store.Local);
            return CommandResult.Success(new
            {
                message = $"正在播放（{(store.Mic ? "混入麦克风" : "仅本地")}）",
                index,
            });
        }

        /// <summary>
        /// 自然播完自动连播。注意：引擎回调发生在 Stop() 之后（phase 已 idle），
        /// 当前曲目必须直接按路径查表——不能用 CurrentIndex()（其含 idle=-1 守卫，
        /// 会让连播永远落回第一首）。播放方式：list=顺序循环、single=单曲循环（重播当前）、
        /// random=随机不重复。
        /// </summary>
        private static void OnTrackFinished()
        {
            var store = Mp3Playlist.Get();
            var items = store.Items;
            if (items.Count == 0)
                return;
            int index = IndexOfPath(_currentPath);
            int target;
            switch (store.Mode)
            {
                case "single":
                    target = index < 0 ? 0 : index;   // 单曲循环：重播当前
                    break;
                case "random":
                    target = PickRandom(items.Count, index);
                    break;
                default:   // list：顺序循环
                    target = index < 0 ? 0 : (index + 1) % items.Count;
                    break;
            }
            Log.Info(Tag, $"自动连播：{items[target].Label}");
            PlayInternal(items[target].Path, target);
        }

        /// <summary>按路径查歌单下标（无会话守卫——连播回调时引擎已 Stop，只认路径）。</summary>
        private static int IndexOfPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return -1;
            return Mp3Playlist.Get().Items.FindIndex(i =>
                string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>随机挑一首（避免与当前重复；单曲目时只能原地）。</summary>
        private static int PickRandom(int count, int exclude)
        {
            if (count <= 1)
                return 0;
            var rnd = new Random();
            int t;
            do { t = rnd.Next(count); } while (t == exclude);
            return t;
        }

        /// <summary>当前曲目在歌单中的下标（会话空闲或路径不在歌单时 -1）。</summary>
        private static int CurrentIndex()
        {
            if (MicBroadcast.Snapshot().Phase == "idle")
                return -1;
            return IndexOfPath(_currentPath);
        }

        /// <summary>统一执行壳：RunOnMain 包裹（结果信封原样回写）。</summary>
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

        /// <summary>解析 JSON 请求体；缺失/非法直接回 "invalid body"（统一解析在 ApiUtil）。</summary>
        private static bool ParseBody(HttpListenerContext ctx, out JObject body)
        {
            body = ApiUtil.ParseBody(ctx);
            if (body != null) return true;
            HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid body"));
            return false;
        }
    }
}
