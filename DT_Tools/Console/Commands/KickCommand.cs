using System;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /kick #&lt;playerId&gt;
    ///
    /// 将指定玩家踢出当前房间（仅 Lobby 状态有效）。
    /// 内部直接调用 GameRoom.KickPlayer，与游戏内房主踢人按钮走同一套逻辑：
    ///   - 校验当前为 Lobby 且调用者是 Host
    ///   - 把目标 SteamId 加入本局黑名单（_bannedSteamIds）
    ///   - 向目标发送 S_KICKED 并断开连接
    ///
    /// 示例:
    ///   /kick #3
    /// </summary>
    internal sealed class KickCommand : IConsoleCommand
    {
        public string   Name        => "kick";
        public string[] Aliases     => new[] { "boot", "踢" };
        public string   Usage       => "kick #<playerId>";
        public string   Description => "将指定玩家踢出房间（仅大厅可用，需房主）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            // 确认是房主（只有 Host 端才有 GameRoom）
            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行。", LogLevel.Warning);
                return;
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                console.Log("当前没有活动的游戏房间。", LogLevel.Warning);
                return;
            }

            if (room.State != EGameState.Lobby)
            {
                console.Log("只能在大厅（Lobby）状态下踢人。", LogLevel.Warning);
                return;
            }

            if (args.Length == 0)
            {
                console.Log("用法: /kick #<playerId>\n可用 /list_players 查看玩家列表。", LogLevel.Info);
                return;
            }

            string targetArg = args[0];
            int targetId;

            if (targetArg.StartsWith("#") && int.TryParse(targetArg.Substring(1), out targetId))
            {
                // ok
            }
            else if (int.TryParse(targetArg, out targetId))
            {
                // 也允许直接写数字
            }
            else
            {
                console.Log($"无效的玩家 ID: {targetArg}（应为 #数字 或纯数字）", LogLevel.Warning);
                return;
            }

            var host = room.Host;
            if (host?.PublicInfo == null)
            {
                console.Log("无法获取房主玩家对象。", LogLevel.Error);
                return;
            }

            if (host.PublicInfo.PlayerId == targetId)
            {
                console.Log("不能踢自己。", LogLevel.Warning);
                return;
            }

            var target = room.Players.Find(p => p?.PublicInfo?.PlayerId == targetId);
            if (target == null)
            {
                console.Log($"找不到 PlayerId={targetId} 的玩家。", LogLevel.Warning);
                return;
            }

            string targetName = target.Name ?? ("#" + targetId);
            // 与游戏内踢人按钮相同路径
            room.KickPlayer(host, targetId);
            console.Log($"已踢出玩家 {targetName}（#{targetId}）。", LogLevel.Message);
        }
    }
}
