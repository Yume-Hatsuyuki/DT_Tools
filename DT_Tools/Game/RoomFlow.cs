using Server.Game;

namespace DT_Tools.Game
{
    public static class RoomFlow
    {
        public static bool IsInRoom => Managers.Network?.GameServer != null;

        public static void LeaveCurrentRoom()
        {
            Managers.Network.Leave();
            Managers.Scene.LoadScene(Define.EScene.LobbyScene);
        }

        public static bool TryGuardBusy(GameRoom room, string migratingAction, out string code, out string text)
        {
            if (room.IsTransitioning)
            {
                code = "transitioning";
                text = "阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。";
                return false;
            }
            if (room.IsMigrating)
            {
                code = "migrating";
                text = $"正在进行主机迁移，无法{migratingAction}。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }
    }
}
