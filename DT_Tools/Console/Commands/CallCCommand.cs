using System;
using BepInEx.Logging;
using DummyClient;
using Google.Protobuf;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /call_c &lt;#id&gt; &lt;C_*包名&gt; [json]
    /// 模拟指定玩家向 Host 提交 C_* 包（走 PacketManager.HandlePacket）。
    /// </summary>
    internal sealed class CallCCommand : IConsoleCommand
    {
        public string Name => "call_c";
        public string[] Aliases => new[] { "callc", "send_c" };
        public string Usage => "call_c <#id> <C_PacketName> [json]";
        public string Description => "以指定玩家身份注入 C_* 包到 Host 处理链。无参显示帮助与包名自检。";
        public string Author => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            PacketCallHelper.EnsureInit(console);

            if (args.Length == 0)
            {
                console.Log(
                    PacketCallHelper.BuildHelp(serverPackets: false, "用法: " + Usage),
                    LogLevel.Info);
                return;
            }

            if (!PacketCallHelper.RequireHost(console)) return;
            var room = PacketCallHelper.RequireRoom(console);
            if (room == null) return;

            // call_c 只支持单一玩家身份（不能 all）
            string targetToken = args[0];
            if (targetToken.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                console.Log("call_c 不支持 all，请指定 #<playerId> 作为发包身份。", LogLevel.Warning);
                return;
            }
            if (!PacketCallHelper.TryParseTarget(targetToken, out _, out int targetId, out string targetErr))
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
            if (!PacketCallHelper.TryResolveType(false, packetName, out Type type, out string typeErr))
            {
                console.Log(typeErr, LogLevel.Warning);
                return;
            }

            if (!PacketCallHelper.TryGetPacketId(packetName, out ushort protocol, out string idErr))
            {
                console.Log(idErr, LogLevel.Warning);
                return;
            }

            string json = PacketCallHelper.JoinJson(args, 2);
            if (!PacketCallHelper.TryCreateMessage(type, json, out IMessage message, out string createErr))
            {
                console.Log(createErr, LogLevel.Warning);
                return;
            }

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

            IPacketSink sink = found.Session;
            var packet = new Packet
            {
                Protocol = protocol,
                Pkt = message,
                Session = sink
            };

            try
            {
                PacketManager.Instance.HandlePacket(sink, packet);
                console.Log($"已注入 {type.Name}（id={protocol}）← 身份 {found.Name}（#{targetId}）。", LogLevel.Message);
            }
            catch (Exception ex)
            {
                console.Log($"HandlePacket 异常: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
