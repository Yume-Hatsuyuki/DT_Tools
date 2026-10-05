using System;
using System.Linq;
using DT_Tools.Commands;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp.Tools.ListCommands
{
    /// <summary>list_commands：命令总表。纯内存读（CommandRegistry 启动后恒定），不经主线程。</summary>
    [McpTool("list_commands",
        "列出全部 DT_Tools 命令（name/usage/description/requireHost）。行动能力（发包、操控、查询、自动化）全部经 run_command 执行这些命令；本工具是 run_command 前的查阅面。",
        Author = "梦初雪")]
    internal static class ListCommandsTool
    {
        public static JObject Schema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
            ["additionalProperties"] = false,
        };

        public static CommandResult Execute(JObject args)
            => CommandResult.Success(CommandRegistry.All
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new
                {
                    name = c.Name,
                    usage = c.Usage,
                    description = c.Description,
                    requireHost = c.RequireHost,
                    author = string.IsNullOrEmpty(c.Author) ? "佚名" : c.Author,
                })
                .ToList());
    }
}
