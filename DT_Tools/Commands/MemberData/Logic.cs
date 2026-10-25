using System;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Features.DtPresence;
using DT_Tools.Game;
using Steamworks;

namespace DT_Tools.Commands.MemberData
{
    internal static class MemberDataLogic
    {
        public static readonly string[] DefaultKeys =
        {
            DummyClient.SteamLobbyManager.LOBBY_DATA_BUILD_KEY,
            DtPresenceLogic.MemberDataKey,
        };

        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public ulong SteamId;
            public bool IsHost;
            public bool IsSelf;
            public Dictionary<string, string> Data = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public static int ResolveHostPlayerId()
        {
            if (HostGuard.IsHost)
            {
                var host = Server.Game.GameRoom.Instance?.Host;
                if (host?.PublicInfo != null)
                    return host.PublicInfo.PlayerId;
            }

            var lobby = Managers.Network?.Lobby;
            if (lobby == null || !lobby.InLobby || lobby.HostSteamId == CSteamID.Nil)
                return 0;

            ulong hostSteam = lobby.HostSteamId.m_SteamID;
            if (hostSteam == 0 || Managers.Player == null)
                return 0;

            foreach (var player in Managers.Player.GetAllPlayers() ?? Enumerable.Empty<Player>())
            {
                if (player?.PublicInfo == null) continue;
                int pid = player.PublicInfo.PlayerId;
                if (Managers.Player.GetRosterSteamId(pid) == hostSteam)
                    return pid;
            }
            return 0;
        }

        public static List<Entry> Collect(int filterPid, IEnumerable<string> extraKeys)
        {
            var keys = new List<string>();
            foreach (string k in DefaultKeys)
                AddKey(keys, k);
            if (extraKeys != null)
            {
                foreach (string k in extraKeys)
                    AddKey(keys, k);
            }

            int hostId = ResolveHostPlayerId();
            int myId = Managers.Player?.MyPlayerID ?? 0;
            var lobby = Managers.Network?.Lobby;
            bool canRead = lobby != null && lobby.InLobby && lobby.LobbyId != CSteamID.Nil;

            var list = new List<Entry>();
            var players = Managers.Player?.GetAllPlayers();
            if (players == null)
                return list;

            foreach (var player in players.OrderBy(p => p?.PublicInfo?.PlayerId ?? 0))
            {
                if (player?.PublicInfo == null)
                    continue;
                int pid = player.PublicInfo.PlayerId;
                if (filterPid > 0 && pid != filterPid)
                    continue;

                ulong steamId = Managers.Player.GetRosterSteamId(pid);
                var entry = new Entry
                {
                    Pid = pid,
                    Name = player.Name ?? "(未命名)",
                    SteamId = steamId,
                    IsHost = pid == hostId,
                    IsSelf = pid == myId,
                };

                if (canRead && steamId != 0)
                {
                    var sid = new CSteamID(steamId);
                    foreach (string key in keys)
                    {
                        string val = "";
                        try
                        {
                            val = SteamMatchmaking.GetLobbyMemberData(lobby.LobbyId, sid, key) ?? "";
                        }
                        catch
                        {
                            val = "";
                        }
                        entry.Data[key] = val;
                    }
                }

                list.Add(entry);
            }

            return list;
        }

        private static void AddKey(List<string> keys, string key)
        {
            key = (key ?? "").Trim();
            if (key.Length == 0)
                return;
            if (keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                return;
            keys.Add(key);
        }
    }
}
