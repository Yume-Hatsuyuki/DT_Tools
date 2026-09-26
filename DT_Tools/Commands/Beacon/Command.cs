using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.Beacon
{
    /// <summary>
    /// /beacon [x y | spawn &lt;#i&gt; | player &lt;#id&gt; | lobby | error] — 战略信标：瞬移到指定坐标。
    /// 掉皮掉肉不掉队！（跟随控制台 / 客户端，非房主可用，仅作用于本机）
    ///
    /// 机制（按《分析MOD客户端功能数据包》第 8 节"传送"伪造路径）：服务端
    /// GameRoom.HandleMove → Player.Move(pos) 全程无距离校验，仅拒绝 Hide/Sit/MoveLock，
    /// 位置非法时仅在 Survive/Detective 阶段 fallback 到 MapData.ErrorPos（force respawn），
    /// 其余阶段整包静默丢弃
    /// （见 0.1.15b Server.Game/GameRoom.cs:2321 HandleMove、:2349-2354 越界分流、
    /// Server.Game/Player.cs:733 Move）。
    /// 客户端 PlayerManager.HandleMove 显式忽略本机 MyPlayer（仅同步他人，S_MOVE 不会回拉本机坐标，
    /// 见 0.1.15b PlayerManager.cs:457 HandleMove、459 的 MyPlayer 判断），故需本地主动落位——
    /// 统一走 Game/Teleport.TryTeleport（本地落位 + C_MOVE + 相机跟随，与官方 HandleRespawn 同款）。
    /// </summary>
    internal sealed class BeaconCommand : ICommand
    {
        public string Name => "beacon";
        public string[] Aliases => new[] { "战略信标", "信标", "teleport", "tp", "瞬移" };
        public string Usage => "beacon [x y | spawn <#i> | player <#id> | lobby | error]";
        public string Description => "战略信标：瞬移到指定坐标。掉皮掉肉不掉队！（跟随控制台 / 客户端）";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            // 1. 校验本机玩家 + 网络链路
            if (!LocalPlayer.TryGetPlayer(out var my, out string localError))
            {
                ctx.Reply(localError);
                return CommandResult.Fail("not in game");
            }

            var mapData = Managers.Data?.MapData;
            if (mapData == null)
            {
                ctx.Reply("地图数据未加载（MapData 为空）。");
                return CommandResult.Fail("no mapdata");
            }

            // 2. 无参数 → 列出出生点 / 在线玩家 + 帮助
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(BeaconFormat.HelpAndSpawns(mapData, my));
                return CommandResult.Success(BeaconFormat.ListResult(mapData, my));
            }

            // 3. 解析目标坐标
            if (!BeaconArgs.TryParse(ctx.Args, out var target, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("bad args");
            }
            if (!BeaconLogic.ResolveTarget(target, mapData, my, out var pos, out string label,
                    out int targetPlayerId, out string resolveError))
            {
                ctx.Reply(resolveError);
                return CommandResult.Fail("bad args");
            }

            // 4. 状态门禁：Hide/Sit 服务端会拒收 C_MOVE
            if (my.State == EPlayerState.Hide || my.State == EPlayerState.Sit)
            {
                ctx.Warn($"当前状态 {my.State} 无法瞬移（服务端 HandleMove 拒收 Hide/Sit）。请先脱离藏身处/座位。");
                return CommandResult.Fail("state locked");
            }

            // 5. 越界预警（不阻断：仅生存/调查阶段服务端兜底 ErrorPos，其余阶段丢弃移动）
            string oob = BeaconLogic.OutOfBoundsWarning(pos, mapData);
            if (oob != null)
                ctx.Warn(oob);

            // 6. 瞬移（本地落位 + C_MOVE + 相机，与官方 HandleRespawn 同款）
            if (!Teleport.TryTeleport(my, pos, out string teleportError))
            {
                ctx.Warn($"瞬移失败: {teleportError}");
                return CommandResult.Fail("teleport failed");
            }

            var roomLabel = RoomLabel.FromPos(pos);
            ctx.Reply($"[战略信标] 已瞬移到 {label} → ({pos.X:F0}, {pos.Y:F0})  [{roomLabel.localized}]。掉皮掉肉不掉队！");
            return CommandResult.Success(BeaconFormat.TeleportResult(pos, roomLabel.raw, targetPlayerId));
        }
    }
}
