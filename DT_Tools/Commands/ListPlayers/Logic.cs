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
    /// <summary>/list_players 业务：本机玩家表收集 + 房主 PlayerId 识别 + P2P 远端地址。</summary>
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

            /// <summary>
            /// P2P 连接远端描述：直连时为 IP:port；SDR 中继时为 sdr:POP 或中继地址。
            /// 无本机→该玩家的 P2P 句柄时为 null（客机通常只有到房主的连接）。
            /// </summary>
            public string RemoteIp;

            /// <summary>连接是否走中继（SDR）。null = 未知/无连接。</summary>
            public bool? Relayed;

            /// <summary>实时 ping（ms），&lt;0 表示不可用。</summary>
            public int PingMs = -1;
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
                    TryResolveConnection(steamId, out string ip, out bool? relayed, out int ping);
                    return new Entry
                    {
                        Pid = p.PublicInfo.PlayerId,
                        Name = p.Name ?? "(未命名)",
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

        /// <summary>
        /// 通过 P2PSessionManager → SteamP2PSession.Connection → GetConnectionInfo
        /// 取远端地址 / 是否中继 / ping。
        /// 路径对齐 0.1.17a DummyClient/SteamP2PSession.cs:39 GetConnectionInfo、
        /// DummyClient/P2PSessionManager.cs:39 TryGet。
        ///
        /// 拓扑说明：游戏是星型——客机只与房主建连，客机之间没有 P2P 句柄，
        /// 故客机执行本命令时除房主外均为无连接（ip_src=—）。
        /// SDR 下 m_addrRemote 常为空，此时用 m_idPOPRelay 显示 sdr:POP。
        /// </summary>
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
                    // 0x10 = k_nSteamNetworkConnectionInfoFlags_Relay（走 SDR 中继）
                    bool isRelay = (info.m_nFlags & 0x10) != 0;
                    relayed = isRelay;

                    remoteIp = FormatIpAddr(ref info.m_addrRemote);

                    // SDR 时常无有效 m_addrRemote，用 POP 名占位（对齐 SteamP2PSession.DecodePop）
                    if (string.IsNullOrEmpty(remoteIp) && isRelay)
                    {
                        string pop = DecodePop(info.m_idPOPRelay);
                        if (!string.IsNullOrEmpty(pop))
                            remoteIp = "sdr:" + pop;
                        else
                            remoteIp = "sdr:(relay)";
                    }
                }

                pingMs = session.GetPingMs();

                // 有 ping 却仍无地址：至少标已连通
                if (string.IsNullOrEmpty(remoteIp) && pingMs >= 0)
                    remoteIp = relayed == true ? "sdr:(relay)" : "connected";
            }
            catch
            {
                // Steam API 在关房/断线瞬间可能抛，吞掉保持 list 可用
            }
        }

        /// <summary>
        /// SteamNetworkingPOPID → 四字符 POP 名（如 hkg）。
        /// 对齐 0.1.17a DummyClient/SteamP2PSession.cs:64 DecodePop。
        /// </summary>
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

        /// <summary>
        /// SteamNetworkingIPAddr → "a.b.c.d:port" 或 "[v6]:port"。
        /// Steamworks.NET 签名：void ToString(out string buf, bool bWithPort)。
        /// </summary>
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
                // 回退 IPv4 手工格式化
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
                // ignore
            }

            return null;
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
