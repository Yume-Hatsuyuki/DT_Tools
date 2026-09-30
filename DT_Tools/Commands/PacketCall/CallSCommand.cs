using System;
using DT_Tools.Commands;
using DT_Tools.Game;
using Google.Protobuf;

namespace DT_Tools.Commands.PacketCall
{
    /// <summary>
    /// /call_s &lt;all|#id&gt; &lt;S_*包名&gt; [json] — 服务端 → 客户端：构造 S_* 包并
    /// Send / 全员各发一份。仅房主。
    /// </summary>
    internal sealed class CallSCommand : ICommand
    {
        public string Name => "call_s";
        public string[] Aliases => new[] { "calls", "send_s" };
        public string Usage => "call_s <all|#id> <S_PacketName> [json]";
        public string Description => "向玩家发送 S_* 协议包（JSON 填充，房主）。无参显示帮助与包名自检。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            PacketCallLogic.EnsureInit(ctx);

            if (ctx.Args.Length == 0)
            {
                ctx.Reply(PacketCallLogic.BuildHelp(serverPackets: true, "用法: " + Usage));
                return CommandResult.Success(new { mode = "help" });
            }

            var room = PacketCallLogic.RequireRoom(ctx);
            if (room == null)
                return CommandResult.Fail("no room");

            if (!PacketCallLogic.TryParseTarget(ctx.Args[0], out bool targetAll, out int targetId, out string targetError))
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
            if (!PacketCallLogic.TryResolveType(true, packetName, out Type type, out string typeError))
            {
                ctx.Reply(typeError);
                return CommandResult.Fail("unknown packet");
            }

            string json = PacketCallLogic.JoinJson(ctx.Args, 2);
            if (!PacketCallLogic.TryCreateMessage(type, json, out IMessage message, out string createError))
            {
                ctx.Reply(createError);
                return CommandResult.Fail("create failed");
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
                        ctx.Warn($"发给 #{player.PublicInfo.PlayerId} 失败: {ex.Message}");
                    }
                }
                ctx.Reply($"已发送 {type.Name} → {sent} 名玩家。");
                return CommandResult.Success(new { packet = type.Name, sent });
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

            found.Session.Send(message);
            ctx.Reply($"已发送 {type.Name} → {found.Name}（#{targetId}）。");
            return CommandResult.Success(new { packet = type.Name, pid = targetId });
        }
    }
}
