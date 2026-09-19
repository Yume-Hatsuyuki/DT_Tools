using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DT_Tools.Console.Http
{
    /// <summary>
    /// 全项目唯一的手写 JSON 序列化实现（无第三方依赖）。
    /// 此前 ConfigApi / AutomationApi 各复制了一份 J / JV / AcceptsJson，
    /// 两份逐渐漂移，导致枚举 Accepts 在 Automation 页被序列化成 "System.String[]"。
    /// 现统一收敛于此；新增类型支持只需改这一处。
    /// </summary>
    internal static class JsonWriter
    {
        public static string Bool(bool v) => v ? "true" : "false";

        /// <summary>JSON 字符串字面量（含引号与转义）；null → null。</summary>
        public static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
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
                        if (c < 32)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// 任意值 → JSON。递归支持 null / bool / 数值 / 字符串 / 枚举 /
        /// IDictionary&lt;string,object&gt; / IEnumerable（数组）。
        /// 关键：IEnumerable 被展开为 JSON 数组，而不是 ToString()。
        /// </summary>
        public static string Value(object v)
        {
            if (v == null) return "null";

            switch (v)
            {
                case bool b:
                    return Bool(b);
                case string s:
                    return Str(s);
                case Enum e:
                    return Str(e.ToString());
                case byte or sbyte or short or ushort or int or uint or long or ulong:
                    return Convert.ToString(v, CultureInfo.InvariantCulture);
                case float f:
                    return FiniteOrNull(f, Convert.ToString(f, CultureInfo.InvariantCulture));
                case double d:
                    return FiniteOrNull(d, Convert.ToString(d, CultureInfo.InvariantCulture));
                case decimal m:
                    return Convert.ToString(m, CultureInfo.InvariantCulture);
                case IDictionary<string, object> dict:
                    return Object(dict);
                case IEnumerable seq:
                    return Array(seq);
                default:
                    return Str(Convert.ToString(v, CultureInfo.InvariantCulture));
            }
        }

        public static string Array(IEnumerable seq)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var x in seq)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Value(x));
            }
            sb.Append(']');
            return sb.ToString();
        }

        public static string Object(IEnumerable<KeyValuePair<string, object>> pairs)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in pairs)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Str(kv.Key)).Append(':').Append(Value(kv.Value));
            }
            sb.Append('}');
            return sb.ToString();
        }

        // NaN / Infinity 不是合法 JSON，输出 null 以免前端 JSON.parse 整体失败
        private static string FiniteOrNull(double d, string text)
            => (double.IsNaN(d) || double.IsInfinity(d)) ? "null" : text;
    }
}
