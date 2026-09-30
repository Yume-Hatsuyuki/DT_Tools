using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GameState
{
    /// <summary>
    /// /game_state 业务：读取游戏主状态机。房主与客户端数据来源不同：
    /// 房主走 GameRoom.Instance.State（服务器权威）+ TrialManager.Instance.State，
    /// 并附带 IsTransitioning / IsMigrating 屏障标志；客户端走 Managers.Game.State
    /// （由 S_CHANGE_GAME_STATE 同步）+ TrialMirror（由 S_TRIAL_STATE 镜像，
    /// 0.1.15b Server.Game/TrialMirror.cs:14-22、PacketHandler.cs:1021-1027）。
    /// </summary>
    internal static class GameStateLogic
    {
        /// <summary>读取结果（供 Format 输出）。</summary>
        public sealed class ReadResult
        {
            public bool IsHost;
            public EGameState State;
            public bool Transitioning;
            public bool Migrating;
            public ETrialState? TrialState;
        }

        public static bool TryRead(out ReadResult result, out string code, out string text)
        {
            result = new ReadResult { IsHost = HostGuard.IsHost };

            if (result.IsHost)
            {
                var room = GameRoom.Instance;
                if (room == null)
                {
                    code = "no room";
                    text = "当前没有活动的游戏房间（尚未创建/加入房间）。";
                    return false;
                }

                result.State = room.State;
                result.Transitioning = room.IsTransitioning;
                result.Migrating = room.IsMigrating;

                // 裁判子状态以服务器 TrialManager 为准（0.1.15b Server.Game/TrialManager.cs:81/:93）
                if (result.State == EGameState.Trial && TrialManager.Instance != null)
                    result.TrialState = TrialManager.Instance.State;
            }
            else
            {
                if (Managers.Game == null)
                {
                    code = "game manager not ready";
                    text = "游戏管理器尚未初始化。";
                    return false;
                }

                result.State = Managers.Game.State;

                // 客户端无 TrialManager，子状态来自 S_TRIAL_STATE 的镜像缓存
                if (result.State == EGameState.Trial && TrialMirror.HasState)
                    result.TrialState = TrialMirror.LatestState;
            }

            code = null;
            text = null;
            return true;
        }
    }
}
