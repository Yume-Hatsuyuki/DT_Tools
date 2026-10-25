using System;
using Protocol;
using Server.Game;

namespace DT_Tools.Game
{
    public static class GameState
    {
        public static bool IsInGame(EGameState state)
            => state != EGameState.Lobby
               && state != EGameState.NoneState
               && state != EGameState.PickCharacter;

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
