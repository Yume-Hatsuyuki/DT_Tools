using System.Collections.Generic;
using DT_Tools.Commands;

namespace DT_Tools.Commands.ScanAll
{
    /// <summary>
    /// /scan_all &lt;间隔秒&gt; — 知晓一切：本机玩家无视距离 / 泡泡状态，对所有可扫描设备
    /// （设备 / 尸体 / 武器架）直接发 C_SCAN_DEVICE 收集全部线索（跟随控制台，非房主可用）。
    /// 通常用于调查阶段：生存阶段 ClueList 已被 StartSurvival 清空，扫描不会返回线索。
    /// 间隔 0=同帧一次性发完（秒发包）；&gt;0=每包间隔该秒数；不带参数显示用法。
    /// ⚠ 多人局警示：死亡玩家也能扫描（服务端只查观战），线索归属会与正常玩法不一致
    /// 且事后无法区分是否命令所为，慎用。
    /// </summary>
    internal sealed class ScanAllCommand : ICommand
    {
        public string Name => "scan_all";
        public string[] Aliases => new[] { "知晓一切", "知晓一切之人", "omniscient", "全扫描" };
        public string Usage => "scan_all <间隔秒>";
        public string Description => "知晓一切之人：对所有可扫描设备发 C_SCAN_DEVICE 收集全部线索（跟随控制台）。间隔 0=同帧一次发完，>0=每包间隔秒数，不带参数显示用法。⚠ 多人局：死亡玩家也可扫描且事后无法区分，慎用。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply(
                    "用法: /scan_all <间隔秒>\n" +
                    "  0    = 同帧一次性发完全部 C_SCAN_DEVICE（最快拿全线索）\n" +
                    "  0.2  = 每包间隔 0.2 秒（分帧错峰，多人局建议）\n" +
                    "  范围 0~60 秒。示例: /scan_all 0、/scan_all 0.5");
                return CommandResult.Fail("missing interval");
            }
            if (!ScanAllArgs.TryParseInterval(ctx.Args[0], out float interval, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("invalid interval");
            }

            if (!ScanAllLogic.TryGetLocalContext(out var device, out string ctxCode, out string ctxText))
            {
                ctx.Reply(ctxText);
                return CommandResult.Fail(ctxCode);
            }

            if (ScanAllLogic.IsSpectator)
            {
                ctx.Reply("观战玩家无法扫描设备（服务端会拒绝 IsSpectator）。");
                return CommandResult.Fail("spectator");
            }

            var targets = ScanAllLogic.CollectScannable(device);
            if (targets.Count == 0)
            {
                ctx.Reply("当前没有可扫描的设备（不在调查阶段 / 已全部扫描 / 设备无线索）。");
                return CommandResult.Fail("no scannable");
            }

            ScanAllLogic.StartPacketsCoroutine(targets, interval, ctx);
            ctx.Reply(ScanAllFormat.Reply(targets, interval));
            return CommandResult.Success(ScanAllFormat.Result(targets));
        }
    }
}
