using System;
using System.Collections.Generic;
using DummyClient;

namespace DT_Tools.Commands.RoomList
{
    /// <summary>
    /// /room_list 业务：公开房间列表的异步查询（Steam Lobby）。
    /// </summary>
    internal static class RoomListLogic
    {
        /// <summary>
        /// 网络与 Lobby 管理器就绪校验。
        /// </summary>
        /// <param name="lobby">就绪的大厅管理器；失败时为 null。</param>
        /// <param name="code">失败时的错误码。</param>
        /// <param name="text">失败时的提示文字。</param>
        /// <returns>网络与大厅管理器都就绪时为 true。</returns>
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

        /// <summary>
        /// 发起异步查询（0.1.16b DummyClient/SteamLobbyManager.cs:140（待核对）
        /// RequestLobbyList(Action&lt;List&lt;LobbyListEntry&gt;&gt;)，结果经 Steam 回调送达）。
        /// </summary>
        /// <param name="lobby">大厅管理器。</param>
        /// <param name="onResult">查询完成后调用，传入大厅列表。</param>
        public static void Request(SteamLobbyManager lobby, Action<List<LobbyListEntry>> onResult)
            => lobby.RequestLobbyList(onResult);
    }
}
