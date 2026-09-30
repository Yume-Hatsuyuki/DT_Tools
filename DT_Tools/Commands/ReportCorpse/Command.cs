using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.ReportCorpse
{
    /// <summary>
    /// /report [#corpseId] — 客户端报警：无参列出场上尸体与可报警状态；带 #id 对尸体
    /// 直接发 C_INTERACT_CORPSE 强制全员开会（服务端 EndSurvival → Detective）。
    /// 无视距离 / 身份 / 存活状态（跟随控制台，非房主可用，只需本机已进入对局）。
    /// x 是死者 PlayerId（尸体 DeviceId 与死者 PlayerId 相同）。
    /// </summary>
    internal sealed class ReportCorpseCommand : ICommand
    {
        public string Name => "report";
        public string[] Aliases => new[] { "alert_corpse", "开庭", "报警", "我想开庭" };
        public string Usage => "report [#corpseId]";
        public string Description => "客户端报警：无参列出场上尸体，带 #id 直接发 C_INTERACT_CORPSE 强制开会（跟随控制台）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!ReportCorpseLogic.TryGetLocalContext(out var device, out string ctxCode, out string ctxText))
            {
                ctx.Reply(ctxText);
                return CommandResult.Fail(ctxCode);
            }

            // 无参：列尸体 + 用法，不报警
            if (ctx.Args.Length == 0)
            {
                var corpses = ReportCorpseLogic.ListCorpses(device);
                ctx.Reply(ReportCorpseFormat.ReplyList(corpses, device));
                return CommandResult.Success(ReportCorpseFormat.ResultList(corpses, device));
            }

            // 带参：报警指定尸体。报警只在生存阶段有意义（已开会/未开局都拒）
            if (!ReportCorpseLogic.RequireSurvive(out string stateCode, out string stateText))
            {
                ctx.Reply(stateText);
                return CommandResult.Fail(stateCode);
            }

            if (!TargetSpec.TryParseId(ctx.Args[0], out int targetId))
            {
                ctx.Reply($"无效的 ID: {ctx.Args[0]}（应为 #<数字> 或纯数字）");
                return CommandResult.Fail("invalid target");
            }

            var target = ReportCorpseLogic.FindCorpse(device, targetId);
            if (target == null)
            {
                ctx.Reply($"场上没有尸体 #{targetId}（先用 /report 查看当前尸体列表）。");
                return CommandResult.Fail("corpse not found");
            }

            string blockReason = ReportCorpseLogic.GetBlockReason(device, target);
            if (blockReason != null)
            {
                ctx.Reply($"尸体 #{targetId} 当前不可报警：{blockReason}。");
                return CommandResult.Fail("blocked", new { reason = blockReason });
            }

            Managers.Network.GameServer.Send(new C_INTERACT_CORPSE
            {
                CorpseId = target.ID,
            });

            ctx.Reply(ReportCorpseFormat.ReplyReport(target));
            return CommandResult.Success(ReportCorpseFormat.ResultReport(target));
        }
    }
}
