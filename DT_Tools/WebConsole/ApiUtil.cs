using System.Net;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// WebConsole API 层内共享的请求体解析工具（AGENTS.md §4：≥2 处才上浮；仅本层使用）：
    /// ParseBody（ConfigApi/AutomationApi/DummyApi 三份同构）、ReadBool（AutomationApi/DummyApi 两份）、
    /// NormalizeRaw（ConfigApi 的 JToken → 配置原始字符串搬移于此）。
    /// Core/ConfigService 另有一份 object 版 NormalizeRaw（与导出格式互逆、输入类型不同，
    /// 分层上 Core 不能引用本层，故各自保留）。
    /// </summary>
    internal static class ApiUtil
    {
        /// <summary>解析 JSON 请求体；空体或非法 JSON 返回 null（由调用方回 "invalid body"，信封形状各异）。</summary>
        public static JObject ParseBody(HttpListenerContext ctx)
        {
            string body = HttpServer.ReadBody(ctx.Request);
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                return JObject.Parse(body);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return null;
            }
        }

        /// <summary>读布尔字段；缺失或类型不符返回 null。</summary>
        public static bool? ReadBool(JObject body, string key)
        {
            var token = body[key];
            if (token == null || token.Type != JTokenType.Boolean)
                return null;
            return (bool)token;
        }

        /// <summary>JToken 值 → 配置更新用的原始字符串（字符串去引号，其余字面量转文本）。</summary>
        public static string NormalizeRaw(JToken token)
        {
            switch (token?.Type)
            {
                case null:
                case JTokenType.Null:
                    return null;
                case JTokenType.String:
                    return (string)token;
                case JTokenType.Boolean:
                    return (bool)token ? "true" : "false";
                default:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
            }
        }
    }
}
