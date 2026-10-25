using System;
using System.Linq;
using System.Net;
using System.Text;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.WebConsole.Api
{
    public static class ConfigApi
    {
        public static void HandleList(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, ConfigService.List(Engine.Config));

        public static void HandleTags(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, FeatureTagCatalog.Entries.Select(e => new
            {
                key = e.Key,
                group = e.Group,
                groupKey = e.GroupKey,
                label = e.Label,
                description = e.Description,
            }).ToArray());

        public static void HandleUpdate(HttpListenerContext ctx)
        {
            var body = ApiUtil.ParseBody(ctx);
            if (body == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "invalid body" });
                return;
            }
            if (string.IsNullOrEmpty((string)body["section"]) ||
                string.IsNullOrEmpty((string)body["key"]))
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "missing section or key" });
                return;
            }

            string section = (string)body["section"];
            string key = (string)body["key"];
            string raw = ApiUtil.NormalizeRaw(body["value"]);
            if (raw == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "missing value" });
                return;
            }

            var (ok, error, value) = ConfigService.Update(Engine.Config, section, key, raw);
            if (!ok)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error });
                return;
            }

            HttpServer.WriteJson(ctx.Response, new { ok = true, section, key, value });
        }

        public static void HandleSave(HttpListenerContext ctx)
        {
            ConfigService.Save(Engine.Config);
            HttpServer.WriteJson(ctx.Response, new { ok = true });
        }

        public static void HandleReset(HttpListenerContext ctx)
        {
            var body = ApiUtil.ParseBody(ctx);
            if (body == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "invalid body" });
                return;
            }
            string section = (string)body["section"] ?? "";
            string key = (string)body["key"] ?? "";
            if (section == "" && key != "")
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "reset with key requires section" });
                return;
            }
            int n = ConfigService.Reset(Engine.Config, section, key);
            HttpServer.WriteJson(ctx.Response, new { ok = true, reset = n });
        }

        public static void HandleImport(HttpListenerContext ctx)
        {
            var body = ApiUtil.ParseBody(ctx);
            if (body == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "invalid body" });
                return;
            }
            string format = ((string)body["format"] ?? "json").ToLowerInvariant();
            string mode = ((string)body["mode"] ?? "memory").ToLowerInvariant();
            string content = (string)body["content"];
            if (string.IsNullOrEmpty(content))
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "missing content" });
                return;
            }

            ConfigService.ImportResult result = format == "cfg"
                ? ConfigService.ImportCfg(Engine.Config, content, mode)
                : ConfigService.ImportJson(Engine.Config, content, mode);

            HttpServer.WriteJson(ctx.Response, new
            {
                ok = true,
                mode = result.Mode,
                updated = result.Updated,
                skipped = result.Skipped,
                errors = result.Errors.Select(e => new { section = e.Section, key = e.Key, error = e.Error }),
            });
        }

        public static void HandleExport(HttpListenerContext ctx)
        {
            string cfg = ConfigService.ExportCfg(Engine.Config);
            var resp = ctx.Response;
            var bytes = Encoding.UTF8.GetBytes(cfg);
            resp.StatusCode = 200;
            resp.ContentType = "text/plain; charset=utf-8";
            resp.AddHeader("Content-Disposition", "attachment; filename=\"DT_Tools.cfg\"");
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Close();
        }

        public static void HandleSectionLog(HttpListenerContext ctx)
        {
            const string prefix = "/api/config/section/";
            string rest = ctx.Request.Url.AbsolutePath.Substring(prefix.Length);
            int slash = rest.IndexOf('/');
            string section = slash >= 0 ? Uri.UnescapeDataString(rest.Substring(0, slash)) : Uri.UnescapeDataString(rest);
            string tail = slash >= 0 ? rest.Substring(slash) : "";

            if (tail == "/log" && ctx.Request.HttpMethod == "GET")
            {
                var (seq, entries) = Log.SnapshotEntries(section);
                HttpServer.WriteJson(ctx.Response, new
                {
                    ok = true,
                    section,
                    seq,
                    lines = entries.Select(e => $"{e.Time:HH:mm:ss} [{e.Level}] {e.Message}"),
                });
                return;
            }

            if ((tail == "/log/clear" && ctx.Request.HttpMethod == "POST") ||
                (tail == "/log" && ctx.Request.HttpMethod == "DELETE"))
            {
                Log.Clear(section);
                Log.Info(section, "日志已清空");
                HttpServer.WriteJson(ctx.Response, new { ok = true });
                return;
            }

            HttpServer.WriteJson(ctx.Response, new { ok = false, error = "unknown section endpoint" });
        }

    }
}
