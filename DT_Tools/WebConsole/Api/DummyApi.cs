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
    public static class DummyApi
    {
        public static void HandleState(HttpListenerContext ctx)
            => WriteMain(ctx, () => CommandResult.Success(FakePlayers.Snapshot(ResolveRoomCapacity())));

        public static void HandleCharacters(HttpListenerContext ctx)
            => WriteMain(ctx, () => CommandResult.Success(new
            {
                characters = AutoPickCharacterLogic.ListAll(includeRandom: true)
                    .Select(c => new { id = c.DataId, label = c.DisplayName })
                    .ToArray(),
            }));

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

        public static void HandleRemoveAll(HttpListenerContext ctx)
            => WriteMain(ctx, () =>
            {
                int removed = FakePlayers.RemoveAll();
                string text = removed > 0 ? $"已移出 {removed} 个假人。" : "房间里没有假人。";
                return Op(true, null, text, new { removed });
            });

        private static int ResolveRoomCapacity()
        {
            try
            {
                if (Engine.Enabled<DT_Tools.Features.LobbyMaxPlayers.LobbyMaxPlayersFeature>())
                    return DT_Tools.Features.LobbyMaxPlayers.LobbyMaxPlayersFeature.MaxMembers;
            }
            catch (Exception ex)
            {
                Log.Warn("FakePlayer", $"解析房间人数上限失败，回退原生上限：{ex.Message}");
            }
            return Define.MAX_PLAYER_COUNT;
        }

        private static void WriteMain(HttpListenerContext ctx, Func<CommandResult> work)
            => HttpServer.WriteJson(ctx.Response, WebConsole.RunOnMain(work));

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

        private static bool ParseBody(HttpListenerContext ctx, out JObject body)
        {
            body = ApiUtil.ParseBody(ctx);
            if (body != null) return true;
            HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid body"));
            return false;
        }
    }
}
