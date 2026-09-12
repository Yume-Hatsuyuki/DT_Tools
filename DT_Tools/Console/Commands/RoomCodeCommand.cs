using System.Text;
using BepInEx.Logging;
using DT_Tools.Patches.System;
using Server.Game;
using Steamworks;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /room_code
    ///
    /// 显示当前房间的房间号（7 位邀请码）及玩家数 [当前/上限]。
    ///
    /// 数据来源:
    ///   - 房间号:   Managers.Network.RoomCode
    ///   - 当前人数: 房主端 → GameRoom.Instance.Players.Count（服务器权威）
    ///               客户端 → Lobby.GetMembers().Count（Steam Lobby 可见成员）
    ///   - 人数上限: Patch_CreateLobby.MaxMembers（游戏进房判定，默认 8）
    ///   - Steam容器: GetLobbyMemberLimit（仅当与进房上限不一致时附加一行）
    ///
    /// 这是纯读操作，不需要房主权限。
    /// </summary>
    internal sealed class RoomCodeCommand : IConsoleCommand
    {
        public string   Name        => "room_code";
        public string[] Aliases     => new[] { "code", "room" };
        public string   Usage       => "room_code";
        public string   Description => "显示当前房间的房间号（邀请码）及玩家数。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            if (Managers.Network == null)
            {
                console.Log("网络管理器尚未初始化。", LogLevel.Warning);
                return;
            }

            string code = Managers.Network.RoomCode;

            if (string.IsNullOrEmpty(code))
            {
                console.Log("当前不在任何房间中（房间号为空）。", LogLevel.Warning);
                return;
            }

            int max = Patch_CreateLobby.MaxMembers;
            var lobby = Managers.Network.Lobby;

            int current;
            if (Managers.Host != null && Managers.Host.IsHost)
            {
                current = GameRoom.Instance?.Players.Count ?? 0;
            }
            else
            {
                current = (lobby != null && lobby.InLobby)
                    ? lobby.GetMembers().Count
                    : 0;
            }

            var text = new StringBuilder();
            text.AppendLine("━━━ 房间信息 ━━━");
            text.AppendLine($"  房间号:  {code}");
            text.Append($"  玩家数:  {current} / {max}");

            if (lobby != null && lobby.InLobby)
            {
                int steamLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby.LobbyId);
                if (steamLimit > 0 && steamLimit != max)
                    text.Append($"\n  Steam容器:  {current} / {steamLimit}（仅容器，进房仍按 {max}）");
            }

            console.Log(text.ToString(), LogLevel.Info);
        }
    }
}
