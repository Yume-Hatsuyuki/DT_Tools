using System.Text;
using BepInEx.Logging;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /players
    ///
    /// 列出当前房间内所有玩家的 PlayerId / SteamId / 昵称。
    ///
    /// 数据来源与 CosmeticSightingReporter 一致：
    ///   - 玩家列表:   Managers.Player.GetAllPlayers()
    ///   - SteamId:    Managers.Player.GetRosterSteamId(playerId)
    ///                 （由 S_PLAYER_ROSTER 广播维护，客户端/房主均可读）
    ///
    /// 这是纯读操作，不需要房主权限，任何运行本控制台的客户端都能查询。
    /// </summary>
    internal sealed class ListPlayersCommand : IConsoleCommand
    {
        public string   Name        => "list_players";
        public string[] Aliases     => new[] { "list", "who" };
        public string   Usage       => "list_players";
        public string   Description => "列出所有玩家的 PlayerId / SteamId / 昵称。";
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

            var sb = new StringBuilder();
            sb.AppendLine("━━━ 玩家列表 ━━━");

            int myId = Managers.Player.MyPlayerID;
            int count = 0;

            foreach (var player in players)
            {
                if (player?.PublicInfo == null) continue;

                int    pid     = player.PublicInfo.PlayerId;
                ulong  steamId = Managers.Player.GetRosterSteamId(pid);
                string name    = player.Name ?? "(未命名)";
                string tag     = pid == myId ? "  ← 这是你" : "";

                string steamStr = steamId != 0
                    ? steamId.ToString()
                    : "未知";

                sb.AppendLine($"  #{pid,-4} {name,-16} steam_id={steamStr}{tag}");
                count++;
            }

            sb.Append($"共 {count} 名玩家");
            console.Log(sb.ToString(), LogLevel.Info);
        }
    }
}
