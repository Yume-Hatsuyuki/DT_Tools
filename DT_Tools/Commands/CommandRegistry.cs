using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using DT_Tools.Game;

namespace DT_Tools.Commands
{
    /// <summary>
    /// 命令注册表：反射发现全部 ICommand 实现，主名+别名入大小写不敏感字典；
    /// 统一执行入口负责房主门禁与异常兜底（命令内禁止重复实现）。
    /// </summary>
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, ICommand> Map =
            new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<ICommand> Commands = new List<ICommand>();

        /// <summary>已注册命令（注册顺序，用于 /help 列表）。</summary>
        public static IReadOnlyList<ICommand> All => Commands;

        public static bool TryGet(string name, out ICommand command) => Map.TryGetValue(name, out command);

        public static void Load()
        {
            var types = FeatureLoader.SafeGetTypes(typeof(CommandRegistry).Assembly)
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ICommand).IsAssignableFrom(t));

            foreach (var type in types)
            {
                var command = (ICommand)Activator.CreateInstance(type);
                Commands.Add(command);
                Claim(command.Name, command);
                foreach (var alias in command.Aliases ?? Array.Empty<string>())
                    Claim(alias, command);
            }

            Log.Info("Commands", $"已注册 {Commands.Count} 条命令");
        }

        public static CommandResult Execute(ICommand command, string[] args)
        {
            var ctx = new CommandContext("cmd:" + command.Name, args);
            try
            {
                if (command.RequireHost && !HostGuard.IsHost)
                {
                    ctx.Warn("此命令只能由房主执行。");
                    return CommandResult.Fail("host only");
                }
                return command.Execute(ctx) ?? CommandResult.Fail("no result");
            }
            catch (Exception ex)
            {
                // 错误码必须是稳定的短词：ex.Message 是给人看的诊断文本，进日志不进信封
                Log.Exception("cmd:" + command.Name, ex);
                return CommandResult.Fail("command error");
            }
        }

        private static void Claim(string name, ICommand command)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"{command.GetType().Name} 有空的命令名/别名。");
            if (Map.TryGetValue(name, out var existing) && !ReferenceEquals(existing, command))
                throw new InvalidOperationException(
                    $"命令名/别名冲突：{name}（{existing.GetType().Name} vs {command.GetType().Name}）");
            Map[name] = command;
        }
    }
}
