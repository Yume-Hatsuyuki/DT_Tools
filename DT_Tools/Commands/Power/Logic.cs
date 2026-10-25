using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Power
{
    internal static class PowerLogic
    {
        private const int RequiredDisconnectedFuses = 2;

        private static GameRoom Validate(CommandContext ctx)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return null;
            }
            if (!RoomFlow.TryGuardBusy(room, "操作电力", out _, out string busyText))
            {
                ctx.Reply(busyText);
                return null;
            }
            if (room.State != EGameState.Survive)
            {
                ctx.Reply($"电力命令仅能在生存阶段（Survive）使用，当前状态: {room.State}。");
                return null;
            }
            return room;
        }

        public static CommandResult Blackout(CommandContext ctx)
        {
            var room = Validate(ctx);
            if (room == null) return CommandResult.Fail("invalid state");

            var deviceManager = Server.Game.DeviceManager.Instance;
            int disconnected = deviceManager.GetDisconnectFuseCount();
            if (disconnected >= RequiredDisconnectedFuses)
            {
                ctx.Reply($"当前已经处于停电状态（已断开电箱 {disconnected} 个），可用 /restore_power 恢复电力。");
                return CommandResult.Fail("already blackout", new { disconnected });
            }

            int need = RequiredDisconnectedFuses - disconnected;

            var lightFuses = deviceManager.Fuseboxes.Where(f => f.IsLight).ToList();
            var targets = lightFuses
                .Where(f => f.DeviceInfo.MissionType == -1)
                .Concat(lightFuses.Where(f => f.DeviceInfo.MissionType != -1))
                .Take(need)
                .ToList();

            if (targets.Count < need)
            {
                ctx.Reply(
                    $"可断开的电箱不足（还需 {need} 个，实际 {targets.Count} 个），无法触发停电。");
                return CommandResult.Fail("not enough fuseboxes", new { need, available = targets.Count });
            }

            foreach (var fusebox in targets)
            {
                if (fusebox.DeviceInfo.MissionType != -1)
                    fusebox.StartMission();

                fusebox.Interact(null, new global::Packet());
            }

            int after = deviceManager.GetDisconnectFuseCount();
            if (after >= RequiredDisconnectedFuses)
            {
                ctx.Reply(
                    "已触发全场停电（与断开第 2 个电箱同效果）：全体区域灭灯、中断读条、" +
                    "仅保留玩家身边灯光；可用 /restore_power 恢复。");
                return CommandResult.Success(new { disconnected = after });
            }

            ctx.Warn($"停电未触发：当前断开电箱 {after} 个（需要 {RequiredDisconnectedFuses} 个）。");
            return CommandResult.Fail("blackout not triggered", new { disconnected = after });
        }

        public static CommandResult RestorePower(CommandContext ctx)
        {
            var room = Validate(ctx);
            if (room == null) return CommandResult.Fail("invalid state");

            var deviceManager = Server.Game.DeviceManager.Instance;
            int disconnected = deviceManager.GetDisconnectFuseCount();
            if (disconnected == 0)
            {
                ctx.Reply("当前电力正常，没有已断开的电箱。");
                return CommandResult.Fail("power is on", new { disconnected = 0 });
            }

            deviceManager.RefuseAllFuse();

            int after = deviceManager.GetDisconnectFuseCount();
            if (after == 0)
            {
                ctx.Reply("电力已恢复：全体区域亮灯，60 秒后将重新随机发放电箱破坏任务。");
                return CommandResult.Success(new { disconnected = 0 });
            }

            ctx.Warn($"仍有 {after} 个电箱未重连（可能正在被修复读条占用），请稍后重试。");
            return CommandResult.Fail("still disconnected", new { disconnected = after });
        }
    }
}
