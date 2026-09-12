using System;
using BepInEx.Logging;
using Google.Protobuf;
using Server.Game;

namespace DT_Tools.Console.Commands.PacketCall
{
    /// <summary>
    /// /call_s &lt;all|#id&gt; &lt;S_*包名&gt; [json]
    /// 服务端 → 客户端：构造 S_* 包并 Send / 全员各发一份。
    /// </summary>
    internal sealed class CallSCommand : IConsoleCommand
    {
        public string Name => "call_s";
        public string[] Aliases => new[] { "calls", "send_s" };
        public string Usage => "call_s <all|#id> <S_PacketName> [json]";
        public string Description => "向玩家发送 S_* 协议包（JSON 填充，房主）。无参显示帮助与包名自检。";
        public string Author => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            PacketCallHelper.EnsureInit(console);

            if (args.Length == 0)
            {
                console.Log(
                    PacketCallHelper.BuildHelp(serverPackets: true, "用法: " + Usage),
                    LogLevel.Info);
                return;
            }

            if (!PacketCallHelper.RequireHost(console)) return;
            var room = PacketCallHelper.RequireRoom(console);
            if (room == null) return;

            if (!PacketCallHelper.TryParseTarget(args[0], out bool targetAll, out int targetId, out string targetErr))
            {
                console.Log(targetErr, LogLevel.Warning);
                return;
            }

            if (args.Length < 2)
            {
                console.Log("缺少包名。用法: " + Usage, LogLevel.Warning);
                return;
            }

            string packetName = args[1];
            if (!PacketCallHelper.TryResolveType(true, packetName, out Type type, out string typeErr))
            {
                console.Log(typeErr, LogLevel.Warning);
                return;
            }

            string json = PacketCallHelper.JoinJson(args, 2);
            if (!PacketCallHelper.TryCreateMessage(type, json, out IMessage message, out string createErr))
            {
                console.Log(createErr, LogLevel.Warning);
                return;
            }

            int sent = 0;
            if (targetAll)
            {
                foreach (var player in room.Players)
                {
                    if (player?.PublicInfo == null || player.Session == null) continue;
                    try
                    {
                        player.Session.Send(message);
                        sent++;
                    }
                    catch (Exception ex)
                    {
                        console.Log($"发给 #{player.PublicInfo.PlayerId} 失败: {ex.Message}", LogLevel.Warning);
                    }
                }
                console.Log($"已发送 {type.Name} → {sent} 名玩家。", LogLevel.Message);
            }
            else
            {
                var found = PacketCallHelper.FindPlayer(room, targetId);
                if (found == null)
                {
                    console.Log($"找不到 PlayerId={targetId} 的玩家。", LogLevel.Warning);
                    return;
                }
                if (found.Session == null)
                {
                    console.Log($"玩家 {found.Name}（#{targetId}）无有效 Session。", LogLevel.Warning);
                    return;
                }
                found.Session.Send(message);
                console.Log($"已发送 {type.Name} → {found.Name}（#{targetId}）。", LogLevel.Message);
            }
        }
    }
}
