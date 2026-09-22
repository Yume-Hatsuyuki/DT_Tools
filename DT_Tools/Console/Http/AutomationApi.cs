using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using DT_Tools.Automation;
using DT_Tools.Core;

namespace DT_Tools.Console.Http
{
    /// <summary>
    /// /api/automation/* — 总开关、模块列表、模块日志。
    /// 参数读写复用 ConfigService（与 DT CONFIG 同一数据源）。
    /// </summary>
    internal static class AutomationApi
    {
        public static string StatusJson(ConfigFile config)
        {
            var bySection = IndexSections(config);
            var sb = new StringBuilder(1024);
            sb.Append('{');
            sb.Append("\"hostEnabled\":").Append(JsonBool(AutomationHost.IsHostEnabled)).Append(',');
            sb.Append("\"modules\":[");
            bool first = true;
            foreach (var m in AutomationRegistry.All)
            {
                if (!first) sb.Append(',');
                first = false;
                AppendModuleObject(sb, m, bySection, includeLog: false);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static string GetModuleLog(string id)
        {
            var m = AutomationRegistry.Find(id);
            if (m == null)
                return "{\"ok\":false,\"error\":\"module not found\"}";

            var lines = m.Log.Snapshot();
            var sb = new StringBuilder(256 + lines.Length * 64);
            sb.Append("{\"ok\":true,\"id\":").Append(J(m.Id));
            sb.Append(",\"seq\":").Append(m.Log.Seq.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"lines\":[");
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(J(lines[i]));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static string ClearModuleLog(string id)
        {
            var m = AutomationRegistry.Find(id);
            if (m == null)
                return "{\"ok\":false,\"error\":\"module not found\"}";
            m.Log.Clear();
            m.Log.Info("日志已清空");
            return "{\"ok\":true,\"id\":" + J(m.Id) + "}";
        }

        public static string SetHostEnabled(string body)
        {
            if (!TryGetBool(body, "enabled", out bool enabled))
                return "{\"ok\":false,\"error\":\"missing enabled\"}";
            if (AutomationHost.CfgEnabled == null)
                return "{\"ok\":false,\"error\":\"not bound\"}";
            AutomationHost.CfgEnabled.Value = enabled;
            return "{\"ok\":true,\"hostEnabled\":" + JsonBool(enabled) + "}";
        }

        private static Dictionary<string, ConfigService.SectionDto> IndexSections(ConfigFile config)
        {
            var map = new Dictionary<string, ConfigService.SectionDto>(StringComparer.Ordinal);
            foreach (var sec in ConfigService.List(config))
                map[sec.Section] = sec;
            return map;
        }

        private static void AppendModuleObject(
            StringBuilder sb,
            IAutomationModule m,
            Dictionary<string, ConfigService.SectionDto> bySection,
            bool includeLog)
        {
            sb.Append('{');
            sb.Append("\"id\":").Append(J(m.Id)).Append(',');
            sb.Append("\"section\":").Append(J(m.Section)).Append(',');
            sb.Append("\"displayName\":").Append(J(m.DisplayName)).Append(',');
            sb.Append("\"description\":").Append(J(m.Description)).Append(',');
            sb.Append("\"side\":").Append(J(FormatSide(m.Side))).Append(',');
            sb.Append("\"author\":").Append(J(m.Author ?? "")).Append(',');
            sb.Append("\"enabled\":").Append(JsonBool(m.ModuleEnabled)).Append(',');
            sb.Append("\"entries\":[");

            if (bySection.TryGetValue(m.Section, out var sec) && sec.Entries != null)
            {
                bool firstE = true;
                foreach (var e in sec.Entries)
                {
                    if (!firstE) sb.Append(',');
                    firstE = false;
                    sb.Append('{');
                    sb.Append("\"key\":").Append(J(e.Key)).Append(',');
                    sb.Append("\"type\":").Append(J(e.Type)).Append(',');
                    sb.Append("\"value\":").Append(JV(e.Value)).Append(',');
                    sb.Append("\"default\":").Append(JV(e.Default)).Append(',');
                    sb.Append("\"description\":").Append(J(e.Description ?? "")).Append(',');
                    sb.Append("\"accepts\":").Append(JV(e.Accepts));
                    sb.Append('}');
                }
            }

            sb.Append(']');

            if (includeLog)
            {
                var lines = m.Log.Snapshot();
                sb.Append(",\"seq\":").Append(m.Log.Seq.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"lines\":[");
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(J(lines[i]));
                }
                sb.Append(']');
            }
            sb.Append('}');
        }

        private static string FormatSide(FeatureSide side)
        {
            switch (side)
            {
                case FeatureSide.Client: return "客户端";
                case FeatureSide.Host: return "服务端";
                case FeatureSide.Both: return "双方";
                default: return side.ToString();
            }
        }

        // 序列化统一委托给 JsonWriter（全项目唯一实现）。
        private static string JsonBool(bool v) => JsonWriter.Bool(v);
        private static string J(string s) => JsonWriter.Str(s);
        private static string JV(object v) => JsonWriter.Value(v);

        private static bool TryGetBool(string body, string key, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(body)) return false;
            string needle = "\"" + key + "\"";
            int i = body.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            int colon = body.IndexOf(':', i + needle.Length);
            if (colon < 0) return false;
            int j = colon + 1;
            while (j < body.Length && char.IsWhiteSpace(body[j])) j++;
            if (j >= body.Length) return false;
            if (string.Compare(body, j, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = true;
                return true;
            }
            if (string.Compare(body, j, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = false;
                return true;
            }
            return false;
        }
    }
}
