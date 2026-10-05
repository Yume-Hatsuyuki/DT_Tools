using System;
using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Core;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp.Tools.GetConfig
{
    /// <summary>get_config：配置段总表。ConfigService.List 本就供 HTTP 线程直调（同 /api/config/list）。</summary>
    [McpTool("get_config",
        "读取全部配置段（功能开关、参数、默认值、分类），与 WebUI 配置页同数据源。可按 section 过滤。",
        Author = "梦初雪")]
    internal static class GetConfigTool
    {
        public static JObject Schema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["section"] = new JObject { ["type"] = "string", ["description"] = "可选：只返回该配置段（如 Mcp、AutoReady）" },
            },
            ["additionalProperties"] = false,
        };

        public static CommandResult Execute(JObject args)
        {
            var sections = ConfigService.List(Engine.Config);
            string section = args["section"]?.ToString();
            if (!string.IsNullOrEmpty(section))
                sections = sections.Where(s => string.Equals(s.Section, section, StringComparison.OrdinalIgnoreCase)).ToList();
            return CommandResult.Success(sections);
        }
    }
}
