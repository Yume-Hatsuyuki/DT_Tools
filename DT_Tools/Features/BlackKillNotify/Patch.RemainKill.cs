using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.BlackKillNotify
{
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
