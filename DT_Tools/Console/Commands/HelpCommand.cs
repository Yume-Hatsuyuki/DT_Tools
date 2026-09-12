using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /help  /？
    /// 列出所有已注册命令及其用法。
    /// </summary>
    internal sealed class HelpCommand : IConsoleCommand
    {
        private readonly Dictionary<string, IConsoleCommand> _registry;

        public HelpCommand(Dictionary<string, IConsoleCommand> registry)
        {
            _registry = registry;
        }

        public string   Name        => "help";
        public string[] Aliases     => new[] { "?", "？" };
        public string   Usage       => "help [命令名]";
        public string   Description => "显示所有命令，或查询某个命令的详细用法。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            // /help <cmd> — 查询单条
            if (args.Length > 0)
            {
                string key = args[0];
                if (_registry.TryGetValue(key, out var cmd))
                {
                    console.Log($"用法: /{cmd.Usage}", LogLevel.Info);
                    console.Log($"说明: {cmd.Description}", LogLevel.Info);
                    if (cmd.Aliases.Length > 0)
                        console.Log($"短命令: {string.Join(", ", cmd.Aliases.Select(a => "/" + a))}", LogLevel.Info);
                    if (!string.IsNullOrEmpty(cmd.Author))
                        console.Log($"作者: {cmd.Author}", LogLevel.Info);
                }
                else
                {
                    console.Log($"未知命令: {key}", LogLevel.Warning);
                }
                return;
            }

            // 列出所有（去重：只显示 Name，跳过别名键）
            var seen  = new HashSet<string>();
            var lines = new StringBuilder();
            lines.AppendLine("━━━ 可用命令 ━━━");
            foreach (var kv in _registry)
            {
                if (kv.Key != kv.Value.Name) continue;   // 是别名，跳过
                if (!seen.Add(kv.Value.Name))  continue;  // 已显示

                // 第一行：用法
                lines.AppendLine($"/{kv.Value.Usage}");

                // 第二行：作者（如果有）
                if (!string.IsNullOrEmpty(kv.Value.Author))
                    lines.AppendLine($"      功能制作者：[{kv.Value.Author}]");

                // 第三行：描述
                lines.AppendLine($"      {kv.Value.Description}");

                // 第四行：短命令（如果有）
                if (kv.Value.Aliases.Length > 0)
                    lines.AppendLine($"      短命令: {string.Join(", ", kv.Value.Aliases.Select(a => "/" + a))}");
            }
            lines.Append("提示: /help <命令名> 查看详情");
            console.Log(lines.ToString(), LogLevel.Info);
        }
    }
}
