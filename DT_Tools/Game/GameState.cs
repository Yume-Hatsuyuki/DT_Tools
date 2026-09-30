using System;
using Protocol;
using Server.Game;

namespace DT_Tools.Game
{
    /// <summary>对局状态读取：Host 端走权威 GameRoom.State，客户端走本地镜像 Managers.Game.State。</summary>
    public static class GameState
    {
        public static bool TryGetState(out EGameState state)
        {
            state = EGameState.NoneState;
            try
            {
                if (HostGuard.IsHost)
                {
                    var room = GameRoom.Instance;
                    if (room == null)
                        return false;
                    state = room.State;
                    return true;
                }
                if (Managers.Game == null)
                    return false;
                state = Managers.Game.State;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
