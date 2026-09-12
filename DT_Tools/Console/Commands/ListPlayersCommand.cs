using System.Linq;
using System.Text;
using BepInEx.Logging;
using Server.Game;
using Steamworks;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /players
    ///
    /// 列出当前房间内所有玩家的 PlayerId / SteamId / 昵称，并标注自己与房主。
    ///
    /// 数据来源:
    ///   - 玩家列表:   Managers.Player.GetAllPlayers()
    ///   - SteamId:    Managers.Player.GetRosterSteamId(playerId)
    ///   - 房主判定:   本机为 Host 时 → GameRoom.Instance.Host
    ///                 否则 → Lobby.HostSteamId 与 roster SteamId 对齐
    ///
    /// 纯读操作，不需要房主权限。
    /// </summary>
    internal sealed class ListPlayersCommand : IConsoleCommand
    {
        public string   Name        => "list_players";
        public string[] Aliases     => new[] { "list", "who" };
        public string   Usage       => "list_players";
        public string   Description => "列出所有玩家的 PlayerId / SteamId / 昵称，并标注房主。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            if (Managers.Player == null)
            {
                console.Log("玩家管理器尚未初始化（可能还未进入房间）。", LogLevel.Warning);
                return;
            }

            var players = Managers.Player.GetAllPlayers();
            if (players == null || players.Count == 0)
            {
                console.Log("当前没有已知玩家。", LogLevel.Warning);
                return;
            }

            int hostId = ResolveHostPlayerId();
            int myId   = Managers.Player.MyPlayerID;

            // 按 PlayerId 升序，避免 roster 迭代顺序乱跳
            var ordered = players
                .Where(p => p?.PublicInfo != null)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("━━━ 玩家列表 ━━━");

            int count = 0;
            foreach (var player in ordered)
            {
                int    pid     = player.PublicInfo.PlayerId;
                ulong  steamId = Managers.Player.GetRosterSteamId(pid);
                string name    = player.Name ?? "(未命名)";
                string steamStr = steamId != 0 ? steamId.ToString() : "未知";

                string tag = "";
                if (pid == hostId && pid == myId)
                    tag = "  ← 房主·你";
                else if (pid == hostId)
                    tag = "  ← 房主";
                else if (pid == myId)
                    tag = "  ← 你";

                sb.AppendLine($"  #{pid,-4} {name,-16} steam_id={steamStr}{tag}");
                count++;
            }

            if (hostId == 0)
                sb.AppendLine("（未能识别房主）");

            sb.Append($"共 {count} 名玩家");
            console.Log(sb.ToString(), LogLevel.Info);
        }

        /// <summary>
        /// 优先用权威 Host 对象；客户端则用 Lobby.HostSteamId 对齐 roster。
        /// </summary>
        private static int ResolveHostPlayerId()
        {
            // 本机是 Host：GameRoom 上的 Host 最准
            if (Managers.Host != null && Managers.Host.IsHost)
            {
                var host = GameRoom.Instance?.Host;
                if (host?.PublicInfo != null)
                    return host.PublicInfo.PlayerId;
            }

            // 客户端：用 Steam Lobby Owner 对齐 roster
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
    }
}
