using System.Linq;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands.Power
{
    /// <summary>
    /// /blackout、/restore_power 共用：房主端全场电力操作。
    ///
    /// 原版机制（0.1.14b）：
    ///   - Dark 在破坏任务电箱（Fusebox.DeviceInfo.MissionType == -1）剪完全部线后，
    ///     C_INTERACT_FUSEBOX → DeviceManager.Interact → Fusebox.Interact → DisconnetCable。
    ///   - 断开第 2 个电箱时 AreaManager.RefreshLight 把所有 Area.IsLight 置 false，
    ///     Area.IsLight setter 向区域内每名玩家单播 S_AREA_PUBLIC，
    ///     客户端 Managers.Game.Darkness=true：关全局灯、中断读条、只剩玩家身边小聚光灯。
    ///   - 修复走 Fusebox.ConnetCable；DeviceManager.RefuseAllFuse 会重连全部已断开电箱，
    ///     计数归 0 时全区亮灯、播放 FuseOnSfx，并安排 60 秒后重新发放 3 个破坏任务。
    /// </summary>
    internal static class PowerControlHelper
    {
        /// <summary>触发全场停电所需的断开电箱数量（与 AreaManager.RefreshLight 阈值一致）。</summary>
        private const int RequiredDisconnectedFuses = 2;

        /// <summary>
        /// 房主 / 活动房间 / 屏障 / 生存阶段统一校验，失败返回 null 并已输出提示。
        /// 电力逻辑只存在于 Host 端，且仅在生存阶段有游戏意义。
        /// </summary>
        public static GameRoom Validate(WebConsole console)
        {
            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行（电力逻辑仅在 Host 端）。", LogLevel.Warning);
                return null;
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                console.Log("当前没有活动的游戏房间。", LogLevel.Warning);
                return null;
            }

            if (room.IsTransitioning)
            {
                console.Log("阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。", LogLevel.Warning);
                return null;
            }

            if (room.IsMigrating)
            {
                console.Log("正在进行主机迁移，无法操作电力。", LogLevel.Warning);
                return null;
            }

            if (room.State != EGameState.Survive)
            {
                console.Log($"电力命令仅能在生存阶段（Survive）使用，当前状态: {room.State}。", LogLevel.Warning);
                return null;
            }

            return room;
        }

        /// <summary>
        /// 触发全场停电。复用 Dark 剪线的原版完整路径
        /// （Fusebox.Interact → DisconnetCable → AreaManager.RefreshLight），
        /// 断开第 2 个电箱时全体区域灭灯并广播 FuseOffSfx / 电箱指路箭头。
        /// </summary>
        public static void Blackout(WebConsole console)
        {
            var room = Validate(console);
            if (room == null) return;

            // 用全名限定：客户端存在全局命名空间的 DeviceManager（Managers.Device），
            // 这里需要的是 Host 端 Server.Game.DeviceManager
            var deviceManager = Server.Game.DeviceManager.Instance;
            int disconnected = deviceManager.GetDisconnectFuseCount();
            if (disconnected >= RequiredDisconnectedFuses)
            {
                console.Log($"当前已经处于停电状态（已断开电箱 {disconnected} 个），可用 /restore_power 恢复电力。", LogLevel.Warning);
                return;
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
                console.Log(
                    $"可断开的电箱不足（还需 {need} 个，实际 {targets.Count} 个），无法触发停电。",
                    LogLevel.Warning);
                return;
            }

            foreach (var fusebox in targets)
            {
                if (fusebox.DeviceInfo.MissionType != -1)
                {
                    fusebox.StartMission();
                }

                // player=null：DisconnetCable 内仅在 player!=null 时记录成就，
                // Device.RecordLastUsingPlayer 亦有 player!=null 保护；Packet 只需非空实例。
                fusebox.Interact(null, new Packet());
            }

            int after = deviceManager.GetDisconnectFuseCount();
            if (after >= RequiredDisconnectedFuses)
            {
                console.Log(
                    "已触发全场停电（与断开第 2 个电箱同效果）：全体区域灭灯、中断读条、" +
                    "仅保留玩家身边灯光；可用 /restore_power 恢复。",
                    LogLevel.Message);
            }
            else
            {
                console.Log($"停电未触发：当前断开电箱 {after} 个（需要 {RequiredDisconnectedFuses} 个）。", LogLevel.Warning);
            }
        }

        /// <summary>
        /// 恢复电力。直接复用原版 DeviceManager.RefuseAllFuse：
        /// 重连全部已断开电箱，计数归 0 时全区亮灯、播放 FuseOnSfx，
        /// 并安排 60 秒后重新随机发放 3 个破坏任务。
        /// </summary>
        public static void RestorePower(WebConsole console)
        {
            var room = Validate(console);
            if (room == null) return;

            var deviceManager = Server.Game.DeviceManager.Instance;
            int disconnected = deviceManager.GetDisconnectFuseCount();
            if (disconnected == 0)
            {
                console.Log("当前电力正常，没有已断开的电箱。", LogLevel.Warning);
                return;
            }

            // 原版公共方法：遍历全部 IsLight==false 的电箱执行 ConnetCable
            deviceManager.RefuseAllFuse();

            int after = deviceManager.GetDisconnectFuseCount();
            if (after == 0)
            {
                console.Log("电力已恢复：全体区域亮灯，60 秒后将重新随机发放电箱破坏任务。", LogLevel.Message);
            }
            else
            {
                console.Log($"仍有 {after} 个电箱未重连（可能正在被修复读条占用），请稍后重试。", LogLevel.Warning);
            }
        }
    }
}
