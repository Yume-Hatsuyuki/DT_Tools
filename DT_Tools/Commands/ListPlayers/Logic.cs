using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            /// <summary>Steam 账号昵称（persona name），数据未就绪/不可查为 null。</summary>
            public string SteamName;
            public bool IsHost;
            public bool IsSelf;
        }

        /// <summary>
        /// 房主 PlayerId：Host 端直接取 GameRoom.Host；客户端用 Steam Lobby 的 HostSteamId
        /// 在 Roster 里反查。识别失败返回 0
        /// （0.1.16b DummyClient/SteamLobbyManager.cs:90 HostSteamId / :104 InLobby）。
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
                .Select(p =>
                {
                    ulong steamId = Managers.Player.GetRosterSteamId(p.PublicInfo.PlayerId);
                    return new Entry
                    {
                        Pid = p.PublicInfo.PlayerId,
                        Name = p.Name ?? "(未命名)",
                        SteamId = steamId,
                        SteamName = SteamNameOf(steamId),
                        IsHost = p.PublicInfo.PlayerId == hostId,
                        IsSelf = p.PublicInfo.PlayerId == myId,
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Steam 账号昵称（persona name）：同房间/同大厅玩家无需好友关系即可查
        /// （查询模式对齐 0.1.16b GameHistoryStorage.cs:472 GetSteamName；
        /// 清洗对齐同文件 CleanName——去控制字符、NeutralizeRichText、截断 32）。
        /// Steam 未初始化 / 数据未就绪（空、"[unknown]"）返回 null。
        /// </summary>
        public static string SteamNameOf(ulong steamId)
        {
            if (steamId == 0 || Managers.Steam == null || !Managers.Steam.IsInitialized)
                return null;
            try
            {
                string raw = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
                if (string.IsNullOrWhiteSpace(raw) || raw == "[unknown]")
                    return null;

                var sb = new StringBuilder(Math.Min(raw.Length, 32));
                foreach (char c in raw)
                {
                    if (!char.IsControl(c))
                    {
                        sb.Append(c);
                        if (sb.Length >= 32)
                            break;
                    }
                }
                string cleaned = Util.NeutralizeRichText(sb.ToString()).Trim();
                return cleaned.Length > 0 ? cleaned : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
