using System;
using System.Linq;
using DT_Tools.Commands;
using DT_Tools.WebConsole.Api;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp.Tools.ReadLog
{
    /// <summary>read_log：全局日志尾部。RingBuffer 自带锁，HTTP 线程可直接读（同 /api/log）。</summary>
    [McpTool("read_log",
        "读取全局日志尾部（环形缓冲 2000 条）：命令输出、警告、异常都在这里。run_command 之后建议先 read_log 核对命令的人类可读输出，再决定下一步。",
        Author = "梦初雪")]
    internal static class ReadLogTool
    {
        public static JObject Schema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["count"] = new JObject { ["type"] = "integer", ["description"] = "尾部条数，默认 80，上限 500" },
                ["level"] = new JObject { ["type"] = "string", ["description"] = "可选：按级别过滤 INFO/WARN/ERROR/FATAL（不区分大小写）" },
                ["filter"] = new JObject { ["type"] = "string", ["description"] = "可选：子串过滤（匹配 tag 或正文）" },
            },
            ["additionalProperties"] = false,
        };

        public static CommandResult Execute(JObject args)
        {
            int count = 80;
            if (args["count"] != null && int.TryParse(args["count"].ToString(), out int parsed) && parsed > 0)
                count = Math.Min(parsed, 500);
            string level = args["level"]?.ToString().ToUpperInvariant();
            string filter = args["filter"]?.ToString();

            var (_, entries) = Log.SnapshotEntries();
            var filtered = entries
                .Where(e => level == null || e.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                .Where(e => filter == null
                            || (e.Tag?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            || (e.Message?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            // 先过滤后取尾：过滤语义下"最近 count 条匹配"比"最近 count 条里挑匹配"更符合调用意图
            var page = filtered.Skip(Math.Max(0, filtered.Count - count)).Select(LogsApi.Shape).ToList();
            return CommandResult.Success(new { total = filtered.Count, returned = page.Count, entries = page });
        }
    }
}
