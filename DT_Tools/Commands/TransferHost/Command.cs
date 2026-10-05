using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.TransferHost
{
    /// <summary>
    /// /transferhost #&lt;playerId&gt; — 将房主身份转让给指定玩家（大厅与局内均可，需房主）。
    ///
    /// 原版房主身份只在房主退出房间时自动转移（GameRoom.HandleLeavePlayer 内
    /// Host = Players.FirstOrDefault()，且仅 Lobby 广播 S_SET_HOST）。
    /// 本命令允许房主主动交出房主身份：服务端 GameRoom.Host 切换为目标玩家，
    /// 并向全员广播 S_SET_HOST（所有阶段）。转让后：
    ///   - 目标玩家获得房主权限（开局判定 / 踢人 / 后续再转让），经服务端
    ///     Host 字段校验即刻生效；
    ///   - 原房主留在房间继续游戏；客户端房主标记的局内同步由
    ///     Patches/System/HostTransfer 补丁负责（需开启 [HostTransfer]）；
    ///   - 原房主之后退出房间时按原版机制自动迁移给新 Host，房间不解散。
    /// 注意：P2P 中继拓扑不变（Steam 大厅 owner 仍为原房主，退出时 Steam 自动
    /// 移交大厅 owner，游戏内走既有掉线迁移接管），本命令只做逻辑房主转让。
    /// </summary>
    internal sealed class TransferHostCommand : ICommand
    {
        public string Name => "transferhost";
        public string[] Aliases => new[] { "host", "转让" };
        public string Usage => "transferhost #<playerId>";
        public string Description => "将房主身份转让给指定玩家（大厅与局内均可，需房主；原房主留在房间）。";
        public string Author => "合理";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /transferhost #<playerId>\n可用 /list_players 查看玩家列表。");
                return CommandResult.Fail("missing target");
            }
            if (!TargetSpec.TryParseId(ctx.Args[0], out int targetId))
            {
                ctx.Reply($"无效的玩家 ID: {ctx.Args[0]}（应为 #数字 或纯数字）");
                return CommandResult.Fail("invalid target");
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }

            var host = room.Host;
            if (host?.PublicInfo == null)
            {
                ctx.Reply("无法获取房主玩家对象。");
                return CommandResult.Fail("host missing");
            }
            if (host.PublicInfo.PlayerId == targetId)
            {
                ctx.Reply("不能转让给自己。");
                return CommandResult.Fail("self transfer");
            }

            var target = PlayerQuery.FindById(room, targetId);
            if (target == null)
            {
                ctx.Reply($"找不到 PlayerId={targetId} 的玩家。");
                return CommandResult.Fail("target not found");
            }
            if (target.IsSpectator)
            {
                ctx.Reply("不能转让给观战者。");
                return CommandResult.Fail("spectator target");
            }
            if (target.IsDummy)
            {
                ctx.Reply("不能转让给分身。");
                return CommandResult.Fail("dummy target");
            }

            // 客户端局内房主标记同步依赖 [HostTransfer] 功能（默认关闭），提示但不强拦：
            // 服务端权限（开局/踢人/再转让）不依赖该功能开关。
            if (!Engine.EnabledOf(typeof(DT_Tools.Patches.System.HostTransfer.HostTransferFeature)))
                ctx.Warn("提示: 客户端 [HostTransfer] 功能未开启，局内房主标记不会同步（服务端权限转让已生效）。");

            room.Host = target;
            var hostPkt = new S_SET_HOST { HostId = target.PublicInfo.PlayerId };
            int sent = 0;
            foreach (var p in room.Players)
            {
                if (p?.Session != null)
                {
                    p.Session.Send(hostPkt);
                    sent++;
                }
            }
            ctx.Reply($"房主已转让给 {target.Name}（#{target.PublicInfo.PlayerId}），已通知 {sent} 名玩家，阶段={room.State}。");
            return CommandResult.Success($"transferred to {targetId}");
        }
    }
}
