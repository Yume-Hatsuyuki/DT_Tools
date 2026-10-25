using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DummyClient;
using DT_Tools.Game;
using Server.Game;
using Steamworks;

namespace DT_Tools.Commands.ListPlayers
{
    internal static class ListPlayersLogic
    {
        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public ulong SteamId;

            public int CharacterId;

            public string Character;

            public string SteamName;
            public bool IsHost;
            public bool IsSelf;

            public string RemoteIp;

            public bool? Relayed;

            public int PingMs = -1;
        }

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

        public static List<Entry> Collect(int hostId, int myId)
        {
            return Managers.Player.GetAllPlayers()
                .Where(p => p?.PublicInfo != null)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .Select(p =>
                {
                    ulong steamId = Managers.Player.GetRosterSteamId(p.PublicInfo.PlayerId);
                    TryResolveConnection(steamId, out string ip, out bool? relayed, out int sessionPing);
                    ulong hostSteamId = Managers.Network?.GameHostSteamId.m_SteamID ?? 0;
                    bool isHostRow = steamId != 0 && steamId == hostSteamId;
                    int ping = ResolveRosterPing(
                        iAmHost: HostGuard.IsHost,
                        isHostRow: isHostRow,
                        sessionPingMs: sessionPing,
                        rosterPingMs: Managers.Player.GetRosterPing(p.PublicInfo.PlayerId),
                        hostPingMs: isHostRow ? (Managers.Network?.GetHostPingMs() ?? -1) : -1);
                    int characterId = p.PublicInfo.CharacterId;
                    return new Entry
                    {
                        Pid = p.PublicInfo.PlayerId,
                        Name = p.Name ?? "(未命名)",
                        CharacterId = characterId,
                        Character = CharacterNames.IsKnown(characterId) ? CharacterNames.EnglishNameOf(characterId) : null,
                        SteamId = steamId,
                        SteamName = SteamNameOf(steamId),
                        IsHost = p.PublicInfo.PlayerId == hostId,
                        IsSelf = p.PublicInfo.PlayerId == myId,
                        RemoteIp = ip,
                        Relayed = relayed,
                        PingMs = ping,
                    };
                })
                .ToList();
        }

        public static int ResolveRosterPing(bool iAmHost, bool isHostRow, int sessionPingMs, int rosterPingMs, int hostPingMs)
        {
            if (iAmHost)
                return sessionPingMs;
            if (isHostRow)
                return hostPingMs;
            return rosterPingMs;
        }

        public static void TryResolveConnection(ulong steamId, out string remoteIp, out bool? relayed, out int pingMs)
        {
            remoteIp = null;
            relayed = null;
            pingMs = -1;

            if (steamId == 0)
                return;

            var p2p = Managers.Network?.P2P;
            if (p2p == null)
                return;

            if (!p2p.TryGet(new CSteamID(steamId), out HostPeerSession hostSession) || hostSession == null)
                return;

            if (!(hostSession.Underlying is SteamP2PSession session))
                return;

            HSteamNetConnection conn = session.Connection;
            if (conn == HSteamNetConnection.Invalid)
                return;

            try
            {
                if (SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info))
                {
                    bool isRelay = (info.m_nFlags & 0x10) != 0;
                    relayed = isRelay;

                    remoteIp = FormatIpAddr(ref info.m_addrRemote);

                    if (string.IsNullOrEmpty(remoteIp) && isRelay)
                    {
                        string pop = DecodePop(info.m_idPOPRelay);
                        if (!string.IsNullOrEmpty(pop))
                            remoteIp = $"sdr:{pop}";
                        else
                            remoteIp = "sdr:(relay)";
                    }
                }

                pingMs = session.GetPingMs();

                if (string.IsNullOrEmpty(remoteIp) && pingMs >= 0)
                    remoteIp = relayed == true ? "sdr:(relay)" : "connected";
            }
            catch
            {
            }
        }

        static string DecodePop(SteamNetworkingPOPID popId)
        {
            uint pop = (uint)popId;
            if (pop == 0)
                return null;
            var sb = new StringBuilder(4);
            int[] shifts = { 16, 8, 0, 24 };
            foreach (int num in shifts)
            {
                char c = (char)((pop >> num) & 0xFF);
                if (c != 0)
                    sb.Append(c);
            }
            string s = sb.ToString();
            return s.Length > 0 ? s : null;
        }

        static string FormatIpAddr(ref SteamNetworkingIPAddr addr)
        {
            try
            {
                addr.ToString(out string s, bWithPort: true);
                if (!string.IsNullOrEmpty(s))
                {
                    s = s.Trim();
                    if (s.Length > 0 && s != "[::]:0" && s != "0.0.0.0:0")
                        return s;
                }
            }
            catch
            {
            }

            try
            {
                if (addr.IsIPv4())
                {
                    uint ip = addr.GetIPv4();
                    byte a = (byte)((ip >> 24) & 0xFF);
                    byte b = (byte)((ip >> 16) & 0xFF);
                    byte c = (byte)((ip >> 8) & 0xFF);
                    byte d = (byte)(ip & 0xFF);
                    return $"{a}.{b}.{c}.{d}:{addr.m_port}";
                }
            }
            catch
            {
            }

            return null;
        }

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
