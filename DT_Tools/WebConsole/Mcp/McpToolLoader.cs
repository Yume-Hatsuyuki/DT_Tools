using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Commands;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp
{
    /// <summary>单个 MCP 工具的运行期定义（Schema 在加载时求值一次并缓存）。</summary>
    internal sealed class McpToolDef
    {
        public string Name;
        public string Description;
        public string Author;
        public JObject SchemaJson;
        public Func<JObject, CommandResult> Execute;
    }

    /// <summary>
    /// MCP 工具注册表：反射发现本程序集全部 [McpTool] 类。与 CommandRegistry 同风格，
    /// 零注册代码；同名工具直接报错。工具类契约：static JObject Schema() +
    /// static CommandResult Execute(JObject args)（返回值经 Json.To 进 MCP content）。
    /// </summary>
    internal static class McpToolLoader
    {
        private static Dictionary<string, McpToolDef> _tools;

        public static IReadOnlyDictionary<string, McpToolDef> Tools => _tools;

        public static void Load()
        {
            if (_tools != null) return;

            var map = new Dictionary<string, McpToolDef>(StringComparer.Ordinal);
            foreach (var type in typeof(McpToolLoader).Assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<McpToolAttribute>();
                if (attr == null)
                    continue;

                if (string.IsNullOrWhiteSpace(attr.Author))
                    throw new InvalidOperationException($"MCP 工具 {type.FullName} 漏写 Author（逐工具实名，禁止兜底）");

                var schemaMethod = type.GetMethod("Schema", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                var executeMethod = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(JObject) }, null);
                if (schemaMethod == null || executeMethod == null)
                    throw new InvalidOperationException($"MCP 工具 {type.FullName} 缺少 static Schema() 或 static Execute(JObject)");

                if (map.ContainsKey(attr.Name))
                    throw new InvalidOperationException($"MCP 工具重名: {attr.Name}");

                map[attr.Name] = new McpToolDef
                {
                    Name = attr.Name,
                    Description = attr.Description,
                    Author = attr.Author.Trim(),
                    SchemaJson = (JObject)schemaMethod.Invoke(null, null),
                    Execute = args => (CommandResult)executeMethod.Invoke(null, new object[] { args }),
                };
            }

            _tools = map;
            Log.Info("Mcp", $"已加载 {map.Count} 个 MCP 工具：{string.Join("、", map.Keys.OrderBy(n => n, StringComparer.Ordinal))}");
        }
    }
}
