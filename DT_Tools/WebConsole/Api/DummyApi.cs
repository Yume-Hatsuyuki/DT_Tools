using System;
using System.Linq;
using System.Net;
using DT_Tools.Automation.AutoPickCharacter;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Game;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// /api/dummy/* — 假人管理应用的后端（Game/FakePlayers 的 HTTP 面）。
    /// GameRoom 及其玩家只能在 Unity 主线程读写：所有端点经 WebConsole.RunOnMain
    /// 同步执行（复用 /api/run 的队列与 5 秒超时保护）。
    /// 响应沿用命令信封 {ok, error, data}；错误码小写英文短词，中文详情在 data.message。
    /// </summary>
    public static class DummyApi
    {
        /// <summary>GET /api/dummy/state — 房间快照：阶段/房主/容量/玩家与假人标记。</summary>
        public static void HandleState(HttpListenerContext ctx)
            => WriteMain(ctx, () => CommandResult.Success(FakePlayers.Snapshot(ResolveRoomCapacity())));

        /// <summary>GET /api/dummy/characters — 角色目录（含随机项），下拉数据源。</summary>
        public static void HandleCharacters(HttpListenerContext ctx)
            => WriteMain(ctx, () => CommandResult.Success(new
            {
                characters = AutoPickCharacterLogic.ListAll(includeRandom: true)
                    .Select(c => new { id = c.DataId, label = c.DisplayName })
                    .ToArray(),
            }));

        /// <summary>POST /api/dummy/create {name, characterId} — 创建假人并进场。</summary>
        public static void HandleCreate(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string name = body.Value<string>("name");
            int characterId = ReadInt(body, "characterId");
            WriteMain(ctx, () =>
            {
                bool ok = FakePlayers.TryCreate(name, characterId, ResolveRoomCapacity(), out _, out string error, out string text);
                return Op(ok, error, text, new { name });
            });
        }

        /// <summary>POST /api/dummy/remove {name} — 移除假人（昵称或 #座位ID）。</summary>
        public static void HandleRemove(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string name = body.Value<string>("name");
            WriteMain(ctx, () =>
            {
                bool ok = FakePlayers.TryRemove(name, out string error, out string text);
                return Op(ok, error, text, new { name });
            });
        }

        /// <summary>POST /api/dummy/ready {name, ready} — 假人就绪/取消就绪。</summary>
        public static void HandleReady(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string name = body.Value<string>("name");
            bool? ready = ApiUtil.ReadBool(body, "ready");
            if (ready == null)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("missing ready"));
                return;
            }
            WriteMain(ctx, () =>
            {
                bool ok = FakePlayers.TrySetReady(name, ready.Value, out string error, out string text);
                return Op(ok, error, text, new { name, ready = ready.Value });
            });
        }

        /// <summary>POST /api/dummy/pick {name, characterId} — 为假人选角（选角阶段/大厅换角通吃）。</summary>
        public static void HandlePick(HttpListenerContext ctx)
        {
            if (!ParseBody(ctx, out var body)) return;
            string name = body.Value<string>("name");
            int characterId = ReadInt(body, "characterId");
            WriteMain(ctx, () =>
            {
                bool ok = FakePlayers.TryPick(name, characterId, out string error, out string text);
                return Op(ok, error, text, new { name, characterId });
            });
        }

        /// <summary>POST /api/dummy/remove-all — 移除全部假人（不动 WebUI 行配置）。</summary>
        public static void HandleRemoveAll(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                int removed = FakePlayers.RemoveAll();
                string text = removed > 0 ? $"已移出 {removed} 个假人。" : "房间里没有假人。";
                return Op(true, null, text, new { removed });
            });

        // ---- 内部 ----

        /// <summary>
        /// 有效房间人数上限：「房间人数上限」功能（LobbyMaxPlayers）开启时用其 MaxMembers
        /// （1–16，其 GameStart 补丁已做出生点循环复用，>8 不越界）；关闭时回原生 8
        /// （0.1.16b Define.cs:752）。Game 层禁止反向引用 Patches（AGENTS.md §4），
        /// 故在 API 顶层解析后作为参数传给 FakePlayers；座位表 16 为硬顶。
        /// </summary>
        private static int ResolveRoomCapacity()
        {
            try
            {
                if (Engine.Enabled<DT_Tools.Patches.System.LobbyMaxPlayers.LobbyMaxPlayersFeature>())
                    return DT_Tools.Patches.System.LobbyMaxPlayers.LobbyMaxPlayersFeature.MaxMembers;
            }
            catch (Exception ex)
            {
                Log.Warn("FakePlayer", $"解析房间人数上限失败，回退原生上限：{ex.Message}");
            }
            return Define.MAX_PLAYER_COUNT;
        }

        /// <summary>统一执行壳：RunOnMain 包裹（结果信封原样回写）。</summary>
        private static void WriteMain(HttpListenerContext ctx, Func<CommandResult> work)
            => HttpServer.WriteJson(ctx.Response, WebConsole.RunOnMain(work));

        /// <summary>成功/失败 → 命令信封；中文详情进 data.message 并写一条全局日志（日志应用可见）。</summary>
        private static CommandResult Op(bool ok, string error, string text, object data)
        {
            if (!string.IsNullOrEmpty(text))
                Log.Info("FakePlayer", text);
            return ok
                ? CommandResult.Success(new { message = text })
                : CommandResult.Fail(error, new { message = text ?? error });
        }

        private static int ReadInt(JObject body, string key)
        {
            var token = body[key];
            if (token == null) return 0;
            try { return token.Value<int>(); }
            catch { return 0; }
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
