// 作用：为黑方击杀通报挂载补丁拦截；不负责配置声明与状态存储。
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
