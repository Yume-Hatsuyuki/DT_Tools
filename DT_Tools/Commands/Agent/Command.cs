using DT_Tools.Core.Attributes;
using System.Collections.Generic;
using System.Text;
using DT_Tools.Game;

namespace DT_Tools.Commands.Agent
{
    internal sealed class AgentCommand : ICommand
    {
        public string Name => "agent";
        public string[] Aliases => new[] { "特工", "任务" };
        public string Usage => "agent [all|任务ID|名|#设备号|stop]";
        public string Description => "特工：按任务链自动推进（一步做不完就跳过做下一个）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.SysAutomation | FeatureTag.GameMission;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!AgentArgs.TryParse(ctx.Args, out var args, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("unknown");
            }
            if (args.IsStop)
            {
                AgentRunner.Stop(ctx);
                return CommandResult.Success();
            }

            if (!LocalPlayer.TryGetPlayer(out var my, out string localError))
            {
                ctx.Reply(localError);
                return CommandResult.Fail("not in game");
            }
            if (!LocalPlayer.IsSurvive)
            {
                ctx.Reply($"仅生存阶段可用，当前: {LocalPlayer.StateText}。");
                return CommandResult.Fail("not survive");
            }
            if (Managers.Game.IsSpectator)
            {
                ctx.Reply("观战无法完成任务。");
                return CommandResult.Fail("spectator");
            }
            if (Managers.Device?.Cache == null || Managers.Device.Cache.Count == 0)
            {
                ctx.Reply("设备缓存为空。");
                return CommandResult.Fail("no device");
            }

            if (ctx.Args.Length == 0)
            {
                var preview = AgentPlanner.Peek(default);
                ctx.Reply(BuildHelp(preview));
                return CommandResult.Success(new { next = preview.Count });
            }

            AgentRunner.Start(ctx, args.Filter);
            return CommandResult.Success(new { started = true });
        }

        private static string BuildHelp(List<string> next)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 特工 · 任务链 ━━━");
            sb.AppendLine("一步做不了就跳过；优先获取物/采矿/无等待，摇酒(走)最后。");
            sb.AppendLine();
            sb.AppendLine("【本 tick 可执行（按优先级）】");
            if (next.Count == 0)
            {
                sb.AppendLine("  （无 / 或在等计时）");
                int hr = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                sb.AppendLine($"  诊断: HandItemId={hr}（≤0 视为空手） Cache={Managers.Device?.Cache?.Count ?? 0}");
                if (Managers.Device?.Cache != null)
                {
                    foreach (var d in Managers.Device.Cache.Values)
                    {
                        if (d?.Info == null || d.Info.MissionType <= 0) continue;
                        var st0 = (d.Info.StateList != null && d.Info.StateList.Count > 0) ? d.Info.StateList[0].ToString() : "-";
                        sb.AppendLine($"  设备#{d.ID} Type={d.DeviceType} Sub={d.Data?.SubType} Mt={d.Info.MissionType} St0={st0} Bubble={d.Info.Bubble}");
                    }
                }
            }
            else foreach (var a in next) sb.AppendLine($"  {a}");
            sb.AppendLine();
            sb.AppendLine("  /agent all | /agent <任务ID|名> | /agent #<设备号> | /agent stop");
            return sb.ToString();
        }
    }
}
