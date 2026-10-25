using System;
using System.Globalization;
using System.Linq;
using System.Net;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Features.WebTablet;
using DT_Tools.Game.WebTablet;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Api
{
    public static class WebTabletApi
    {
        public static void HandleState(HttpListenerContext ctx)
        {
            long since = ParseLong(ctx.Request.QueryString["since"]);
            WebTabletSnapshot snap = WebTabletStore.Snapshot(since);
            if (snap.StateCode.Length == 0 && Engine.Enabled<WebTabletFeature>())
            {
                WebConsole.RunOnMain(() =>
                {
                    WebTabletBridge.SyncState();
                    return CommandResult.Success();
                });
                snap = WebTabletStore.Snapshot(since);
            }

            HttpServer.WriteJson(ctx.Response, CommandResult.Success(new
            {
                enabled = Engine.Enabled<WebTabletFeature>(),
                epoch = snap.Epoch,
                seq = snap.Seq,
                state = new { code = snap.StateCode, label = snap.StateLabel },
                photoCooldownMs = WebTabletStore.PhotoCooldownMs(),
                canType = WebTabletText.CanTypeInState(snap.StateCode),
                typeReason = WebTabletText.TypeBlockReason(snap.StateCode, snap.StateLabel),
                lines = snap.Lines.Select(ToLineDto).ToList(),
                clues = snap.Clues.Select(ToClueDto).ToList(),
            }));
        }

        public static void HandleSend(HttpListenerContext ctx)
        {
            JObject body = ApiUtil.ParseBody(ctx);
            if (body == null)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid body"));
                return;
            }

            string text = (body.Value<string>("text") ?? "").Trim();
            if (text.Length == 0)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("empty", new { message = "消息为空" }));
                return;
            }

            WriteMain(ctx, () =>
            {
                string error = WebTabletBridge.SendChat(text);
                return error == null
                    ? CommandResult.Success(new { message = "已发送" })
                    : CommandResult.Fail("rejected", new { message = error });
            });
        }

        public static void HandlePhotoUpload(HttpListenerContext ctx)
        {
            if (!Engine.Enabled<WebTabletFeature>())
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("disabled", new { message = "平板电脑未启用" }));
                return;
            }

            JObject body = ApiUtil.ParseBody(ctx);
            if (body == null)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid body"));
                return;
            }

            byte[] jpeg;
            try
            {
                jpeg = Convert.FromBase64String(body.Value<string>("data") ?? "");
            }
            catch (FormatException)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid data", new { message = "图片数据无效" }));
                return;
            }

            if (jpeg.Length == 0 || jpeg.Length > WebTabletText.MaxPhotoBytes)
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("too large",
                    new { message = $"图片须在 {WebTabletText.MaxPhotoBytes / 1024}KB 以内" }));
                return;
            }
            if (!WebTabletText.IsJpeg(jpeg))
            {
                HttpServer.WriteJson(ctx.Response, CommandResult.Fail("not jpeg", new { message = "仅支持 JPEG 图片" }));
                return;
            }

            WriteMain(ctx, () =>
            {
                string error = WebTabletBridge.SendPhoto(jpeg);
                return error == null
                    ? CommandResult.Success(new { message = "照片已发送" })
                    : CommandResult.Fail("rejected", new { message = error });
            });
        }

        public static void HandlePhotoGet(HttpListenerContext ctx)
        {
            if (!int.TryParse(ctx.Request.QueryString["key"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key))
            {
                HttpServer.WriteText(ctx.Response, 400, "bad key");
                return;
            }

            byte[] data = WebTabletStore.GetPhoto(key);
            if (data == null)
            {
                HttpServer.WriteText(ctx.Response, 404, "not found");
                return;
            }

            HttpListenerResponse resp = ctx.Response;
            resp.StatusCode = 200;
            resp.ContentType = "image/jpeg";
            resp.Headers["Cache-Control"] = "no-cache";
            resp.ContentLength64 = data.Length;
            resp.OutputStream.Write(data, 0, data.Length);
            resp.OutputStream.Close();
        }

        public static void HandleClear(HttpListenerContext ctx)
        {
            JObject body = ApiUtil.ParseBody(ctx);
            string scope = body?.Value<string>("scope") ?? "";
            string message;
            switch (scope)
            {
                case "chat":
                    WebTabletStore.ClearChat();
                    message = "已清理聊天记录";
                    break;
                case "clues":
                    WebTabletStore.ClearClues();
                    message = "已清理线索";
                    break;
                case "all":
                    WebTabletStore.Clear();
                    message = "已清理全部";
                    break;
                default:
                    HttpServer.WriteJson(ctx.Response, CommandResult.Fail("invalid scope", new { message = "清理范围无效" }));
                    return;
            }
            HttpServer.WriteJson(ctx.Response, CommandResult.Success(new { message, scope }));
        }

        private static object ToLineDto(WebTabletLine l) => new
        {
            seq = l.Seq,
            kind = l.Kind,
            time = l.Time,
            playerId = l.PlayerId,
            name = l.Name,
            avatar = l.Avatar,
            isMine = l.IsMine,
            isDead = l.IsDead,
            text = l.Text,
            photoKey = l.PhotoKey,
            round = l.Round,
        };

        private static object ToClueDto(WebTabletClue c) => new
        {
            key = c.Key,
            isProposition = c.IsProposition,
            isMine = c.IsMine,
            ownerId = c.OwnerId,
            ownerName = c.OwnerName,
            ownerAvatar = c.OwnerAvatar,
            who = c.Who,
            whoAvatar = c.WhoAvatar,
            where = c.Where,
            when = c.When,
            sentence = c.Sentence,
            destroyed = c.Destroyed,
            time = c.Time,
            round = c.Round,
            targetId = c.TargetId,
            roomId = c.RoomId,
        };

        private static void WriteMain(HttpListenerContext ctx, Func<CommandResult> work)
            => HttpServer.WriteJson(ctx.Response, WebConsole.RunOnMain(work));

        private static long ParseLong(string raw)
            => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
    }
}
