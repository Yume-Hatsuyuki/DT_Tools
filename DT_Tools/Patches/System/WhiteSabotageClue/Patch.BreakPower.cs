using System.Reflection;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.WhiteSabotageClue
{
    /// <summary>
    /// 白方拉电闸留痕：Fusebox.DisconnetCable 后缀（private，字符串定位：
    /// 0.1.16a Server.Game/Fusebox.cs:114）。原版在此处把拉闸人固定记为黑幕
    /// （11037，:127 的 EWhatState.Mastermind），白方拉闸同样被记成"黑幕所为"；
    /// 本补丁在其后追加一条真实身份线索。后缀校验 StateList[0]==9999（成功拉闸
    /// 后置位，:122）——MissionType 不符被原版提前 return 时不误记。
    /// </summary>
    [HarmonyPatch]
    internal static class WhiteBreakPowerCluePatch
    {
        // 方法体里不写 System.Reflection 全限定——Patches/System 目录遮蔽命名空间链上的
        // System 成员（AGENTS §5.3），改用 using 导入的 MethodBase 短名
        private static MethodBase TargetMethod()
            => AccessTools.Method(typeof(Server.Game.Fusebox), "DisconnetCable");

        private static void Postfix(Server.Game.Fusebox __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<WhiteSabotageClueFeature>() || !WhiteSabotageClueFeature.BreakPower)
                return;
            if (player?.Color != EPlayerColor.White)
                return;
            if (__instance.DeviceInfo == null || __instance.DeviceInfo.StateList[0] != 9999)
                return;    // 本次调用没有实际拉闸
            SabotageClue.AddClue(__instance, player);
            Log.Info<WhiteSabotageClueFeature>(
                $"白方拉电闸留痕：{player.Name}(pid={player.PublicInfo.PlayerId}) 设备={__instance.ID} 房间={__instance.RoomID}");
        }
    }
}
