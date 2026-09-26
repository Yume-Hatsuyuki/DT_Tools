using System;
using System.Linq;
using System.Net;
using System.Text;
using DT_Tools.Core;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// /api/config/* — 配置列表/更新/保存/重置/导入/导出 + 功能段日志。
    /// 数据源统一走 ConfigService（与 WebUI 同一协议）；API 层不认识任何具体功能。
    /// </summary>
    public static class ConfigApi
    {
        public static void HandleList(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, ConfigService.List(Engine.Config));

        public static void HandleUpdate(HttpListenerContext ctx)
        {
            var body = ParseBody(ctx);
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
            string raw = NormalizeRaw(body["value"]);
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
            var body = ParseBody(ctx);
            if (body == null)
            {
                // 非法 JSON 不能静默当成空体——空 section/key 会触发全量重置
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "invalid body" });
                return;
            }
            int n = ConfigService.Reset(
                Engine.Config,
                (string)body["section"] ?? "",
                (string)body["key"] ?? "");
            HttpServer.WriteJson(ctx.Response, new { ok = true, reset = n });
        }

        public static void HandleImport(HttpListenerContext ctx)
        {
            var body = ParseBody(ctx);
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

        /// <summary>/api/config/section/{段名}/log — 功能段日志（GET 读取；POST /log/clear 或 DELETE 清空）。</summary>
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

        // ---- 内部 ----

        /// <summary>解析 JSON 请求体；空体或非法 JSON 一律返回 null（由调用方回 "invalid body"）。</summary>
        private static Newtonsoft.Json.Linq.JObject ParseBody(HttpListenerContext ctx)
        {
            string body = HttpServer.ReadBody(ctx.Request);
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                return Newtonsoft.Json.Linq.JObject.Parse(body);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return null;
            }
        }

        /// <summary>JToken 值 → 配置更新用的原始字符串（字符串去引号，其余字面量转文本）。</summary>
        private static string NormalizeRaw(Newtonsoft.Json.Linq.JToken token)
        {
            switch (token?.Type)
            {
                case null:
                case Newtonsoft.Json.Linq.JTokenType.Null:
                    return null;
                case Newtonsoft.Json.Linq.JTokenType.String:
                    return (string)token;
                case Newtonsoft.Json.Linq.JTokenType.Boolean:
                    return (bool)token ? "true" : "false";
                default:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
            }
        }
    }
}
