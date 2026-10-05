using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp
{
    /// <summary>JSON-RPC 2.0 信封（MCP 的载体）。响应体一律用 JObject 构建，禁止手拼字符串。</summary>
    internal static class McpRpc
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        public static JObject Result(JToken id, JToken result)
            => new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["result"] = result,
            };

        public static JObject Error(JToken id, int code, string message)
            => new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
            };
    }
}
