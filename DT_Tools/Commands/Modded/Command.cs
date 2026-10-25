using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.Modded
{
    internal sealed class ModdedCommand : ICommand
    {
        public string Name => "modded";
        public string[] Aliases => new[] { "mod", "check_mod", "改装" };
        public string Usage => "modded [all|#id]";
        public string Description => "列出所有玩家的 modded（改装检测）标记；all 列出全部，可指定 #id。房主与客户端均可用。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.PlayerIdentity | FeatureTag.SysNetwork;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!TryParse(ctx.Args, out int filterPid, out bool listAll, out string error))
            {
                ctx.Reply($"{error}\n用法: /modded [all|#id]");
                return CommandResult.Fail("invalid args");
            }
            if (!listAll && filterPid <= 0)
            {
                ctx.Reply(ModdedFormat.Help());
                return CommandResult.Success(new { help = true });
            }

            var result = ModdedLogic.Collect(listAll ? 0 : filterPid);
            if (!result.Ok)
            {
                ctx.Reply(result.ErrorMessage);
                return CommandResult.Fail(result.ErrorCode);
            }

            int myId = Managers.Player?.MyPlayerID ?? 0;
            ctx.Reply(ModdedFormat.Reply(result, myId));
            return CommandResult.Success(ModdedFormat.Result(result, myId));
        }

        private static bool TryParse(string[] args, out int filterPid, out bool listAll, out string error)
        {
            filterPid = 0;
            listAll = false;
            error = null;
            if (args == null || args.Length == 0)
                return true;

            string first = (args[0] ?? "").Trim();
            if (first.Length == 0)
                return true;

            if (first.Equals("all", System.StringComparison.OrdinalIgnoreCase)
                || first == "*"
                || first == "全部")
            {
                listAll = true;
                return true;
            }

            string idToken = first;
            if (idToken.StartsWith("#", System.StringComparison.Ordinal))
                idToken = idToken.Substring(1).Trim();

            if (int.TryParse(idToken, out int pid) && pid > 0)
            {
                filterPid = pid;
                return true;
            }

            error = $"无法解析参数 '{first}'。";
            return false;
        }
    }
}
