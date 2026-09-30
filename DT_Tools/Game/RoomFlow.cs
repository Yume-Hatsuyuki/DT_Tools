namespace DT_Tools.Game
{
    /// <summary>
    /// 房间流转助手（Game 层）：Join/ExitRoom 两个命令域共用的"是否在房/退出回大厅"行为。
    /// IsInRoom：NetworkManager.InRoom 为 private（0.1.15b NetworkManager.cs:165，判
    /// GameServer != null），GameServer 公开读（NetworkManager.cs:130）。
    /// LeaveCurrentRoom：与原版退出确认 OnClickExitYes 同路径（0.1.15b UI_GameScene.cs:2302-2308）。
    /// </summary>
    public static class RoomFlow
    {
        public static bool IsInRoom => Managers.Network?.GameServer != null;

        public static void LeaveCurrentRoom()
        {
            Managers.Network.Leave();                              // 0.1.15b NetworkManager.cs:1305
            Managers.Scene.LoadScene(Define.EScene.LobbyScene);    // 0.1.15b SceneManagerEx.cs:9
        }
    }
}
