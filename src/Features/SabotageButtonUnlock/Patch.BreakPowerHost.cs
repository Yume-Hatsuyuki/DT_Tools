// 作用：在房主侧放行白方拆电源并复刻断电流程；不处理客户端按钮显示与其他身份的破坏入口。
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    // 0.2.0 服务端 DisconnetCable 只接受 Dark/Black；白方放行时由本前缀复刻原方法体。
    [HarmonyPatch(typeof(Server.Game.Fusebox), "DisconnetCable")]
    internal static class SabotageBreakPowerHostPatch
    {
        private static bool Prefix(Server.Game.Fusebox __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            if (player == null || player.Color != EPlayerColor.White)
                return true;
            if (!SabotageButtonUnlockLogic.ModeAllows(SabotageButtonUnlockFeature.BreakPowerMode, player.Color))
                return true;
            if (__instance.DeviceInfo == null || __instance.DeviceInfo.MissionType != -1)
                return true;

            Traverse.Create(__instance).Property(nameof(Server.Game.Fusebox.IsLight)).SetValue(false);
            Server.Game.AreaManager.Instance.RefreshLight(isDisconnect: true);
            __instance.DeviceInfo.StateList[0] = 9999;
            __instance.DeviceInfo.StateList[2] = 0;
            __instance.DeviceInfo.MissionType = 0;
            __instance.DeviceInfo.IsInfected = false;
            __instance.BroadcastState();
            __instance.RecordSpecialClue(player, 11037);
            Server.Game.GameRoom.Instance.SendSabotageMission(ESchoolMission.ScFusebox, __instance.ID, __instance.DeviceInfo.Pos, isAdd: false);
            if (Server.Game.DeviceManager.Instance.GetDisconnectFuseCount() == 2)
            {
                Server.Game.DeviceManager.Instance.ClearFuseboxSabotage();
                Server.Game.AwardManager.Instance.OnBlackout(player.PublicInfo.PlayerId);
            }

            Log.Info<SabotageButtonUnlockFeature>(
                $"白方拆电源放行：{player.Name}(pid={player.PublicInfo.PlayerId}) 设备={__instance.ID} 房间={__instance.RoomID}");
            return false;
        }
    }
}
