using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GameState
{
    internal static class GameStateLogic
    {
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

                if (result.State == EGameState.Trial && TrialMirror.HasState)
                    result.TrialState = TrialMirror.LatestState;
            }

            code = null;
            text = null;
            return true;
        }
    }
}
