using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.BlackKillNotify
{
    /// <summary>
    /// MyPlayer.Modify 后缀（public，0.1.16b MyPlayer.cs:941）：仅 RemainKill 类型包
    /// 进入检测。RemainKill 由服务端经 S_MODIFY_MY_PLAYER 只发攻击者本人
    /// （SendRemainKill → Session.Send，Server.Game/Player.cs:1834-1843），客户端在
    /// Modify 的 switch 里落到 Managers.Game.RemainKill（MyPlayer.cs:974-976）；
    /// 后缀读到的即主机接受击杀后的剩余值，直接作为通报文案的次数。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.Modify))]
    internal static class BlackKillNotifyRemainKillPatch
    {
        private static void Postfix(S_MODIFY_MY_PLAYER pkt)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>() || !BlackKillNotifyFeature.ClientSide)
                return;
            if (pkt == null || pkt.Type != EModifyMyPlayerEvent.RemainKill)
                return;
            BlackKillNotifyLogic.OnRemainKillApplied();
        }
    }
}
