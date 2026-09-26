using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.DoorLockServer
{
    /// <summary>
    /// Door.HandleEvent 前缀（public override，0.1.15b Server.Game/Door.cs:50-71）：
    /// 原方法体只有锁门一个分支（开关门走 Interact 通道），按 LockDoorMode 放行后
    /// 逐句复刻原版锁门序列（CanSabotage=false → 40s 恢复任务 → S_COOLTIME_SABOTAGE
    /// → StateList[2]=0 → LockDoor → 1s 后 TickDoor 启动自动解锁倒计时）。
    /// 模式未放行的颜色放行原版（Dark 走原版；其余被原版静默拒绝，与未启用一致）。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Door), nameof(Server.Game.Door.HandleEvent))]
    internal static class DoorLockServerHandleEventPatch
    {
        // Player 必须全限定：全局命名空间有客户端 Player 类，裸名会解析错类型
        private static bool Prefix(Server.Game.Door __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<DoorLockServerFeature>())
                return true;

            var mode = DoorLockServerFeature.Mode;
            bool allowed = player.Color == EPlayerColor.Dark
                || (mode == LockDoorMode.Black && player.Color == EPlayerColor.Black)
                || (mode == LockDoorMode.White && player.Color == EPlayerColor.White)
                || mode == LockDoorMode.All;
            if (!allowed)
                return true;    // 原版路径：Dark 正常锁，其余被原版条件拒绝

            if (!player.CanSabotage || __instance.State == 2)
                return false;   // 放行身份但处于冷却/已锁：与原版条件不满足时的静默行为一致

            player.CanSabotage = false;
            player.SabotageCooltimeEndTick = TimeManager.Instance.SurviveTime + 40;
            TimeManager.Instance.PushSurvivalJob(40, delegate
            {
                player.CanSabotage = true;
            });
            player.Session.Send(new S_COOLTIME_SABOTAGE
            {
                Cooltime = 40
            });
            __instance.DeviceInfo.StateList[2] = 0;
            __instance.LockDoor();
            var tickDoor = AccessTools.Method(typeof(Server.Game.Door), "TickDoor");   // private：Door.cs:134
            TimeManager.Instance.PushSurvivalJob(1, delegate
            {
                tickDoor?.Invoke(__instance, null);
            });
            return false;
        }
    }
}
