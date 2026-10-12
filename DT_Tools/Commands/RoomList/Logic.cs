using System;
using System.Collections.Generic;
using DummyClient;

namespace DT_Tools.Commands.RoomList
{
    /// /room_list 业务：公开房间列表的异步查询（Steam Lobby）。
    internal static class RoomListLogic
    {
        /// 网络与 Lobby 管理器就绪校验。
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

        /// 发起异步查询（0.1.16b DummyClient/SteamLobbyManager.cs:140
        /// RequestLobbyList(Action&lt;List&lt;LobbyListEntry&gt;&gt;)，结果经 Steam 回调送达）。
        public static void Request(SteamLobbyManager lobby, Action<List<LobbyListEntry>> onResult)
            => lobby.RequestLobbyList(onResult);
    }
}
