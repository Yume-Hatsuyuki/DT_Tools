using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DT_Tools.Commands;
using DT_Tools.Core;

namespace DT_Tools.Commands.Help
{
    /// <summary>/help 输出格式化：列表/详情的人类文本 + JSON 结果 DTO。</summary>
    internal static class HelpFormat
    {
        public static string ListText(IReadOnlyList<ICommand> commands)
        {
            var text = new StringBuilder();
            text.AppendLine($"━━━ 可用命令（{commands.Count} 条）━━━");
            foreach (var c in commands)
            {
                text.AppendLine($"/{c.Usage}");
                text.AppendLine(c.RequireHost ? $"      【房主】{c.Description}" : $"      {c.Description}");
                if (c.Aliases is { Length: > 0 })
                    text.AppendLine($"      短命令: {string.Join(", ", c.Aliases.Select(a => "/" + a))}");
                text.AppendLine($"      功能制作者：[{(string.IsNullOrEmpty(c.Author) ? "佚名" : c.Author)}]");
            }
            text.Append("提示: /help <命令名> 查看详情");
            return text.ToString();
        }

        public static object ListData(IReadOnlyList<ICommand> commands) => new
        {
            count = commands.Count,
            commands = commands.Select(Item),
        };

        public static string DetailText(ICommand c)
        {
            var text = new StringBuilder();
            text.AppendLine($"用法: /{c.Usage}");
            text.AppendLine($"说明: {c.Description}");
            if (c.RequireHost)
                text.AppendLine("门禁: 仅房主可用");
            if (c.Aliases is { Length: > 0 })
                text.AppendLine($"短命令: {string.Join(", ", c.Aliases.Select(a => "/" + a))}");
            text.Append($"功能制作者: {(string.IsNullOrEmpty(c.Author) ? "佚名" : c.Author)}");
            return text.ToString();
        }

        public static object DetailData(ICommand c) => Item(c);

        private static object Item(ICommand c) => new
        {
            name = c.Name,
            usage = c.Usage,
            description = c.Description,
            aliases = c.Aliases ?? Array.Empty<string>(),
            require_host = c.RequireHost,
            author = string.IsNullOrEmpty(c.Author) ? "佚名" : c.Author,
        };
    }
}
