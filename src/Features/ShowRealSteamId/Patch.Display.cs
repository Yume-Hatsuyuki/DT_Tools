// 作用：把举报列表与举报详情中的 SteamID 显示改为完整 SteamID64；不改动记录数据与网络身份。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.ShowRealSteamId
{
    [HarmonyPatch(typeof(GameHistoryStorage.Participant), nameof(GameHistoryStorage.Participant.GetMaskedSteamId))]
    internal static class RealSteamIdDisplayPatch
    {
        private static void Postfix(GameHistoryStorage.Participant __instance, ref string __result)
        {
            if (!Engine.Enabled<ShowRealSteamIdFeature>() || __instance.SteamId == 0L)
                return;
            __result = __instance.SteamId.ToString();
        }
    }
}
