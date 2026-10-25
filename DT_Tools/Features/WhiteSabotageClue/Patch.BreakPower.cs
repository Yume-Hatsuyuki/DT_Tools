using System.Reflection;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.WhiteSabotageClue
{
    [HarmonyPatch]
    internal static class WhiteBreakPowerCluePatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(Server.Game.Fusebox), "DisconnetCable");

        private static void Postfix(Server.Game.Fusebox __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<WhiteSabotageClueFeature>() || !WhiteSabotageClueFeature.BreakPower)
                return;
            if (player?.Color != EPlayerColor.White)
                return;
            if (__instance.DeviceInfo == null || __instance.DeviceInfo.StateList[0] != 9999)
                return;
            SabotageClue.AddClue(__instance, player);
            Log.Info<WhiteSabotageClueFeature>(
                $"白方拉电闸留痕：{player.Name}(pid={player.PublicInfo.PlayerId}) 设备={__instance.ID} 房间={__instance.RoomID}");
        }
    }
}
