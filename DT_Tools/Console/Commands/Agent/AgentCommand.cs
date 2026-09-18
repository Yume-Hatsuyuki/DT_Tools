using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;
using Protocol;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// /agent — 按任务链推进；一步做不了就跳过做别的。
    /// 优先级：无等待/仓储获取物/采矿 &gt; 设备生成 &gt; 计时类 &gt; 摇酒(移动)最后。
    /// 手空时「顺手牵羊」获取仓储/地面任务道具。
    ///
    /// 实现拆分说明（原单文件 ~1244 行，逻辑未改动，仅按职责拆到本目录内的文件）：
    ///   AgentTypes.cs         — Pri / AgentFilter / AgentStep / AddDel
    ///   AgentItemHelper.cs    — 道具/矿石/花静态工具方法
    ///   AgentChainHelper.cs   — 任务链关联 RelatedTo / 扫描仪颜色查询
    ///   AgentDeliveryChain.cs — 交付链（原 CollectDeliveries）
    ///   AgentVacuumChain.cs   — 顺手牵羊（原 CollectVacuum）
    ///   AgentInstantChain.cs  — 无道具直清（原 CollectInstant）
    ///   AgentGenerateChain.cs — 生成源（原 CollectGenerate，含挖矿冷却字段）
    ///   AgentPlanner.cs       — PlanAll/Next/Peek 调度
    ///   AgentRunner.cs        — MonoBehaviour tick 驱动
    /// </summary>
    internal sealed class AgentCommand : IConsoleCommand
    {
        public string   Name        => "agent";
        public string[] Aliases     => new[] { "特工", "任务", "mission" };
        public string   Usage       => "agent [all|任务ID|名|#设备号|stop]";
        public string   Description => "特工：按任务链自动推进（一步做不完就跳过做下一个）。";
        public string   Author      => "梦初雪";

        private static readonly Dictionary<string, int> AliasMap =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "morse", 37 }, { "摩斯", 37 }, { "摩斯密码", 37 },
                { "surgery", 1 }, { "手术", 1 },
                { "spray", 2 }, { "喷雾", 2 }, { "合成喷雾", 2 },
                { "miner", 3 }, { "矿工", 3 },
                { "candle", 4 }, { "蜡烛", 4 },
                { "collector", 6 }, { "收集", 6 },
                { "mineralcraft", 7 }, { "放矿", 7 },
                { "craft", 8 }, { "合成", 8 },
                { "essence", 9 }, { "分离", 9 },
                { "microscope", 10 }, { "显微镜", 10 },
                { "bookrune", 11 }, { "符文书", 11 },
                { "rune", 12 }, { "符文", 12 },
                { "warp", 13 }, { "传送", 13 },
                { "boiler", 14 }, { "制冰", 14 }, { "锅炉", 14 },
                { "drink", 15 }, { "调酒", 15 },
                { "shaker", 16 }, { "摇酒", 16 },
                { "fixpc", 17 }, { "修电脑", 17 }, { "电脑", 17 },
                { "manikin", 18 }, { "人体模型", 18 }, { "假人", 18 },
                { "spraycancer", 20 }, { "杀菌", 20 },
                { "potionalchemist", 21 }, { "炼金药", 21 },
                { "shakerdrink", 22 }, { "献酒", 22 },
                { "charge", 23 }, { "充电", 23 },
                { "battery", 24 }, { "满电", 24 },
                { "batteryminer", 25 }, { "矿工电池", 25 },
                { "batterywarp", 26 }, { "传送电池", 26 },
                { "batterybio", 27 }, { "生物电池", 27 },
                { "mushroom", 28 }, { "蘑菇", 28 },
                { "mushroomalchemist", 30 }, { "蘑菇炼金", 30 },
                { "nintendo", 34 }, { "任天堂", 34 },
                { "potion", 35 }, { "药剂", 35 },
                { "fire", 36 }, { "火焰", 36 },
                { "fish", 40 }, { "钓鱼", 40 },
            };

        public void Execute(string[] args, WebConsole console)
        {
            if (args.Length > 0 && (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase)
                                    || args[0] == "停止"))
            {
                AgentRunner.Stop(console);
                return;
            }

            if (WeaponPacketHelper.RequireLocalPlayer(console) == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return;
            }
            if (Managers.Game == null || Managers.Game.State != EGameState.Survive)
            {
                console.Log($"仅生存阶段可用，当前: {(Managers.Game == null ? "(null)" : Managers.Game.State.ToString())}。",
                    LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not survive\"}");
                return;
            }
            if (Managers.Game.IsSpectator)
            {
                console.Log("观战无法完成任务。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"spectator\"}");
                return;
            }
            if (Managers.Device?.Cache == null || Managers.Device.Cache.Count == 0)
            {
                console.Log("设备缓存为空。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no device\"}");
                return;
            }

            AgentFilter filter = default;
            if (args.Length > 0
                && !string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase)
                && args[0] != "全部" && args[0] != "全清")
            {
                if (!TryParseFilter(args[0], out filter))
                {
                    console.Log($"无法识别: {args[0]}\n  /agent 1 | /agent 手术 | /agent #10084", LogLevel.Warning);
                    console.SetResult("{\"ok\":false,\"error\":\"unknown\"}");
                    return;
                }
            }

            if (args.Length == 0)
            {
                var preview = AgentPlanner.Peek(filter);
                console.Log(BuildHelp(preview), LogLevel.Info);
                console.SetResult("{\"ok\":true,\"next\":" + preview.Count + "}");
                return;
            }

            AgentRunner.Start(console, filter);
            console.SetResult("{\"ok\":true,\"started\":true}");
        }

        private static bool TryParseFilter(string s, out AgentFilter filter)
        {
            filter = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s[0] == '#')
            {
                if (int.TryParse(s.Substring(1), out int did) && did > 0)
                {
                    filter = AgentFilter.ByDevice(did);
                    return true;
                }
                return false;
            }
            if (AliasMap.TryGetValue(s, out int aid))
            {
                filter = AgentFilter.ByMission(aid);
                return true;
            }
            if (int.TryParse(s, out int mid) && mid > 0)
            {
                filter = AgentFilter.ByMission(mid);
                return true;
            }
            return false;
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
