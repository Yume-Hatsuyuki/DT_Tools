using System;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;
using Google.Protobuf;

namespace DT_Tools.Commands.PacketCall
{
    internal sealed class CallMeCommand : ICommand
    {
        public string Name => "call_me";
        public string[] Aliases => new[] { "callme", "send_me" };
        public string Usage => "call_me <C_PacketName> [json]";
        public string Description => "以本客户端身份发送 C_* 意图包（JSON 填充）。无参显示帮助与包名自检。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.SysNetwork | FeatureTag.SysDebug;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            PacketCallLogic.EnsureInit(ctx);

            if (ctx.Args.Length == 0)
            {
                ctx.Reply(PacketCallLogic.BuildHelp(serverPackets: false, $"用法: {Usage}"));
                return CommandResult.Success(new { mode = "help" });
            }

            string packetName = ctx.Args[0];
            if (!PacketCallLogic.TryResolveType(false, packetName, out Type type, out string typeError))
            {
                ctx.Reply(typeError);
                return CommandResult.Fail("unknown packet");
            }

            string json = PacketCallLogic.JoinJson(ctx.Args, 1);
            if (!PacketCallLogic.TryCreateMessage(type, json, out IMessage message, out string createError))
            {
                ctx.Reply(createError);
                return CommandResult.Fail("create failed");
            }

            if (!ClientPackets.TrySend(message, out string sendError))
            {
                ctx.Warn($"发送失败: {sendError}");
                return CommandResult.Fail("send failed");
            }

            ctx.Reply($"已发送 {type.Name}（本客户端身份）。"
                      + "服务端可能校验后静默拒绝，效果以 S_* 回包与状态变化为准。");
            return CommandResult.Success(new { packet = type.Name, sent = true });
        }
    }
}
