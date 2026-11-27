// 作用：为大厅屏幕视频挂载补丁拦截；不负责配置声明与状态存储。
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
