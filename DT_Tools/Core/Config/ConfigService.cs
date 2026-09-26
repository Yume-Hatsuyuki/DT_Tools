using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using DT_Tools.Core.Attributes;
using Newtonsoft.Json;

namespace DT_Tools.Core
{
    /// <summary>
    /// 配置枚举与读写（内存优先；Save / overwrite 才落盘）。
    /// 导入解析用 Newtonsoft（JToken），禁止手写逐字符扫描。
    /// </summary>
    public static class ConfigService
    {
        public sealed class SectionDto
        {
            public string Section;

            /// <summary>段落分组："automation"（自动化总开关+各模块，AUTOMATION 页）| "feature"（其余，CONFIG 页）。前端据此分流，不认识任何段名。</summary>
            public string Group;

            public List<EntryDto> Entries = new List<EntryDto>();
        }

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

        public sealed class ImportResult
        {
            public string Mode;
            public int Updated;
            public int Skipped;
            public List<(string Section, string Key, string Error)> Errors =
                new List<(string, string, string)>();
        }

        // ---- 枚举 ----

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
                    sec = new SectionDto { Section = def.Section, Group = ClassifyGroup(def.Section) };
                    bySection[def.Section] = sec;
                }

                sec.Entries.Add(ToDto(def, entry));
            }

            foreach (var sec in bySection.Values)
            {
                sec.Entries = sec.Entries
                    .OrderBy(e => e.Key == Engine.EnabledKey ? 0 : 1)
                    .ThenBy(e => e.Key, StringComparer.Ordinal)
                    .ToList();
            }

            return bySection.Values
                .OrderBy(s => s.Section, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 段落分组：全组判定零段名字符串——基础设施段查装载期收集的
        /// [ConfigSection(Group=…)] 元数据（Engine.GroupOfSection），自动化模块段查
        /// Engine.AutomationModules 元数据，其余（补丁功能 / WebConsole 基础设施）→ "feature"。
        /// 前端按此分流显示，禁止硬编码段名。
        /// </summary>
        private static string ClassifyGroup(string section)
        {
            string group = Engine.GroupOfSection(section);
            if (group != null)
                return group;
            foreach (var module in Engine.AutomationModules)
            {
                if (string.Equals(module.Section, section, StringComparison.Ordinal))
                    return ConfigSectionAttribute.GroupAutomation;
            }
            return "feature";
        }

        // ---- 更新 / 保存 / 重置 ----

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
                catch
                {
                    // 段/键不存在：按 0 处理
                }
                return n;
            }

            foreach (ConfigDefinition def in config.Keys)
            {
                if (!string.IsNullOrEmpty(section) &&
                    !string.Equals(def.Section, section, StringComparison.Ordinal))
                    continue;
                var entry = config[def];
                if (entry == null) continue;
                entry.BoxedValue = entry.DefaultValue;
                n++;
            }
            return n;
        }

        // ---- 导出 / 导入 ----

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

        /// <summary>
        /// 导入 JSON 兼容两种形状（Newtonsoft 解析）：
        ///   [{ "section":…, "entries":[{ "key":…, "value":… }]}]  —— 配置列表导出格式
        ///   [{ "section":…, "key":…, "value":… }]                 —— 扁平对
        /// </summary>
        private static IEnumerable<(string Section, string Key, string Value)> ParseImportPairs(string json)
        {
            var list = new List<(string, string, string)>();
            if (Json.TryFrom<List<ImportSectionDto>>(json, out var sections) && sections != null)
            {
                foreach (var sec in sections)
                {
                    if (sec?.Entries == null) continue;
                    foreach (var e in sec.Entries)
                    {
                        if (string.IsNullOrEmpty(sec.Section) || string.IsNullOrEmpty(e?.Key)) continue;
                        list.Add((sec.Section, e.Key, NormalizeRaw(e.Value)));
                    }
                }
                if (list.Count > 0)
                    return list;
            }

            if (Json.TryFrom<List<ImportPairDto>>(json, out var pairs) && pairs != null)
            {
                foreach (var p in pairs)
                {
                    if (string.IsNullOrEmpty(p?.Section) || string.IsNullOrEmpty(p.Key)) continue;
                    list.Add((p.Section, p.Key, NormalizeRaw(p.Value)));
                }
            }
            return list;
        }

        private sealed class ImportSectionDto
        {
            [JsonProperty("section")] public string Section;
            [JsonProperty("entries")] public List<ImportEntryDto> Entries;
        }

        private sealed class ImportEntryDto
        {
            [JsonProperty("key")] public string Key;
            [JsonProperty("value")] public object Value;
        }

        private sealed class ImportPairDto
        {
            [JsonProperty("section")] public string Section;
            [JsonProperty("key")] public string Key;
            [JsonProperty("value")] public object Value;
        }

        /// <summary>JSON 反序列化出的 object 值 → 配置更新用的原始字符串（与导出格式互逆）。</summary>
        private static string NormalizeRaw(object value)
        {
            switch (value)
            {
                case null: return null;
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
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

        // ---- DTO / 解析 ----

        private static EntryDto ToDto(ConfigDefinition def, ConfigEntryBase entry)
        {
            return new EntryDto
            {
                Section = def.Section,
                Key = def.Key,
                Type = entry.SettingType.Name,
                Value = DisplayValue(entry.BoxedValue),
                Default = DisplayValue(entry.DefaultValue),
                Description = entry.Description?.Description ?? "",
                Accepts = ExtractAccepts(def, entry)
            };
        }

        /// <summary>
        /// 枚举值序列化为成员名（accepts.options 用的就是名称；裸 BoxedValue 会被
        /// JSON 写成数字，前端下拉匹配不上，出现「0 (未在列表中)」）。
        /// </summary>
        private static object DisplayValue(object value)
            => value is Enum e ? Enum.GetName(e.GetType(), e) ?? e.ToString() : value;

        /// <summary>
        /// 把三种"可选值来源"归一成同一份结构，前端只需认这一种协议：
        ///   options : [{value,label}]  —— 下拉（label 给人看，value 写回配置）
        ///   values  : [string]         —— options 的纯值列表（兼容旧前端）
        ///   min/max : 数值范围
        /// 来源优先级：OptionProviders（动态） > 枚举 > AcceptableValueList/Range。
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
            catch
            {
                // 校验器不可用时放行（与旧版一致）
            }
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
