using System;
using System.Linq;
using System.Net;
using DT_Tools.Automation;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.WebConsole.Api
{
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
                    enabledKey = Engine.EnabledKey,
                    entries = sections.TryGetValue(m.Section, out var sec)
                        ? sec.Entries.Select(EntryDto)
                        : Enumerable.Empty<object>(),
                }),
            });
        }

        public static void HandleHost(HttpListenerContext ctx)
        {
            var body = ApiUtil.ParseBody(ctx);
            if (body == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "invalid body" });
                return;
            }

            bool? enabled = ApiUtil.ReadBool(body, "enabled");
            if (enabled == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "missing enabled" });
                return;
            }

            var entry = Engine.Config[Engine.SectionOf<AutomationHost>(), nameof(AutomationHost.Enabled)];
            if (entry == null)
            {
                HttpServer.WriteJson(ctx.Response, new { ok = false, error = "not bound" });
                return;
            }
            entry.BoxedValue = enabled.Value;
            HttpServer.WriteJson(ctx.Response, new { ok = true, hostEnabled = enabled.Value });
        }

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

    }
}
