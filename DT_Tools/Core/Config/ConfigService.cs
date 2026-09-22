using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Configuration;

namespace DT_Tools.Core
{
    /// <summary>
    /// 配置枚举与读写（内存优先；Save / overwrite 才落盘）。
    /// </summary>
    internal static class ConfigService
    {
        public sealed class EntryDto
        {
            public string Section;
            public string Key;
            public string Type;
            public object Value;
            public object Default;
            public string Description;
            public object Accepts;
        }

        public sealed class SectionDto
        {
            public string Section;
            public List<EntryDto> Entries = new List<EntryDto>();
        }

        public sealed class ImportResult
        {
            public string Mode;
            public int Updated;
            public int Skipped;
            public List<(string Section, string Key, string Error)> Errors =
                new List<(string, string, string)>();
        }

        public static List<SectionDto> List(ConfigFile config)
        {
            var bySection = new Dictionary<string, SectionDto>(StringComparer.Ordinal);

            foreach (ConfigDefinition def in config.Keys)
            {
                ConfigEntryBase entry = config[def];
                if (entry == null)
                    continue;

                if (!bySection.TryGetValue(def.Section, out var sec))
                {
                    sec = new SectionDto { Section = def.Section };
                    bySection[def.Section] = sec;
                }

                sec.Entries.Add(ToDto(def, entry));
            }

            foreach (var sec in bySection.Values)
            {
                sec.Entries = sec.Entries
                    .OrderBy(e => e.Key == "Enabled" ? 0 : 1)
                    .ThenBy(e => e.Key, StringComparer.Ordinal)
                    .ToList();
            }

            return bySection.Values
                .OrderBy(s => s.Section, StringComparer.Ordinal)
                .ToList();
        }

        public static (bool ok, string error, object value) Update(
            ConfigFile config, string section, string key, string rawValue)
        {
            ConfigEntryBase entry;
            try
            {
                entry = config[section, key];
            }
            catch
            {
                return (false, "not found", null);
            }

            if (entry == null)
                return (false, "not found", null);

            try
            {
                object parsed = ParseValue(rawValue, entry.SettingType);
                if (!IsAcceptable(entry, parsed, out string rangeError))
                    return (false, rangeError ?? "out of range", null);

                entry.BoxedValue = parsed;
                return (true, null, entry.BoxedValue);
            }
            catch (Exception ex)
            {
                return (false, "type mismatch: " + ex.Message, null);
            }
        }

        public static void Save(ConfigFile config) => config.Save();

        public static int Reset(ConfigFile config, string section, string key)
        {
            int n = 0;
            if (!string.IsNullOrEmpty(section) && !string.IsNullOrEmpty(key))
            {
                try
                {
                    var entry = config[section, key];
                    if (entry != null)
                    {
                        entry.BoxedValue = entry.DefaultValue;
                        n = 1;
                    }
                }
                catch { }
                return n;
            }

            if (!string.IsNullOrEmpty(section))
            {
                foreach (ConfigDefinition def in config.Keys)
                {
                    if (!string.Equals(def.Section, section, StringComparison.Ordinal))
                        continue;
                    var entry = config[def];
                    if (entry == null) continue;
                    entry.BoxedValue = entry.DefaultValue;
                    n++;
                }
                return n;
            }

            foreach (ConfigDefinition def in config.Keys)
            {
                var entry = config[def];
                if (entry == null) continue;
                entry.BoxedValue = entry.DefaultValue;
                n++;
            }
            return n;
        }

