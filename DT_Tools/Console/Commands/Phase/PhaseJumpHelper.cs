using BepInEx.Logging;
using DT_Tools.Patches.System;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands.Phase
{
    /// <summary>
    /// /enter_detective、/enter_trial 共用：房主端强制跳转游戏阶段（EGameState）。
    ///
    /// 原版阶段流转入口（0.1.14b）：
    ///   Survive → Detective  仅由 Corpse.EndSurvival() 触发
    ///                         （报告尸体 / 尸体生成 50~70 秒超时 / 生存时间结束·任务全清兜底）
    ///   Detective → Trial    仅由 GameRoom.DetectiveTick() 调查倒计时归零触发
    /// 两者最终都走 GameRoom.ChangeGameState：先改 State，再广播 S_CHANGE_GAME_STATE，
    /// 等全体客户端回 C_COMPLETE_PACKET 后执行 EndState/StartState。
    /// </summary>
    internal static class PhaseJumpHelper
    {
        /// <summary>校验房主与活动房间，失败返回 null 并已输出提示。</summary>
        public static GameRoom ValidateRoom(WebConsole console)
        {
            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行（阶段切换逻辑仅在 Host 端）。", LogLevel.Warning);
                return null;
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                console.Log("当前没有活动的游戏房间。", LogLevel.Warning);
                return null;
            }

            return room;
        }

        /// <summary>阶段切换屏障 / Host 迁移进行中时拒绝再次跳转。</summary>
        public static bool EnsureNotBusy(GameRoom room, WebConsole console)
        {
            if (room.IsTransitioning)
            {
                console.Log("阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。", LogLevel.Warning);
                return false;
            }

            if (room.IsMigrating)
            {
                console.Log("正在进行主机迁移，无法切换阶段。", LogLevel.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 强制进入调查阶段（Detective）。仅允许从生存阶段（Survive）进入。
        ///
        /// 存在未处理尸体时走原版完整路径 Corpse.DiscoverByTimeOver()（与生存倒计时
        /// 归零完全一致：固化取证、浮出隐藏尸体、TrialManager.Init、切换状态）；
        /// 无尸体时直接 ChangeGameState(Detective)——不初始化任何凶手，依赖
        /// [StartDetective] 补丁修复原版 StartDetective 开头 black.IsAlive 的空引用
        /// （正常后续逻辑全部 null 安全）。无尸体/无凶手时本次裁判没有录像带，
        /// 投票无人可投，最终按“凶手未被捕获”结算，仅供单人/调试使用。
        /// </summary>
        public static void JumpToDetective(WebConsole console)
        {
            var room = ValidateRoom(console);
            if (room == null) return;
            if (!EnsureNotBusy(room, console)) return;

            switch (room.State)
            {
                case EGameState.Detective:
                    console.Log("当前已经处于调查阶段（Detective）。", LogLevel.Warning);
                    return;
                case EGameState.Survive:
                    break;
                default:
                    console.Log($"只能从生存阶段（Survive）进入调查阶段，当前状态: {room.State}。", LogLevel.Warning);
                    return;
            }

            // 原版路径：最老的未发现尸体“超时发现”，内部会再次校验 State == Survive
            var corpse = room.FindOldestUndiscoveredCorpse();
            if (corpse != null)
            {
                console.Log(
                    $"存在未处理尸体（#{corpse.DeviceInfo.DeviceId}），按原版“时间到发现尸体”流程进入调查阶段……",
                    LogLevel.Message);
                corpse.DiscoverByTimeOver();
                console.Log("已触发调查阶段切换（等待全体客户端加载完成）。", LogLevel.Message);
                return;
            }

            // 无尸体裸进：原版 StartDetective 开头无 null 保护，必须由补丁修复
            if (!Patch_StartDetective.IsApplied)
            {
                console.Log(
                    "场上没有未处理尸体，直接进入调查阶段需要启用补丁 [StartDetective] " +
                    "（配置文件中 [StartDetective].Enabled=true，默认开启，修改后重启游戏）。",
                    LogLevel.Warning);
                return;
            }

            console.Log("场上没有未处理尸体，将不设置凶手直接进入调查阶段（无录像带，投票将以凶手未被捕获结算）。", LogLevel.Warning);
            room.ChangeGameState(EGameState.Detective);
            console.Log("已强制进入调查阶段（Detective，等待全体客户端加载完成）。", LogLevel.Message);
        }

        /// <summary>
        /// 强制进入学级裁判（Trial）。
        /// 调查阶段进入 = 跳过剩余调查时间（与倒计时归零等效）；
        /// 生存阶段进入 = 连调查阶段一并跳过。
        /// 原版裁判链路（StartTrial → 讨论/投票/开票 → FinalizeTrialResult）
        /// 对 Black==null 全部有保护且内置了“无凶手按未捕获判黑方胜”的分支，
        /// 因此无需任何凶手数据即可直接切换。
        /// </summary>
        public static void JumpToTrial(WebConsole console)
        {
            var room = ValidateRoom(console);
            if (room == null) return;
            if (!EnsureNotBusy(room, console)) return;

            switch (room.State)
            {
                case EGameState.Trial:
                    console.Log("当前已经处于学级裁判（Trial）。", LogLevel.Warning);
                    return;

                case EGameState.Detective:
                    break;

                case EGameState.Survive:
                    console.Log("将从生存阶段直接进入学级裁判（跳过调查阶段，无尸体/录像带，投票将以凶手未被捕获结算）。", LogLevel.Warning);
                    break;

                default:
                    console.Log(
                        $"只能从调查阶段（Detective）或生存阶段（Survive）进入学级裁判，当前状态: {room.State}。",
                        LogLevel.Warning);
                    return;
            }

            room.ChangeGameState(EGameState.Trial);
            console.Log("已触发学级裁判切换（等待全体客户端加载完成）。", LogLevel.Message);
        }
    }
}
