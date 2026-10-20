using HarmonyLib;

namespace DT_Tools.Features.LobbyScreenVideo
{
    /// <summary>
    /// 大厅屏幕（UI_ScoreBoard.SetInfo）刷新后确保视频覆盖层已挂载。
    /// 进大厅/回大厅/玩家进出都会触发原版 S_SCORE_BOARD → SetInfo（0.1.18c
    /// GameRoom.StartLobby / PacketHandler.Handle_S_SCORE_BOARD），
    /// 覆盖层随之创建/复用；实际显示由 LobbyVideoController 按大厅状态裁决。
    /// </summary>
    [HarmonyPatch(typeof(UI_ScoreBoard), nameof(UI_ScoreBoard.SetInfo))]
    internal static class LobbyScreenVideoScoreBoardPatch
    {
        private static void Postfix(UI_ScoreBoard __instance)
        {
            LobbyScreenVideoLogic.OnScoreBoardRefreshed(__instance);
        }
    }
}
