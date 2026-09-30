using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Server.Game;
using Steamworks;

namespace DT_Tools.Commands.ListPlayers
{
    /// <summary>/list_players 业务：本机玩家表收集 + 房主 PlayerId 识别。</summary>
    internal static class ListPlayersLogic
    {
        /// <summary>玩家条目（本机客户端玩家表）。</summary>
        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public ulong SteamId;
            public bool IsHost;
            public bool IsSelf;
        }

        /// <summary>
        /// 房主 PlayerId：Host 端直接取 GameRoom.Host；客户端用 Steam Lobby 的 HostSteamId
        /// 在 Roster 里反查。识别失败返回 0
        /// （0.1.15b DummyClient/SteamLobbyManager.cs:90 HostSteamId / :104 InLobby）。
        /// </summary>
        public static int ResolveHostPlayerId()
        {
            if (HostGuard.IsHost)
            {
                var host = GameRoom.Instance?.Host;
                if (host?.PublicInfo != null)
                    return host.PublicInfo.PlayerId;
            }

            var lobby = Managers.Network?.Lobby;
            if (lobby == null || !lobby.InLobby || lobby.HostSteamId == CSteamID.Nil)
                return 0;

            ulong hostSteam = lobby.HostSteamId.m_SteamID;
            if (hostSteam == 0)
                return 0;

            var players = Managers.Player.GetAllPlayers();
            if (players == null)
                return 0;

            foreach (var player in players)
            {
                if (player?.PublicInfo == null) continue;
                int pid = player.PublicInfo.PlayerId;
                if (Managers.Player.GetRosterSteamId(pid) == hostSteam)
                    return pid;
            }

            return 0;
        }

        /// <summary>按 PlayerId 升序整理玩家条目。</summary>
        public static List<Entry> Collect(int hostId, int myId)
        {
            return Managers.Player.GetAllPlayers()
                .Where(p => p?.PublicInfo != null)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .Select(p => new Entry
                {
                    Pid = p.PublicInfo.PlayerId,
                    Name = p.Name ?? "(未命名)",
                    SteamId = Managers.Player.GetRosterSteamId(p.PublicInfo.PlayerId),
                    IsHost = p.PublicInfo.PlayerId == hostId,
                    IsSelf = p.PublicInfo.PlayerId == myId,
                })
                .ToList();
        }
    }
}
