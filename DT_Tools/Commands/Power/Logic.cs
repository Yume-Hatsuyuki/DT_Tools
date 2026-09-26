using System.Linq;
using DT_Tools.Commands;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Power
{
    /// <summary>
    /// /blackout、/restore_power 共用业务（对应旧 PowerControlHelper；房主门禁由框架统一执行）。
    ///
    /// 原版机制：
    ///   - Dark 在破坏任务电箱（Fusebox.DeviceInfo.MissionType == -1）剪完全部线后，
    ///     C_INTERACT_FUSEBOX → DeviceManager.Interact → Fusebox.Interact（0.1.15b
    ///     Server.Game/Fusebox.cs:16）→ DisconnetCable（:114）。
    ///   - 断开第 2 个电箱时 AreaManager.RefreshLight 把所有 Area.IsLight 置 false，
    ///     Area.IsLight setter 向区域内每名玩家单播 S_AREA_PUBLIC，
    ///     客户端 Managers.Game.Darkness=true：关全局灯、中断读条、只剩玩家身边小聚光灯。
    ///   - 修复走 Fusebox.ConnetCable（:97）；DeviceManager.RefuseAllFuse（0.1.15b
    ///     Server.Game/DeviceManager.cs:308）会重连全部已断开电箱，
    ///     计数归 0 时全区亮灯、播放 FuseOnSfx，并安排 60 秒后重新发放 3 个破坏任务。
    /// </summary>
    internal static class PowerLogic
    {
        /// <summary>触发全场停电所需的断开电箱数量（与 AreaManager.RefreshLight 阈值一致）。</summary>
        private const int RequiredDisconnectedFuses = 2;

        /// <summary>
        /// 活动房间 / 屏障 / 生存阶段统一校验，失败返回 null 并已输出提示。
        /// 电力逻辑只存在于 Host 端，且仅在生存阶段有游戏意义。
        /// </summary>
        private static GameRoom Validate(CommandContext ctx)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return null;
            }
            if (room.IsTransitioning)
            {
                ctx.Reply("阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。");
                return null;
            }
            if (room.IsMigrating)
            {
                ctx.Reply("正在进行主机迁移，无法操作电力。");
                return null;
            }
            if (room.State != EGameState.Survive)
            {
                ctx.Reply($"电力命令仅能在生存阶段（Survive）使用，当前状态: {room.State}。");
                return null;
            }
            return room;
        }

        /// <summary>
        /// 触发全场停电。复用 Dark 剪线的原版完整路径
        /// （Fusebox.Interact → DisconnetCable → AreaManager.RefreshLight），
        /// 断开第 2 个电箱时全体区域灭灯并广播 FuseOffSfx / 电箱指路箭头。
        /// </summary>
        public static CommandResult Blackout(CommandContext ctx)
        {
            var room = Validate(ctx);
            if (room == null) return CommandResult.Fail("invalid state");

            // 用全名限定：程序集里另有客户端 DeviceManager（Managers.Device，全局命名空间），
            // 这里需要的是 Host 端 Server.Game.DeviceManager（0.1.15b Server.Game/DeviceManager.cs:10/:38）
            var deviceManager = Server.Game.DeviceManager.Instance;
            int disconnected = deviceManager.GetDisconnectFuseCount();
            if (disconnected >= RequiredDisconnectedFuses)
            {
                ctx.Reply($"当前已经处于停电状态（已断开电箱 {disconnected} 个），可用 /restore_power 恢复电力。");
                return CommandResult.Fail("already blackout", new { disconnected });
            }

            int need = RequiredDisconnectedFuses - disconnected;

            // 仅完好（IsLight）的电箱可断开；优先使用已发放破坏任务（MissionType==-1）的，
            // 与 Dark 手动剪线完全同路径；任务已被清空（如修复后 60 秒刷新间隔内）时，
            // 先按原版 StartMission 路径补发任务再断开。
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

                // player=null：DisconnetCable 内仅在 player!=null 时记录成就，
                // Device.RecordLastUsingPlayer 亦有 player!=null 保护；Packet 只需非空实例。
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

        /// <summary>
        /// 恢复电力。直接复用原版 DeviceManager.RefuseAllFuse：
        /// 重连全部已断开电箱，计数归 0 时全区亮灯、播放 FuseOnSfx，
        /// 并安排 60 秒后重新随机发放 3 个破坏任务。
        /// </summary>
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

            // 原版公共方法：遍历全部 IsLight==false 的电箱执行 ConnetCable
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
