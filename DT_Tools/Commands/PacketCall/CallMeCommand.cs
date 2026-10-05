using System;
using DT_Tools.Commands;
using DT_Tools.Game;
using Google.Protobuf;

namespace DT_Tools.Commands.PacketCall
{
    /// <summary>
    /// /call_me &lt;C_包名&gt; [json] — 客户端以自己身份发送 C_* 意图包。走与原版 UI 完全
    /// 相同的上行路径 Managers.Network.GameServer.Send（Game/ClientPackets.cs 封装），
    /// 无房主门禁，进游戏即可用。与 /call_c（房主以他人身份注入）、/call_s（房主下发）
    /// 互补：本机非房主时的行动通道。协议语义先查 .github/skills/MCP/MCP.md。
    /// </summary>
    internal sealed class CallMeCommand : ICommand
    {
        public string Name => "call_me";
        public string[] Aliases => new[] { "callme", "send_me" };
        public string Usage => "call_me <C_PacketName> [json]";
        public string Description => "以本客户端身份发送 C_* 意图包（JSON 填充）。无参显示帮助与包名自检。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            PacketCallLogic.EnsureInit(ctx);

            if (ctx.Args.Length == 0)
            {
                ctx.Reply(PacketCallLogic.BuildHelp(serverPackets: false, "用法: " + Usage));
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
