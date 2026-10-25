using DummyClient;
using DT_Tools.Core;
using DT_Tools.Game;
using DT_Tools.Features.LobbyMaxPlayers;
using Server.Game;
using Steamworks;

namespace DT_Tools.Commands.RoomCode
{
    internal static class RoomCodeLogic
    {
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

            code = Managers.Network.RoomCode;
            if (string.IsNullOrEmpty(code))
            {
                errCode = "not in room";
                errText = "当前不在任何房间中（房间号为空）。";
                return false;
            }

            var lobby = Managers.Network.Lobby;
            isHost = HostGuard.IsHost;
            current = CurrentCount(isHost, lobby);
            max = Engine.Enabled<LobbyMaxPlayersFeature>()
                ? LobbyMaxPlayersFeature.MaxMembers
                : SteamLobbyManager.MaxMembers;
            steamLimit = TryGetSteamLimit(lobby);

            errCode = null;
            errText = null;
            return true;
        }

        public static int CurrentCount(bool isHost, SteamLobbyManager lobby)
        {
            if (isHost)
                return GameRoom.Instance?.Players.Count ?? 0;
            return lobby != null && lobby.InLobby ? lobby.GetMembers().Count : 0;
        }

        public static int TryGetSteamLimit(SteamLobbyManager lobby)
        {
            if (lobby == null || !lobby.InLobby)
                return 0;
            return SteamMatchmaking.GetLobbyMemberLimit(lobby.LobbyId);
        }
    }
}
