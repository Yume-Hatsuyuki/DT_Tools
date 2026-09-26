using DummyClient;
using DT_Tools.Game;
using Server.Game;
using Steamworks;

namespace DT_Tools.Commands.RoomCode
{
    /// <summary>/room_code 业务：房间号、人数与上限读取（房主/客户端数据源不同）。</summary>
    internal static class RoomCodeLogic
    {
        /// <summary>
        /// 读取房间号 / 人数 / 上限。失败返回 false（提示与错误码已给出）。
        /// 注意：max 当前取原版常量 SteamLobbyManager.MaxMembers（0.1.15b
        /// DummyClient/SteamLobbyManager.cs:10，值为 8）；LobbyMaxPlayers 补丁迁移后
        /// （Patches/System/LobbyMaxPlayers）应改用补丁生效值。
        /// </summary>
        public static bool TryRead(
            out string code,
            out bool isHost,
            out int current,
            out int max,
            out int steamLimit,
            out string errCode,
            out string errText)
        {
            code = null;
            isHost = false;
            current = 0;
            max = 0;
            steamLimit = 0;

            if (Managers.Network == null)
            {
                errCode = "network not ready";
                errText = "网络管理器尚未初始化。";
                return false;
            }

            code = Managers.Network.RoomCode;   // 0.1.15b NetworkManager.cs:138
            if (string.IsNullOrEmpty(code))
            {
                errCode = "not in room";
                errText = "当前不在任何房间中（房间号为空）。";
                return false;
            }

            var lobby = Managers.Network.Lobby;
            isHost = HostGuard.IsHost;
            current = CurrentCount(isHost, lobby);
            max = SteamLobbyManager.MaxMembers;
            steamLimit = TryGetSteamLimit(lobby);

            errCode = null;
            errText = null;
            return true;
        }

        /// <summary>人数读取：房主走 GameRoom.Players（服务器权威），客户端走 Steam Lobby 成员数。</summary>
        public static int CurrentCount(bool isHost, SteamLobbyManager lobby)
        {
            if (isHost)
                return GameRoom.Instance?.Players.Count ?? 0;
            return lobby != null && lobby.InLobby ? lobby.GetMembers().Count : 0;
        }

        /// <summary>
        /// Steam 容器上限（进房人数仍按 max 校验）；不在 Lobby 时返回 0
        /// （Steamworks SteamMatchmaking.GetLobbyMemberLimit）。
        /// </summary>
        public static int TryGetSteamLimit(SteamLobbyManager lobby)
        {
            if (lobby == null || !lobby.InLobby)
                return 0;
            return SteamMatchmaking.GetLobbyMemberLimit(lobby.LobbyId);
        }
    }
}