        public static string ExportCfg(ConfigFile config)
        {
            var sb = new StringBuilder();
            foreach (var sec in List(config))
            {
                sb.Append('[').Append(sec.Section).AppendLine("]");
                foreach (var e in sec.Entries)
                {
                    if (!string.IsNullOrEmpty(e.Description))
                    {
                        foreach (var line in e.Description.Split('\n'))
                            sb.Append("## ").AppendLine(line.TrimEnd());
                    }
                    sb.Append(e.Key).Append(" = ").AppendLine(FormatValue(e.Value));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static ImportResult ImportCfg(ConfigFile config, string text, string mode)
        {
            var result = new ImportResult { Mode = mode ?? "memory" };
            string section = null;
            foreach (var rawLine in (text ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                if (section == null) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                ApplyImportPair(config, result, section, key, val);
            }
            if (string.Equals(result.Mode, "overwrite", StringComparison.OrdinalIgnoreCase))
                config.Save();
            return result;
        }

        public static ImportResult ImportJson(ConfigFile config, string json, string mode)
        {
            var result = new ImportResult { Mode = mode ?? "memory" };
            foreach (var (section, key, value) in ParseImportPairs(json ?? ""))
                ApplyImportPair(config, result, section, key, value);
            if (string.Equals(result.Mode, "overwrite", StringComparison.OrdinalIgnoreCase))
                config.Save();
            return result;
        }

        private static void ApplyImportPair(
            ConfigFile config, ImportResult result, string section, string key, string val)
        {
            try
            {
                ConfigEntryBase entry = config[section, key];
                if (entry == null)
                {
                    result.Skipped++;
                    return;
                }
                var (ok, error, _) = Update(config, section, key, val);
                if (ok) result.Updated++;
                else result.Errors.Add((section, key, error ?? "error"));
            }
            catch
            {
                result.Skipped++;
            }
        }

        private static IEnumerable<(string Section, string Key, string Value)> ParseImportPairs(string json)
        {
            var list = new List<(string, string, string)>();
            int pos = 0;
            while (pos < json.Length)
            {
                int sIdx = json.IndexOf("\"section\"", pos, StringComparison.Ordinal);
                if (sIdx < 0) break;
                if (!TryExtractStringAfter(json, sIdx, out string section, out int afterSec))
                {
                    pos = sIdx + 9;
                    continue;
                }

                int entriesIdx = json.IndexOf("\"entries\"", afterSec, StringComparison.Ordinal);
                int nextSec = json.IndexOf("\"section\"", afterSec, StringComparison.Ordinal);

                if (entriesIdx >= 0 && (nextSec < 0 || entriesIdx < nextSec))
                {
                    int arrStart = json.IndexOf('[', entriesIdx);
                    int arrEnd = FindMatchingBracket(json, arrStart);
                    if (arrStart >= 0 && arrEnd > arrStart)
                    {
                        string arr = json.Substring(arrStart, arrEnd - arrStart + 1);
                        int p2 = 0;
                        while (p2 < arr.Length)
                        {
                            int kIdx = arr.IndexOf("\"key\"", p2, StringComparison.Ordinal);
                            if (kIdx < 0) break;
                            if (!TryExtractStringAfter(arr, kIdx, out string key, out int afterKey))
                            {
                                p2 = kIdx + 5;
                                continue;
                            }
                            int vIdx = arr.IndexOf("\"value\"", afterKey, StringComparison.Ordinal);
                            if (vIdx < 0)
                            {
                                p2 = afterKey;
                                continue;
                            }
                            if (!TryExtractRawAfter(arr, vIdx, out string val, out int afterVal))
                            {
                                p2 = vIdx + 7;
                                continue;
                            }
                            list.Add((section, key, val));
                            p2 = afterVal;
                        }
                        pos = arrEnd + 1;
                        continue;
                    }
                }

                int k2 = json.IndexOf("\"key\"", afterSec, StringComparison.Ordinal);
                int v2 = json.IndexOf("\"value\"", afterSec, StringComparison.Ordinal);
                if (k2 >= 0 && v2 >= 0 && (nextSec < 0 || k2 < nextSec))
                {
                    if (TryExtractStringAfter(json, k2, out string key, out _) &&
                        TryExtractRawAfter(json, v2, out string val, out int afterV))
                    {
                        list.Add((section, key, val));
                        pos = afterV;
                        continue;
                    }
                }
                pos = afterSec;
            }
            return list;
        }

        private static int FindMatchingBracket(string s, int openIdx)
        {
            if (openIdx < 0 || openIdx >= s.Length || s[openIdx] != '[') return -1;
            int depth = 0;
            for (int i = openIdx; i < s.Length; i++)
            {
                if (s[i] == '[') depth++;
                else if (s[i] == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static bool TryExtractStringAfter(string json, int keyIdx, out string value, out int end)
        {
            value = null;
            end = keyIdx;
            int colon = json.IndexOf(':', keyIdx);
            if (colon < 0) return false;
            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return false;
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '\\' && i < json.Length)
                {
                    char n = json[i++];
                    sb.Append(n switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '"' => '"',
                        '\\' => '\\',
                        _ => n
                    });
                }
                else if (c == '"') break;
                else sb.Append(c);
            }
            value = sb.ToString();
            end = i;
            return true;
        }

        private static bool TryExtractRawAfter(string json, int keyIdx, out string value, out int end)
        {
            value = null;
            end = keyIdx;
            int colon = json.IndexOf(':', keyIdx);
            if (colon < 0) return false;
            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return false;
            if (json[i] == '"')
                return TryExtractStringAfter(json, keyIdx, out value, out end);
            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']')
                i++;
            value = json.Substring(start, i - start).Trim();
            end = i;
            return value.Length > 0;
        }

        private static EntryDto ToDto(ConfigDefinition def, ConfigEntryBase entry)
        {
            return new EntryDto
            {
                Section = def.Section,
                Key = def.Key,
                Type = entry.SettingType.Name,
                Value = entry.BoxedValue,
                Default = entry.DefaultValue,
                Description = entry.Description?.Description ?? "",
                Accepts = ExtractAccepts(def, entry)
            };
        }

        /// <summary>
        /// 把三种"可选值来源"归一成同一份结构，前端只需认这一种协议：
        ///   options : [{value,label}]  —— 下拉（label 给人看，value 写回配置）
        ///   values  : [string]         —— options 的纯值列表（兼容旧前端）
        ///   min/max : 数值范围
        /// 来源优先级：OptionProviders（动态） > 枚举 > AcceptableValueList。
        /// </summary>
        private static object ExtractAccepts(ConfigDefinition def, ConfigEntryBase entry)
        {
            // 1) 显式声明的动态选项（角色表、出生点等运行时才知道的数据）
            var dyn = OptionProviders.TryGet(def.Section, def.Key);
            if (dyn != null)
                return OptionsAccepts(dyn);

            // 2) 枚举：不走 AcceptableValueList（Unity 上 MakeGenericType 会失败），
            //    在此用 Enum.GetNames 补全。
            if (entry.SettingType != null && entry.SettingType.IsEnum)
            {
                var opts = new List<ConfigOption>();
                foreach (string name in Enum.GetNames(entry.SettingType))
                    opts.Add(new ConfigOption(name));
                return OptionsAccepts(opts);
            }

            var acc = entry.Description?.AcceptableValues;
            if (acc == null) return null;

            var t = acc.GetType();
            if (t.IsGenericType &&
                t.GetGenericTypeDefinition().Name.StartsWith("AcceptableValueRange", StringComparison.Ordinal))
            {
                object min = t.GetProperty("MinValue")?.GetValue(acc);
                object max = t.GetProperty("MaxValue")?.GetValue(acc);
                return new Dictionary<string, object> { ["min"] = min, ["max"] = max };
            }

            if (t.IsGenericType &&
                t.GetGenericTypeDefinition().Name.StartsWith("AcceptableValueList", StringComparison.Ordinal))
            {
                var arr = t.GetProperty("AcceptableValues")?.GetValue(acc) as Array;
                if (arr != null)
                {
                    var opts = new List<ConfigOption>();
                    foreach (var x in arr)
                        opts.Add(new ConfigOption(Convert.ToString(x, CultureInfo.InvariantCulture)));
                    return OptionsAccepts(opts);
                }
            }

            return null;
        }

        private static Dictionary<string, object> OptionsAccepts(IReadOnlyList<ConfigOption> opts)
        {
            var options = new List<object>(opts.Count);
            var values = new List<object>(opts.Count);
            foreach (var o in opts)
            {
                options.Add(new Dictionary<string, object>
                {
                    ["value"] = o.Value,
                    ["label"] = o.Label
                });
                values.Add(o.Value);
            }
            return new Dictionary<string, object>
            {
                ["options"] = options,
                ["values"] = values
            };
        }

        private static object ParseValue(string raw, Type type)
        {
            if (type == typeof(string))
                return raw ?? "";

            if (type == typeof(bool))
            {
                if (bool.TryParse(raw, out bool b)) return b;
                if (raw == "1") return true;
                if (raw == "0") return false;
                throw new FormatException("expected bool");
            }

            if (type == typeof(int))
                return int.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(float))
                return float.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(double))
                return double.Parse(raw, CultureInfo.InvariantCulture);
            if (type.IsEnum)
                return Enum.Parse(type, raw, ignoreCase: true);

            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }

        private static bool IsAcceptable(ConfigEntryBase entry, object value, out string error)
        {
            error = null;
            var acc = entry.Description?.AcceptableValues;
            if (acc == null) return true;
            try
            {
                var m = acc.GetType().GetMethod("IsValid", new[] { typeof(object) })
                        ?? acc.GetType().GetMethod("IsValid");
                if (m != null)
                {
                    object r = m.Invoke(acc, new[] { value });
                    if (r is bool ok && !ok)
                    {
                        error = "out of range";
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "";
            if (value is bool b) return b ? "true" : "false";
            if (value is IFormattable f)
                return f.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString();
        }
    }
}
