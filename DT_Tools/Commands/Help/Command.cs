using System;
using System.Linq;
using DT_Tools.Commands;

namespace DT_Tools.Commands.Help
{
    /// <summary>
    /// /help — 列出全部已注册命令，或查询单条命令的用法/别名/门禁。
    /// 数据源即 CommandRegistry 的反射注册结果，无独立状态；纯读命令，房主与普通客户端均可执行。
    /// </summary>
    internal sealed class HelpCommand : ICommand
    {
        public string Name => "help";
        public string[] Aliases => new[] { "?", "？" };
        public string Usage => "help [命令名]";
        public string Description => "显示所有命令，或查询某个命令的详细用法。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                var commands = CommandRegistry.All
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                ctx.Reply(HelpFormat.ListText(commands));
                return CommandResult.Success(HelpFormat.ListData(commands));
            }

            string key = ctx.Args[0];
            if (!CommandRegistry.TryGet(key, out var command))
            {
                ctx.Warn($"未知命令: {key}。输入 /help 查看全部命令。");
                return CommandResult.Fail("unknown command", new { name = key });
            }

            ctx.Reply(HelpFormat.DetailText(command));
            return CommandResult.Success(HelpFormat.DetailData(command));
        }
    }
}
