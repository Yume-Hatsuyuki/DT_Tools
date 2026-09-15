using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using DT_Tools.Core;

namespace DT_Tools.Console.Http
{
    internal static class ConfigApi
    {
        public static string ListJson(ConfigFile config)
        {
            var sections = ConfigService.List(config);
            var sb = new StringBuilder();
            sb.Append('[');
            bool firstSec = true;
            foreach (var sec in sections)
            {
                if (!firstSec) sb.Append(',');
                firstSec = false;
                sb.Append("{\"section\":").Append(J(sec.Section)).Append(",\"entries\":[");
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
                    sb.Append("\"accepts\":").Append(AcceptsJson(e.Accepts));
                    sb.Append('}');
                }
                sb.Append("]}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        public static string HandleUpdate(ConfigFile config, string body)
        {
            if (!TryGetString(body, "section", out string section) ||
                !TryGetString(body, "key", out string key))
                return "{\"ok\":false,\"error\":\"missing section or key\"}";

            if (!TryGetRawValue(body, "value", out string raw))
                return "{\"ok\":false,\"error\":\"missing value\"}";

            var (ok, error, value) = ConfigService.Update(config, section, key, raw);
            if (!ok)
                return "{\"ok\":false,\"error\":" + J(error) + "}";
            return "{\"ok\":true,\"section\":" + J(section) + ",\"key\":" + J(key) +
                   ",\"value\":" + JV(value) + "}";
        }

        public static string HandleSave(ConfigFile config)
        {
            ConfigService.Save(config);
            return "{\"ok\":true}";
        }

        public static string HandleReset(ConfigFile config, string body)
        {
            TryGetString(body ?? "", "section", out string section);
            TryGetString(body ?? "", "key", out string key);
            int n = ConfigService.Reset(config, section, key);
            return "{\"ok\":true,\"reset\":" + n + "}";
        }

        public static string ExportCfg(ConfigFile config) => ConfigService.ExportCfg(config);

        public static string HandleImport(ConfigFile config, string body)
        {
            TryGetString(body ?? "", "format", out string format);
            TryGetString(body ?? "", "mode", out string mode);
            TryGetString(body ?? "", "content", out string content);
            format = (format ?? "json").ToLowerInvariant();
            mode = (mode ?? "memory").ToLowerInvariant();
            if (string.IsNullOrEmpty(content))
                return "{\"ok\":false,\"error\":\"missing content\"}";

            ConfigService.ImportResult result = format == "cfg"
                ? ConfigService.ImportCfg(config, content, mode)
                : ConfigService.ImportJson(config, content, mode);

            var sb = new StringBuilder();
            sb.Append("{\"ok\":true,\"mode\":").Append(J(result.Mode));
            sb.Append(",\"updated\":").Append(result.Updated);
            sb.Append(",\"skipped\":").Append(result.Skipped);
            sb.Append(",\"errors\":[");
            for (int i = 0; i < result.Errors.Count; i++)
            {
                var e = result.Errors[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"section\":").Append(J(e.Section));
                sb.Append(",\"key\":").Append(J(e.Key));
                sb.Append(",\"error\":").Append(J(e.Error)).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }


        private static string AcceptsJson(object accepts)
        {
            if (accepts == null) return "null";
            if (accepts is Dictionary<string, object> d)
            {
                var sb = new StringBuilder();
                sb.Append('{');
                bool first = true;
                foreach (var kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(J(kv.Key)).Append(':');
                    if (kv.Value is System.Collections.IEnumerable list && kv.Value is not string)
                    {
                        sb.Append('[');
                        bool f2 = true;
                        foreach (var x in list)
                        {
                            if (!f2) sb.Append(',');
                            f2 = false;
                            sb.Append(JV(x));
                        }
                        sb.Append(']');
                    }
                    else sb.Append(JV(kv.Value));
                }
                sb.Append('}');
                return sb.ToString();
            }
            return "null";
        }

        private static string J(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string JV(object v)
        {
            if (v == null) return "null";
            switch (v)
            {
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case float f: return f.ToString(CultureInfo.InvariantCulture);
                case double d: return d.ToString(CultureInfo.InvariantCulture);
                case string s: return J(s);
                default:
                    if (v is IFormattable fmt && v.GetType().IsPrimitive)
                        return fmt.ToString(null, CultureInfo.InvariantCulture);
                    return J(Convert.ToString(v, CultureInfo.InvariantCulture));
            }
        }

        private static bool TryGetString(string json, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(json)) return false;
            string pattern = "\"" + key + "\"";
            int i = json.IndexOf(pattern, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i + pattern.Length);
            if (i < 0) return false;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return false;
            if (json[i] == 'n') { value = null; return true; }
            if (json[i] != '"') return false;
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '\\' && i < json.Length)
                {
                    char n = json[i++];
                    sb.Append(n switch { 'n' => '\n', 'r' => '\r', 't' => '\t', '"' => '"', '\\' => '\\', _ => n });
                }
                else if (c == '"') break;
                else sb.Append(c);
            }
            value = sb.ToString();
            return true;
        }

        private static bool TryGetRawValue(string json, string key, out string raw)
        {
            raw = null;
            if (string.IsNullOrEmpty(json)) return false;
            string pattern = "\"" + key + "\"";
            int i = json.IndexOf(pattern, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i + pattern.Length);
            if (i < 0) return false;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return false;

            if (json[i] == '"')
                return TryGetString(json, key, out raw);

            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']')
                i++;
            raw = json.Substring(start, i - start).Trim();
            return raw.Length > 0;
        }
    }
}
