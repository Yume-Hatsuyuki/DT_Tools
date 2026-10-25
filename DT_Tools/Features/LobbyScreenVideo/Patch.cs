using HarmonyLib;

namespace DT_Tools.Features.LobbyScreenVideo
{
    [HarmonyPatch(typeof(UI_ScoreBoard), nameof(UI_ScoreBoard.SetInfo))]
    internal static class LobbyScreenVideoScoreBoardPatch
    {
        private static void Postfix(UI_ScoreBoard __instance)
        {
            LobbyScreenVideoLogic.OnScoreBoardRefreshed(__instance);
        }
    }
}
