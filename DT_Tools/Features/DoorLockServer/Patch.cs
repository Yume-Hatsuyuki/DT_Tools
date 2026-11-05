using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.DoorLockServer
{
    internal static class DoorLockRules
    {
        // 原版 Door 数据的 StateList[1] 为 20，客户端「已锁（N 秒）」也以此为上限。
        internal const int LockSeconds = 20;
    }

    [HarmonyPatch(typeof(Server.Game.Door), nameof(Server.Game.Door.HandleEvent))]
    internal static class DoorLockServerHandleEventPatch
    {
        private static bool Prefix(Server.Game.Door __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<DoorLockServerFeature>())
                return true;
            if (__instance?.DoorData == null || player == null)
                return true;
            if (__instance.DoorData.Type != EDoorType.NormalDoor)
                return true;
            if (GameRoom.Instance == null || GameRoom.Instance.State != EGameState.Survive)
                return false;

            var mode = DoorLockServerFeature.Mode;
            bool allowed = player.Color == EPlayerColor.Dark
                || (mode == LockDoorMode.Black && player.Color == EPlayerColor.Black)
                || (mode == LockDoorMode.White && player.Color == EPlayerColor.White)
                || mode == LockDoorMode.All;
            if (!allowed)
                return true;

            if (!player.CanSabotage || __instance.State == 2)
                return false;

            player.CanSabotage = false;
            player.SabotageCooltimeEndTick = TimeManager.Instance.SurviveTime + DoorLockServerFeature.Interval;
            if (DoorLockServerFeature.Interval > 0)
            {
                TimeManager.Instance.PushSurvivalJob(DoorLockServerFeature.Interval, delegate
                {
                    player.CanSabotage = true;
                });
            }
            else
            {
                player.CanSabotage = true;
            }

            __instance.LockDoor();
            TimeManager.Instance.PushSurvivalJob(DoorLockRules.LockSeconds, delegate
            {
                __instance.UnlockDoor();
            });
            Log.Info<DoorLockServerFeature>(
                $"锁门：{player.Name}(pid={player.PublicInfo.PlayerId},{player.Color}) 门={__instance.ID} 房间={__instance.RoomID}");
            return false;
        }
    }

    // 迁移恢复后门仍是 State=2，但生存计时任务已清空，需要重新排一次自动解锁。
    [HarmonyPatch(typeof(Server.Game.Device), nameof(Server.Game.Device.OnMigrationResume))]
    internal static class DoorLockServerMigrationPatch
    {
        private static void Postfix(Server.Game.Device __instance)
        {
            if (!Engine.Enabled<DoorLockServerFeature>())
                return;
            if (!(__instance is Server.Game.Door door) || door.State != 2)
                return;

            TimeManager.Instance.PushSurvivalJob(DoorLockRules.LockSeconds, delegate
            {
                door.UnlockDoor();
            });
        }
    }
}
