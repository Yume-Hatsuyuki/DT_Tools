using Server.Game;

namespace DT_Tools.Game
{
    /// <summary>
    /// 房间流转助手（Game 层）：Join/ExitRoom 两个命令域共用的"是否在房/退出回大厅"行为。
    /// </summary>
    /// <remarks>
    /// IsInRoom：NetworkManager.InRoom 为 private（0.1.18c NetworkManager.cs:167，判
    /// GameServer != null），GameServer 公开读（NetworkManager.cs:130）。
    /// LeaveCurrentRoom：与原版退出确认 OnClickExitYes 同路径（0.1.18c UI_GameScene.cs:2221-2227）。
    /// </remarks>
    public static class RoomFlow
    {
        public static bool IsInRoom => Managers.Network?.GameServer != null;

        public static void LeaveCurrentRoom()
        {
            Managers.Network.Leave();                              // 0.1.18c NetworkManager.cs:1312
            Managers.Scene.LoadScene(Define.EScene.LobbyScene);    // 0.1.18c SceneManagerEx.cs:9
        }

        /// <summary>
        /// 「切换/迁移中」屏障守卫：阶段切换（等待全体客户端加载）或主机迁移进行中时拒绝。
        /// </summary>
        /// <remarks>
        /// 五处命令域逐字相同的守卫上浮于此（AGENTS.md §4：≥2 处才上浮）；
        /// <paramref name="migratingAction"/> 拼接迁移中文案的动作词（如「切换阶段」「执行处决」），
        /// 保持各域原有提示不变。
        /// </remarks>
        /// <param name="room">当前房间。</param>
        /// <param name="migratingAction">要执行的动作名，用于拒绝时的提示。</param>
        /// <param name="code">拒绝时的错误码。</param>
        /// <param name="text">拒绝时的提示文字。</param>
        /// <returns>可以继续时为 true；切换或迁移中为 false。</returns>
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
