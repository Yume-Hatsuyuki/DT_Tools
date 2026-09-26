using System;
using DT_Tools.Commands;
using DT_Tools.Game;
using DummyClient;
using Google.Protobuf;

namespace DT_Tools.Commands.PacketCall
{
    /// <summary>
    /// /call_c &lt;#id&gt; &lt;C_*包名&gt; [json] — 模拟指定玩家向 Host 提交 C_* 包
    /// （走 PacketManager.HandlePacket，0.1.15b PacketManager.cs:84 HandlePacket、:16 Instance）。
    /// 仅房主。
    /// </summary>
    internal sealed class CallCCommand : ICommand
    {
        public string Name => "call_c";
        public string[] Aliases => new[] { "callc", "send_c" };
        public string Usage => "call_c <#id> <C_PacketName> [json]";
        public string Description => "以指定玩家身份注入 C_* 包到 Host 处理链，无参显示帮助与包名自检。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            PacketCallLogic.EnsureInit(ctx);

            if (ctx.Args.Length == 0)
            {
                ctx.Reply(PacketCallLogic.BuildHelp(serverPackets: false, "用法: " + Usage));
                return CommandResult.Success(new { mode = "help" });
            }

            var room = PacketCallLogic.RequireRoom(ctx);
            if (room == null)
                return CommandResult.Fail("no room");

            // call_c 只支持单一玩家身份（不能 all）
            string targetToken = ctx.Args[0];
            if (targetToken.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Reply("call_c 不支持 all，请指定 #<playerId> 作为发包身份。");
                return CommandResult.Fail("all not supported");
            }
            if (!PacketCallLogic.TryParseTarget(targetToken, out _, out int targetId, out string targetError))
            {
                ctx.Reply(targetError);
                return CommandResult.Fail("invalid target");
            }

            if (ctx.Args.Length < 2)
            {
                ctx.Reply("缺少包名。用法: " + Usage);
                return CommandResult.Fail("missing packet");
            }

            string packetName = ctx.Args[1];
            if (!PacketCallLogic.TryResolveType(false, packetName, out Type type, out string typeError))
            {
                ctx.Reply(typeError);
                return CommandResult.Fail("unknown packet");
            }

            if (!PacketCallLogic.TryGetPacketId(packetName, out ushort protocol, out string idError))
            {
                ctx.Reply(idError);
                return CommandResult.Fail("unknown packet id");
            }

            string json = PacketCallLogic.JoinJson(ctx.Args, 2);
            if (!PacketCallLogic.TryCreateMessage(type, json, out IMessage message, out string createError))
            {
                ctx.Reply(createError);
                return CommandResult.Fail("create failed");
            }

            var found = PlayerQuery.FindById(room, targetId);
            if (found == null)
            {
                ctx.Reply($"找不到 PlayerId={targetId} 的玩家。");
                return CommandResult.Fail("target not found");
            }
            if (found.Session == null)
            {
                ctx.Reply($"玩家 {found.Name}（#{targetId}）无有效 Session。");
                return CommandResult.Fail("no session");
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
                if (PacketCallLogic.IsProtocolRegistered(protocol))
                {
                    ctx.Reply($"已注入 {type.Name}（id={protocol}）← 身份 {found.Name}（#{targetId}）。");
                    return CommandResult.Success(new { packet = type.Name, protocol, pid = targetId, registered = true });
                }

                // 服务端对未注册 Protocol 静默丢弃（0.1.15b PacketManager.cs:84-91）——如实告知，勿谎称已注入
                ctx.Warn($"已提交 {type.Name}（id={protocol}）← 身份 {found.Name}（#{targetId}），"
                         + "但 PacketManager 未注册该 Protocol 的处理器，包已被静默丢弃（无任何效果）。");
                return CommandResult.Success(new { packet = type.Name, protocol, pid = targetId, registered = false });
            }
            catch (Exception ex)
            {
                ctx.Warn($"HandlePacket 异常: {ex.Message}");
                return CommandResult.Fail("handle failed");
            }
        }
    }
}
