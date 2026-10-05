using DT_Tools.Commands;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp.Tools.RunCommand
{
    /// <summary>run_command：一切行动能力的入口。命令在 Unity 主线程执行（经 RunOnMain，5s 超时）。</summary>
    [McpTool("run_command",
        "执行一条 DT_Tools 命令并返回结果信封 {ok,error,data}。先 list_commands 查可用命令。"
        + "发包：call_me <C_包名> [json]（本机身份）、call_c #id <C_包名> [json] 与 call_s all|#id <S_包名> [json]（仅房主）——"
        + "包语义与风险先查仓库 .github/skills/MCP/MCP.md（功能列表），未收录的包按同目录 SKILL.md 维护协议补录。观察画面用 shot。"
        + "命令输出与警告同时写进全局日志，可再 read_log 核对。",
        Author = "梦初雪")]
    internal static class RunCommandTool
    {
        public static JObject Schema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["command"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "完整命令行（含参数，前导斜杠可有可无），例：game_state 或 call_me C_PICK_CHARACTER {\"characterId\":3}",
                },
            },
            ["required"] = new JArray { "command" },
            ["additionalProperties"] = false,
        };

        public static CommandResult Execute(JObject args)
        {
            string command = args["command"]?.ToString();
            if (string.IsNullOrWhiteSpace(command))
                return CommandResult.Fail("empty command");

            // 命令触碰 Unity API 与游戏单例，必须主线程执行（复用 /api/run 的主线程投递与超时保护）
            return WebConsole.RunOnMain(() => WebConsole.ExecuteCommandText(command));
        }
    }
}
