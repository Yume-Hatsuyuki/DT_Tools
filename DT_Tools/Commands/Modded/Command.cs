using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.Modded
{
    /// <summary>
    /// /modded [all|#id] — 列出玩家及其 modded（改装检测）标记。房主与客户端均可用。
    /// </summary>
    /// <remarks>
    /// 数据来源随身份不同：房主读本机 GameRoom 中的 Server.Game.Player.IsModded（0.1.21c Server.Game/Player.cs:110）；
    /// 客户端读最近收到的 S_STATE_SNAPSHOT（0.1.21c Server.Game/SnapshotCodec.cs:74 写入全员标记，
    /// PacketHandler.cs:184 存入 SnapshotMirror）。房主每秒把快照广播给全员（迁移期间暂停），所以客户端同样能看到所有人的标记。
    /// 原版界面不显示该字段，本命令把它显式列出。
    ///
    /// 标记由各玩家进房时自报（ModDetector.IsModded，0.1.21c ModDetector.cs:58），房主不核验，
    /// 结果只能作参考，不能当作检测结论。
    /// </remarks>
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
            // 既不是 all 也不是指定 id（无参数/无法解析）→ 帮助
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

        /// <summary>
        /// 解析：all|*|全部 → 全列；#id|id → 指定玩家；其余报错。
        /// </summary>
        /// <param name="args">命令参数，可为 null。</param>
        /// <param name="filterPid">指定的玩家 ID；全列或无参数时为 0。</param>
        /// <param name="listAll">是否列出全部玩家。</param>
        /// <param name="error">解析失败时给出中文原因，成功时为 null。</param>
        /// <returns>解析成功返回 true。无参数也算成功，由调用方输出帮助。</returns>
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
