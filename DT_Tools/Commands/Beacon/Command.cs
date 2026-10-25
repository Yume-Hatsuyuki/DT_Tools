using DT_Tools.Core.Attributes;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.Beacon
{
    internal sealed class BeaconCommand : ICommand
    {
        public string Name => "beacon";
        public string[] Aliases => new[] { "战略信标", "信标", "teleport", "tp", "瞬移" };
        public string Usage => "beacon [x y | spawn <#i> | player <#id> | lobby | error]";
        public string Description => "战略信标：瞬移到指定坐标。掉皮掉肉不掉队！（跟随控制台 / 客户端）";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.PlayerMove;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
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

            if (ctx.Args.Length == 0)
            {
                ctx.Reply(BeaconFormat.HelpAndSpawns(mapData, my));
                return CommandResult.Success(BeaconFormat.ListResult(mapData, my));
            }

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

            if (my.State == EPlayerState.Hide || my.State == EPlayerState.Sit)
            {
                ctx.Warn($"当前状态 {my.State} 无法瞬移（服务端 HandleMove 拒收 Hide/Sit）。请先脱离藏身处/座位。");
                return CommandResult.Fail("state locked");
            }

            string oob = BeaconLogic.OutOfBoundsWarning(pos, mapData);
            if (oob != null)
                ctx.Warn(oob);

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
