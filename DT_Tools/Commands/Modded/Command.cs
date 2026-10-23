using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.Modded
{
    /// <summary>
    /// /modded [all|#id] — 列出玩家及其 modded（改装检测）标记（仅房主）。
    /// </summary>
    /// <remarks>
    /// 读取房主侧权威数据 Server.Game.Player.IsModded（0.1.21+ 由进房 C_ENTER_GAME.IsModded 写入，
    /// 并经 SnapshotPlayer.IsModded 广播）。原版没有 UI 展示该标记，本命令把它显式列出。
    ///
    /// 版本兼容：IsModded 是 0.1.21 才加到 Server.Game.Player 的属性，编译目标 0.1.20a 的
    /// Assembly-CSharp 中不存在。因此用反射按版本读取（见 ModdedLogic）：旧版本缺属性时
    /// 输出「版本不支持」；0.1.21+ 输出真实标记。无需升级编译目标。
    ///
    /// 语义提示：开了 ModDetectorNeutralize 的 DT_Tools 玩家会向房主上报 false，因此本命令
    /// 主要揭示「未做中和处理」的改装玩家（其他模组 / 作弊），而非 DT_Tools 用户自身。
    /// </remarks>
    internal sealed class ModdedCommand : ICommand
    {
        public string Name => "modded";
        public string[] Aliases => new[] { "mod", "check_mod", "改装" };
        public string Usage => "modded [all|#id]";
        public string Description => "列出所有玩家的 modded（改装检测）标记；all 列出全部，可指定 #id。仅房主。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.PlayerIdentity | FeatureTag.SysNetwork;

        public bool RequireHost => true;

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

            var room = global::Server.Game.GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }
            if (Managers.Player == null)
            {
                ctx.Reply("玩家管理器尚未初始化。");
                return CommandResult.Fail("player manager not ready");
            }

            int myId = Managers.Player.MyPlayerID;
            int pid = listAll ? 0 : filterPid;
            var players = ModdedLogic.Collect(room, pid);

            ctx.Reply(ModdedFormat.Reply(players, myId));
            return CommandResult.Success(ModdedFormat.Result(players, myId));
        }

        /// <summary>
        /// 解析：all|*|全部 → 全列；#id|id → 指定玩家；其余报错。
        /// </summary>
        private static bool TryParse(string[] args, out int filterPid, out bool listAll, out string error)
        {
            filterPid = 0;
            listAll = false;
            error = null;
            if (args == null || args.Length == 0)
                return true; // 无参 → 帮助

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
