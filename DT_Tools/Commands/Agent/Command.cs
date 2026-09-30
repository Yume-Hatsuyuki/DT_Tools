using System.Collections.Generic;
using System.Text;
using DT_Tools.Game;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// /agent [all|任务ID|名|#设备号|stop] — 特工：按任务链自动推进（一步做不完就跳过做下一个）。
    /// 优先级：无等待/仓储获取物/采矿 &gt; 设备生成 &gt; 计时类 &gt; 摇酒(移动)最后。
    /// 手空时「顺手牵羊」获取仓储/地面任务道具。
    ///
    /// 实现拆分（对应旧 11 文件，逻辑未改动）：
    ///   Args.cs               — 参数/别名解析
    ///   Logic.Types.cs        — Pri / AgentFilter / AgentStep / AddDel
    ///   Logic.ItemHelper.cs   — 道具/矿石/花工具 + 任务链关联 + 扫描仪颜色（旧 ItemHelper+ChainHelper）
    ///   Logic.MissionState.cs — 任务激活状态权威数据源（Host 反射 MissionManager / 客户端 MissionMirror）
    ///   Logic.DeliveryChain.cs— 交付链（原 CollectDeliveries）
    ///   Logic.VacuumChain.cs  — 顺手牵羊（原 CollectVacuum）
    ///   Logic.InstantChain.cs — 无道具直清（原 CollectInstant）
    ///   Logic.GenerateChain.cs— 生成源（原 CollectGenerate，含挖矿冷却字段）
    ///   Logic.Planner.cs      — PlanAll/Next/Peek 调度
    ///   Logic.Runner.cs       — MonoBehaviour tick 驱动
    /// </summary>
    internal sealed class AgentCommand : ICommand
    {
        public string Name => "agent";
        // 注意：旧版 AgentCommand 与 GiveMissionCommand 都声明了别名 "mission"
        //（旧注册表静默覆盖，新 CommandRegistry 会抛冲突异常），此处让出 "mission"，
        // 保留 "特工"/"任务"；如需归还请与 GiveMission 协调。
        public string[] Aliases => new[] { "特工", "任务" };
        public string Usage => "agent [all|任务ID|名|#设备号|stop]";
        public string Description => "特工：按任务链自动推进（一步做不完就跳过做下一个）。";
        public string Author => "梦初雪";

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
                // 无参：预览本 tick 可执行步骤 + 诊断（不启动 Runner）
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
            else foreach (var a in next) sb.AppendLine("  " + a);
            sb.AppendLine();
            sb.AppendLine("  /agent all | /agent <任务ID|名> | /agent #<设备号> | /agent stop");
            return sb.ToString();
        }
    }
}
