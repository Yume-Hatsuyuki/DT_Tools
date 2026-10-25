using System;
using System.Collections.Generic;
using DummyClient;

namespace DT_Tools.Commands.RoomList
{
    internal static class RoomListLogic
    {
        public static bool TryGetLobby(out SteamLobbyManager lobby, out string code, out string text)
        {
            lobby = Managers.Network?.Lobby;
            if (Managers.Network == null || lobby == null)
            {
                code = "network not ready";
                text = "网络管理器尚未初始化。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }

        public static void Request(SteamLobbyManager lobby, Action<List<LobbyListEntry>> onResult)
            => lobby.RequestLobbyList(onResult);
    }
}
