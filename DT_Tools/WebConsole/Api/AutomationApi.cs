using System;
using System.Linq;
using System.Net;
using DT_Tools.Automation;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// /api/automation/* — 总开关、模块列表、模块日志。
    /// 模块元数据来自 Engine.AutomationModules（id 即配置段名）；
    /// 开关统一写 BepInEx 配置项（引擎的 [Config] 绑定会实时回写字段）。
    /// </summary>
    public static class AutomationApi
    {
        public static void HandleStatus(HttpListenerContext ctx)
        {
            var sections = ConfigService.List(Engine.Config).ToDictionary(s => s.Section, s => s);
            HttpServer.WriteJson(ctx.Response, new
            {
                hostEnabled = AutomationHost.Enabled,
                modules = Engine.AutomationModules.Select(m => new
                {
                    id = m.Section,
                    section = m.Section,
                    displayName = m.DisplayName,
                    description = m.Description,
                    side = FormatSide(m.Side),
                    author = string.IsNullOrEmpty(m.Author) ? "佚名" : m.Author,
                    enabled = Engine.EnabledOf(m.Type),
                    entries = sections.TryGetValue(m.Section, out var sec)
                        ? sec.Entries.Select(EntryDto)
                        : Enumerable.Empty<object>(),
                }),
            });
        }

        /// <summary>POST /api/automation/host {enabled} — 自动化总开关。</summary>
        public static void HandleHost(HttpListenerContext ctx)
        {
            var body = ParseBody(ctx);
            if (body == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "invalid body" });
                return;
            }

            bool? enabled = ReadBool(body, "enabled");
            if (enabled == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "missing enabled" });
                return;
            }

            // 段名/键名零字符串：走 Engine 注册表推导（AutomationHost 为 sealed class 可作类型实参）
            var entry = Engine.Config[Engine.SectionOf<AutomationHost>(), nameof(AutomationHost.Enabled)];
            if (entry == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "not bound" });
                return;
            }
            entry.BoxedValue = enabled.Value;
            HttpServer.WriteJson(ctx.Response, new { ok = true, hostEnabled = enabled.Value });
        }

        /// <summary>/api/automation/modules/{id}/log — 模块日志（GET 读取；POST /log/clear 或 DELETE 清空）。</summary>
        public static void HandleModule(HttpListenerContext ctx)
        {
            const string prefix = "/api/automation/modules/";
            string rest = ctx.Request.Url.AbsolutePath.Substring(prefix.Length);
            int slash = rest.IndexOf('/');
            string id = slash >= 0 ? Uri.UnescapeDataString(rest.Substring(0, slash)) : Uri.UnescapeDataString(rest);
            string tail = slash >= 0 ? rest.Substring(slash) : "";

            var module = Engine.AutomationModules.FirstOrDefault(m => m.Section == id);
            if (module == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "module not found" });
                return;
            }

            if (tail == "/log" && ctx.Request.HttpMethod == "GET")
            {
                var (seq, entries) = Log.SnapshotEntries(id);
                HttpServer.WriteJson(ctx.Response, new
                {
                    ok = true,
                    id,
                    seq,
                    lines = entries.Select(e => $"{e.Time:HH:mm:ss} [{e.Level}] {e.Message}"),
                });
                return;
            }

            if ((tail == "/log/clear" && ctx.Request.HttpMethod == "POST") ||
                (tail == "/log" && ctx.Request.HttpMethod == "DELETE"))
            {
                Log.Clear(id);
                Log.Info(id, "日志已清空");
                HttpServer.WriteJson(ctx.Response, new { ok = true, id });
                return;
            }

            HttpServer.WriteJson(ctx.Response, new { ok = false, error = "unknown module endpoint" });
        }

        // ---- 内部 ----

        private static object EntryDto(ConfigService.EntryDto e) => new
        {
            key = e.Key,
            type = e.Type,
            value = e.Value,
            @default = e.Default,
            description = e.Description ?? "",
            accepts = e.Accepts,
        };

        private static string FormatSide(FeatureSide side) => side switch
        {
            FeatureSide.Client => "客户端",
            FeatureSide.Host => "服务端",
            FeatureSide.Both => "双方",
            _ => side.ToString(),
        };

        private static bool? ReadBool(Newtonsoft.Json.Linq.JObject body, string key)
        {
            var token = body[key];
            if (token == null || token.Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                return null;
            return (bool)token;
        }

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
    }
}
