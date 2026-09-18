using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;
using Protocol;
using UnityEngine;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /agent — 按任务链推进；一步做不了就跳过做别的。
    /// 优先级：无等待/仓储获取物/采矿 &gt; 设备生成 &gt; 计时类 &gt; 摇酒(移动)最后。
    /// 手空时「顺手牵羊」获取仓储/地面任务道具。
    /// </summary>
    internal sealed class AgentCommand : IConsoleCommand
    {
        public string   Name        => "agent";
        public string[] Aliases     => new[] { "特工", "任务", "mission" };
        public string   Usage       => "agent [all|任务ID|名|#设备号|stop]";
        public string   Description => "特工：按任务链自动推进（一步做不完就跳过做下一个）。";
        public string   Author      => "梦初雪";

        private const float TickInterval = 0.6f;
        private const int   MaxIdleTicks = 12;
        private const int   MaxTicks     = 300;

        /// <summary>挖矿后冷却 tick 数，强制先捡再挖，避免空挖刷屏。</summary>
        private static int _mineralMineCooldown;

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
                var preview = Planner.Peek(filter);
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

        // ═══════════════════════════════════════════════════
        //  优先级：数字越小越先做
        // ═══════════════════════════════════════════════════
        private static class Pri
        {
            public const int Vacuum     = 10;  // 获取仓储/地面
            public const int Instant    = 20;  // 无道具直清
            public const int Deliver    = 30;  // 手里有货交付
            public const int Generate   = 40;  // 假人/采矿/取电
            public const int TimedStart = 50;  // 浇水/充电放入/制冰启动（之后要等）
            public const int ShakeLast  = 90;  // 摇酒相关最后
        }

        internal static class Planner
        {
            public static List<string> Peek(AgentFilter filter) =>
                PlanAll(filter).Select(s => s.Label).ToList();

            public static AgentStep? Next(AgentFilter filter)
            {
                var list = PlanAll(filter);
                return list.Count > 0 ? list[0] : (AgentStep?)null;
            }

            private static List<AgentStep> PlanAll(AgentFilter filter)
            {
                var steps = new List<AgentStep>();
                // 注意：原版空手时 HandItemId == -1，不是 0
                int handRaw = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                int hand = handRaw > 0 ? handRaw : 0;
                // ItemHolder（地上掉落）为临时设备，常无 DeviceData → Data==null，不能过滤掉
                var devices = Managers.Device.Cache.Values
                    .Where(d => d?.Info != null && (d.Data != null || d.DeviceType == EDeviceType.ItemHolder))
                    .ToList();

                void Add(int priority, string label, int? mission, int deviceId, Action send)
                {
                    if (filter.MissionId.HasValue && mission.HasValue && mission.Value != filter.MissionId.Value)
                        return;
                    if (filter.DeviceId.HasValue && deviceId != 0 && deviceId != filter.DeviceId.Value)
                        return;

                    string display = mission.HasValue
                        ? $"[P{priority}] ID {mission.Value} · {label}"
                        : $"[P{priority}] ID - · {label}";
                    steps.Add(new AgentStep { Priority = priority, Label = display, Send = send });
                }

                // ── A0. 手上杂物（鱼等）先丢掉，避免卡住后续任务 ──
                if (hand > 0 && ShouldDropHand(hand, devices))
                {
                    Add(Pri.Vacuum - 5, $"丢弃手上{hand}", null, 0, () => TryDropHand());
                    // 本 tick 只丢，不叠加其它依赖空手的步骤
                    return steps.OrderBy(s => s.Priority).ToList();
                }

                // ── A. 手持 → 交付（高优先，清掉手上再干别的）──
                if (hand > 0)
                    CollectDeliveries(hand, devices, Add);

                // ── B. 手空：顺手牵羊（仓储/地面）──
                if (hand == 0)
                    CollectVacuum(devices, filter, Add);

                // ── C. 无道具直清 / 状态机 ──
                CollectInstant(devices, Add);

                // ── D. 手空：生成源（假人、采矿、取电、花…）──
                if (hand == 0)
                    CollectGenerate(devices, filter, Add);

                return steps.OrderBy(s => s.Priority).ToList();
            }

            // ──────────── 交付 ────────────
            private static void CollectDeliveries(int hand, List<DeviceBase> devices, AddDel Add)
            {
                foreach (var dev in devices)
                {
                    if (dev.Data == null) continue;
                    var st = dev.Info.StateList;
                    int sub = dev.Data.SubType;
                    int mt = dev.Info.MissionType;
                    int bubble = dev.Info.Bubble;
                    int id = dev.ID;

                    if (hand == 1009 && dev.DeviceType == EDeviceType.Mission
                        && sub == (int)EMissionType.SurgeryMission
                        && st != null && st.Count > 0 && st[0] == 0)
                    {
                        Add(Pri.Deliver, $"交假人→手术台#{id}", (int)ESchoolMission.ScManikinStart, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }));
                    }
                    if (hand == 1008 && dev.DeviceType == EDeviceType.Computer
                        && st != null && st.Count > 1 && st[1] == 2)
                    {
                        Add(Pri.Deliver, $"交螺丝刀→电脑#{id}", (int)ESchoolMission.ScFixPc, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_COMPUTER { ComputerId = id }));
                    }
                    if (hand == 1025 && dev.DeviceType == EDeviceType.Mission
                        && sub == (int)EMissionType.CancerMission)
                    {
                        Add(Pri.Deliver, $"交喷雾→癌台#{id}", (int)ESchoolMission.ScSprayCancer, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }));
                    }
                    if (hand == 1030 && dev.DeviceType == EDeviceType.Mission
                        && sub == (int)EMissionType.AlchemistMission && bubble == 1030)
                    {
                        Add(Pri.Deliver, $"交炼金药→任务板#{id}", (int)ESchoolMission.ScPotionAlchemist, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }));
                    }
                    if (hand == 1011 && dev.DeviceType == EDeviceType.Charger
                        && st != null && st.Count > 2 && st[0] == 0 && st[2] == 10)
                    {
                        // 放入后要等充电计时 → TimedStart
                        Add(Pri.TimedStart, $"放入空电→充电器#{id}", (int)ESchoolMission.ScChargeBattery, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_CHARGER { ChargerId = id }));
                    }
                    if (hand == 1015)
                    {
                        if (dev.DeviceType == EDeviceType.Miner
                            && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)
                        {
                            Add(Pri.Deliver, $"放电池→矿工#{id}", (int)ESchoolMission.ScBatteryMiner, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_MINER { MinerId = id, Color = 0 }));
                        }
                        if (dev.DeviceType == EDeviceType.Warp && sub == (int)EWarpType.WarpScience
                            && bubble == 1015)
                        {
                            Add(Pri.Deliver, $"放电池→传送#{id}", (int)ESchoolMission.ScBatteryWarp, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_WARP { WarpId = id, Index = 0 }));
                        }
                        if (dev.DeviceType == EDeviceType.Bio
                            && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)
                        {
                            Add(Pri.Deliver, $"放电池→Bio#{id}", (int)ESchoolMission.ScBatteryBio, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_BIO { BioId = id }));
                        }
                    }
                    if (hand == 1026 && dev.DeviceType == EDeviceType.Alchemist && sub == 2
                        && st != null && st.Count > 1 && st[0] == 1 && st[1] == 0 && bubble == 1026)
                    {
                        Add(Pri.Deliver, $"放蘑菇→炼金锅#{id}", (int)ESchoolMission.ScMushroomAlchemist, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_ALCHEMIST { AlchemistId = id }));
                    }
                    if ((hand == 1051 || hand == 1052))
                    {
                        if (dev.DeviceType == EDeviceType.Rune && sub == (int)ERuneType.RuneStand
                            && st != null && st.Count > 4 && st[0] == 1 && st[1] == 0 && st[4] == hand)
                        {
                            Add(Pri.Deliver, $"放书→符文台#{id}", (int)ESchoolMission.ScBookRune, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_RUNE { RuneId = id }));
                        }
                        if (dev.DeviceType == EDeviceType.Occult && sub == (int)EOccultType.OccultFire
                            && st != null && st.Count > 1 && st[1] == 1 && bubble == hand)
                        {
                            Add(Pri.Deliver, $"交书→火焰#{id}", (int)ESchoolMission.ScFire, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_OCCULT { OccultId = id }));
                        }
                    }
                    if (hand >= 1021 && hand <= 1024)
                    {
                        if (dev.DeviceType == EDeviceType.Cancer && sub == 1
                            && st != null && st.Count >= 6 && st[0] == 1 && st[1] == 0)
                        {
                            int slot = -1;
                            if (st[4] == hand && st[2] == 0) slot = 0;
                            else if (st[5] == hand && st[3] == 0) slot = 1;
                            if (slot >= 0)
                            {
                                int s = slot;
                                Add(Pri.Deliver, $"放花→喷雾台#{id}槽{s}", (int)ESchoolMission.ScMakeSpray, id,
                                    () => Managers.Network.GameServer.Send(new C_INTERACT_CANCER { CancerId = id, Index = s }));
                            }
                        }
                        if (dev.DeviceType == EDeviceType.Drink && sub == 0
                            && st != null && st.Count >= 6 && st[0] == 1 && st[1] == 0 && mt > 0)
                        {
                            bool match = (st[2] == hand && st[4] == 0) || (st[3] == hand && st[5] == 0);
                            if (match)
                            {
                                Add(Pri.Deliver, $"放花→调酒台#{id}", (int)ESchoolMission.ScDrink, id,
                                    () => Managers.Network.GameServer.Send(new C_INTERACT_DRINK { DrinkId = id }));
                            }
                        }
                    }
                    if (hand >= 1032 && hand <= 1034)
                    {
                        if (dev.DeviceType == EDeviceType.Craft && mt == (int)ESchoolMission.ScMineralCraft
                            && st != null && st.Count > 0 && st[0] == hand)
                        {
                            Add(Pri.Deliver, $"放矿→工艺台#{id}", (int)ESchoolMission.ScMineralCraft, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_CRAFT { CraftId = id }));
                        }
                        if (dev.DeviceType == EDeviceType.Collector
                            && st != null && st.Count >= 7 && st[0] == 1)
                        {
                            int type = MineralTypeFromDataId(hand);
                            if (CollectorRemain(st, type) > 0)
                            {
                                Add(Pri.Deliver, $"放矿→收集柜#{id}(余{CollectorRemain(st, type)})",
                                    (int)ESchoolMission.ScCollector, id,
                                    () => Managers.Network.GameServer.Send(new C_INTERACT_COLLECTOR { CollectorId = id }));
                            }
                            // 该色已交满：不交付，留给 ShouldDropHand 丢掉，避免失败死循环
                        }
                    }
                    if (hand == 1028 && dev.DeviceType == EDeviceType.Boiler
                        && sub == (int)EBoilerType.AdjustBoiler && st != null && st[0] == 1)
                    {
                        Add(Pri.Deliver, $"交热水→温控#{id}", (int)ESchoolMission.ScBoiler, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_BOILER { BoilerId = id }));
                    }
                    if (hand >= 1046 && hand <= 1049 && dev.DeviceType == EDeviceType.Potion && sub == 0
                        && st != null && st.Count >= 6 && st[0] == 1 && st[5] == 0
                        && mt == (int)ESchoolMission.ScPotion)
                    {
                        int color = hand - 1045;
                        int need = st[3] == 0 ? st[1] : (st[4] == 0 ? st[2] : 0);
                        if (need > 0 && color == need)
                        {
                            Add(Pri.Deliver, $"放药→扫描仪#{id}", (int)ESchoolMission.ScPotion, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_POTION { PotionId = id }));
                        }
                    }
                    // 献酒：最后优先级（依赖移动摇出的 1040）
                    if (hand == 1040 && dev.DeviceType == EDeviceType.Drink && sub == 1
                        && st != null && st.Count > 1 && st[0] == 1 && st[1] != 1)
                    {
                        Add(Pri.ShakeLast, $"献酒→雕像#{id}", (int)ESchoolMission.ScShakerDrink, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_DRINK { DrinkId = id }));
                    }
                    // 手持 1039：无法发包变 1040，仅提示性不发包（摇酒靠玩家走）
                }
            }

            // ──────────── 顺手牵羊 ────────────
            private static void CollectVacuum(List<DeviceBase> devices, AgentFilter filter, AddDel Add)
            {
                // 当前仍需要的矿色（工艺台 + 收集柜缺口）
                var needMinerals = new HashSet<int>();
                foreach (var d in devices)
                {
                    if (d.DeviceType == EDeviceType.Craft
                        && d.Info.MissionType == (int)ESchoolMission.ScMineralCraft
                        && d.Info.StateList != null && d.Info.StateList.Count > 0)
                    {
                        int req = d.Info.StateList[0];
                        if (req >= 1032 && req <= 1034) needMinerals.Add(req);
                    }
                }
                if (needMinerals.Count == 0)
                {
                    foreach (var d in devices)
                    {
                        if (d.DeviceType != EDeviceType.Collector) continue;
                        var cst = d.Info.StateList;
                        if (cst == null || cst.Count < 7 || cst[0] != 1) continue;
                        for (int type = 0; type < 3; type++)
                        {
                            if (CollectorRemain(cst, type) > 0)
                                needMinerals.Add(MineralDataIdFromType(type));
                        }
                    }
                }

                // 地面
                foreach (var dev in devices)
                {
                    if (dev.DeviceType != EDeviceType.ItemHolder) continue;
                    var st = dev.Info.StateList;
                    if (st == null || st.Count == 0 || st[0] <= 0) continue;
                    int dataId = st[0];
                    if (!IsMissionItem(dataId)) continue;
                    if (filter.MissionId.HasValue && !ItemMatchesMission(dataId, filter.MissionId.Value))
                        continue;
                    // 矿石：只捡仍缺的颜色（已交满的蓝不再捡）
                    if (dataId >= 1032 && dataId <= 1034 && needMinerals.Count > 0
                        && !needMinerals.Contains(dataId))
                        continue;

                    int objId = dev.ID;
                    int? rel = MissionOfItem(dataId);
                    int pri = (dataId == 1039 || dataId == 1040) ? Pri.ShakeLast : Pri.Vacuum;
                    if (dataId >= 1032 && dataId <= 1034)
                        pri = Pri.Vacuum - 4;
                    Add(pri, $"获取地面{dataId}#{objId}", rel, objId,
                        () => Managers.Network.GameServer.Send(new C_ACQUIRE_ITEM { ItemId = objId }));
                }

                // 仓储（每 tick 只计划第一个，避免 Lock）
                foreach (var dev in devices)
                {
                    if (dev.DeviceType != EDeviceType.Storage) continue;
                    var st = dev.Info.StateList;
                    if (st == null) continue;
                    for (int i = 0; i < st.Count; i++)
                    {
                        int itemId = st[i];
                        if (itemId == 0 || !IsMissionItem(itemId)) continue;
                        if (filter.MissionId.HasValue && !ItemMatchesMission(itemId, filter.MissionId.Value))
                            continue;

                        int id = dev.ID;
                        int idx = i;
                        int item = itemId;
                        int pri = Pri.Vacuum;
                        // 书/螺丝刀/空电/蘑菇 优先获取
                        if (item == 1051 || item == 1052 || item == 1008 || item == 1011 || item == 1026)
                            pri = Pri.Vacuum - 1;

                        Add(pri, $"获取仓储{item}#{id}[{idx}]", MissionOfItem(item), id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_STORAGE
                            {
                                StorageId = id,
                                Index = idx
                            }));
                        return; // 只获取一个
                    }
                }
            }

            // ──────────── 无道具直清 ────────────
            private static void CollectInstant(List<DeviceBase> devices, AddDel Add)
            {
                foreach (var dev in devices)
                {
                    if (dev.Data == null) continue;
                    var st = dev.Info.StateList;
                    if (st == null || st.Count == 0) continue;
                    int sub = dev.Data.SubType;
                    int mt = dev.Info.MissionType;
                    int id = dev.ID;

                    if (dev.DeviceType == EDeviceType.Mission)
                    {
                        if (sub == (int)EMissionType.SurgeryMission && mt > 0 && st[0] == 2)
                            Add(Pri.Instant, $"手术二阶段#{id}", (int)ESchoolMission.ScSurgery, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }));
                        if (sub == (int)EMissionType.MorseMission && mt == 37 && st[0] != 0)
                            Add(Pri.Instant, $"摩斯#{id}", (int)ESchoolMission.ScMorseCode, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = id }));
                    }
                    if (dev.DeviceType == EDeviceType.Warp && sub == (int)EWarpType.WarpScience
                        && mt == 13 && st.Count > 2 && st[2] == 1)
                        Add(Pri.Instant, $"传送解码#{id}", (int)ESchoolMission.ScWarp, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_WARP { WarpId = id, Index = 0 }));

                    if (dev.DeviceType == EDeviceType.Nintendo && mt == 34 && st.Count > 7 && st[7] != 1)
                    {
                        var coins = UnpackCoins(st.Count > 1 ? st[1] : 0);
                        if (coins.Count > 0)
                        {
                            var order = coins.ToList();
                            Add(Pri.Instant, $"任天堂#{id}", (int)ESchoolMission.ScNintendo, id, () =>
                            {
                                foreach (var c in order)
                                    Managers.Network.GameServer.Send(new C_HANDLE_NINTENDO { NintendoId = id, Coin = c });
                            });
                        }
                    }
                    if (dev.DeviceType == EDeviceType.Sample)
                    {
                        if (sub == (int)ESampleType.Microscope && mt == 10 && st[0] != 0)
                            Add(Pri.Instant, $"显微镜#{id}", (int)ESchoolMission.ScMicroscope, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_SAMPLE { SampleId = id }));
                        if (sub == (int)ESampleType.Separator && mt == 9 && st[0] == 1 && st.Count > 1 && st[1] == 0)
                            Add(Pri.TimedStart, $"分离机启动#{id}", (int)ESchoolMission.ScEssence, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_SAMPLE { SampleId = id }));
                    }
                    if (dev.DeviceType == EDeviceType.Craft && mt == 8 && st[0] == 0)
                        Add(Pri.Instant, $"工艺完成#{id}", (int)ESchoolMission.ScCraft, id,
                            () => Managers.Network.GameServer.Send(new C_HANDLE_CRAFT { CraftId = id, IsSuccess = true }));

                    if (dev.DeviceType == EDeviceType.Mushroom && mt == 28 && st.Count >= 5 && st[0] == 1)
                    {
                        if (st[3] == 0)
                        {
                            int idx = st[1];
                            Add(Pri.Instant, $"蘑菇Index{idx}#{id}", (int)ESchoolMission.ScMakeMushroom, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_MUSHROOM { MushroomId = id, Index = idx }));
                        }
                        else if (st[4] == 0)
                        {
                            int idx = st[2];
                            Add(Pri.Instant, $"蘑菇Index{idx}#{id}", (int)ESchoolMission.ScMakeMushroom, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_MUSHROOM { MushroomId = id, Index = idx }));
                        }
                    }
                    if (dev.DeviceType == EDeviceType.Occult && sub == (int)EOccultType.OccultBook
                        && mt == 4 && st.Count >= 6)
                    {
                        int[] cids = { 10121, 10122, 10123, 10124, 10125, 10126 };
                        for (int i = 0; i < 6; i++)
                        {
                            int target = st[i];
                            var candle = devices.FirstOrDefault(d => d.ID == cids[i]);
                            if (candle?.Info?.StateList == null || candle.Info.StateList.Count < 1) continue;
                            int cur = candle.Info.StateList[0];
                            if (cur == 3 || cur == -1 || cur == target) continue;
                            int cid = cids[i];
                            Add(Pri.Instant, $"翻蜡烛#{cid}({cur}→{target})", (int)ESchoolMission.ScCandle, cid,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_OCCULT { OccultId = cid }));
                            break;
                        }
                    }
                    if (dev.DeviceType == EDeviceType.Rune && sub == (int)ERuneType.RuneStand
                        && mt == 12 && st.Count > 1 && st[1] != 0)
                        Add(Pri.Instant, $"完成符文#{id}", (int)ESchoolMission.ScRune, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_RUNE { RuneId = id }));

                    if (dev.DeviceType == EDeviceType.Fishing && st.Count >= 4 && mt == 40)
                    {
                        // 仅 MissionType==40 时推进；清任务后 mt=0，不会再刷
                        // 收杆会 CreateAndInsertInven 鱼(1059/60/61) → 下 tick 自动丢弃
                        int phase = st[1];
                        int handChk = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                        if (handChk < 0) handChk = 0;
                        if (phase == 0 && handChk == 0)
                            Add(Pri.Instant, $"钓鱼开始#{id}", 40, id,
                                () => Managers.Network.GameServer.Send(new C_HANDLE_FISHING { FishingId = id, IsPlaying = true, IsSuccess = false }));
                        else if (phase == 1)
                            Add(Pri.Instant, $"钓鱼咬钩#{id}", 40, id,
                                () => Managers.Network.GameServer.Send(new C_HANDLE_FISHING { FishingId = id, IsPlaying = true, IsSuccess = true }));
                        else if (phase == 2)
                            Add(Pri.Instant, $"钓鱼收杆#{id}", 40, id,
                                () => Managers.Network.GameServer.Send(new C_HANDLE_FISHING { FishingId = id, IsPlaying = false, IsSuccess = true }));
                    }
                    if (dev.DeviceType == EDeviceType.Miner && mt == 3
                        && st.Count >= 5 && st[0] == 1 && st[1] == 5)
                    {
                        int color = st[3];
                        if (st[2] == 0)
                            Add(Pri.Instant, $"矿工抬杆#{id}", 3, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_MINER { MinerId = id, Color = 0 }));
                        else
                            Add(Pri.Instant, $"矿工放杆色{color}#{id}", 3, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_MINER { MinerId = id, Color = color }));
                    }
                }
            }

            // ──────────── 生成源（一步做不了就只做能做的）────────────
            private static void CollectGenerate(List<DeviceBase> devices, AgentFilter filter, AddDel Add)
            {
                bool Want(int mission) =>
                    !filter.MissionId.HasValue || filter.MissionId.Value == mission
                    || RelatedTo(filter.MissionId.Value, mission);

                // ── 矿物需求 ──
                // 工艺台：StateList[0]=所需 DataId（唯一色）
                // 收集柜：State[1]=红需求 State[2]=绿 State[3]=蓝；State[4..6]=已交类型(0红/1绿/2蓝)或-1
                //         缺口 = 需求 - 已交该色次数；只挖/交仍缺的颜色
                var neededMineralIds = new HashSet<int>();
                foreach (var d in devices)
                {
                    if (d.DeviceType == EDeviceType.Craft
                        && d.Info.MissionType == (int)ESchoolMission.ScMineralCraft
                        && d.Info.StateList != null && d.Info.StateList.Count > 0)
                    {
                        int req = d.Info.StateList[0];
                        if (req >= 1032 && req <= 1034)
                            neededMineralIds.Add(req);
                    }
                }
                bool craftLocked = neededMineralIds.Count > 0;
                if (!craftLocked)
                {
                    foreach (var d in devices)
                    {
                        if (d.DeviceType != EDeviceType.Collector) continue;
                        var cst = d.Info.StateList;
                        if (cst == null || cst.Count < 7 || cst[0] != 1) continue;
                        for (int type = 0; type < 3; type++)
                        {
                            int remain = CollectorRemain(cst, type);
                            if (remain > 0)
                                neededMineralIds.Add(MineralDataIdFromType(type));
                        }
                    }
                }

                int handNow = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                if (handNow < 0) handNow = 0;

                bool haveNeededOnHand = handNow >= 1032 && handNow <= 1034
                    && (neededMineralIds.Count == 0 || neededMineralIds.Contains(handNow));
                bool anyMineralOnGround = devices.Any(d =>
                    d.DeviceType == EDeviceType.ItemHolder
                    && d.Info.StateList != null && d.Info.StateList.Count > 0
                    && d.Info.StateList[0] >= 1032 && d.Info.StateList[0] <= 1034);
                bool haveNeededOnGround = devices.Any(d =>
                    d.DeviceType == EDeviceType.ItemHolder
                    && d.Info.StateList != null && d.Info.StateList.Count > 0
                    && d.Info.StateList[0] >= 1032 && d.Info.StateList[0] <= 1034
                    && (neededMineralIds.Count == 0 || neededMineralIds.Contains(d.Info.StateList[0])));

                // 只有真正算出缺口才挖；不要用 Want()（无 filter 时 Want 恒 true）
                bool needMineral = neededMineralIds.Count > 0;

                if (_mineralMineCooldown > 0)
                    _mineralMineCooldown--;

                // 花：仅当喷雾台/调酒台仍缺花槽时才浇/采（按目标 DataId）
                var neededFlowerIds = new HashSet<int>();
                foreach (var d in devices)
                {
                    if (d.Data == null) continue;
                    var dst = d.Info.StateList;
                    if (dst == null) continue;
                    int dmt = d.Info.MissionType;
                    // 喷雾合成台 Cancer SubType=1：State[4]/[5] 目标花，State[2]/[3]==0 表示空槽
                    if (d.DeviceType == EDeviceType.Cancer && d.Data.SubType == 1
                        && dmt == (int)ESchoolMission.ScMakeSpray && dst.Count >= 6 && dst[0] == 1)
                    {
                        if (dst[2] == 0 && dst[4] >= 1021 && dst[4] <= 1024)
                            neededFlowerIds.Add(dst[4]);
                        if (dst[3] == 0 && dst[5] >= 1021 && dst[5] <= 1024)
                            neededFlowerIds.Add(dst[5]);
                    }
                    // 调酒台 Drink SubType=0：State[2]/[3] 目标，State[4]/[5] 是否已放
                    if (d.DeviceType == EDeviceType.Drink && d.Data.SubType == 0
                        && dmt == (int)ESchoolMission.ScDrink && dst.Count >= 6 && dst[0] == 1)
                    {
                        if (dst[4] == 0 && dst[2] >= 1021 && dst[2] <= 1024)
                            neededFlowerIds.Add(dst[2]);
                        if (dst[5] == 0 && dst[3] >= 1021 && dst[3] <= 1024)
                            neededFlowerIds.Add(dst[3]);
                    }
                }
                bool needFlower = neededFlowerIds.Count > 0;

                bool minePlanned = false;

                foreach (var dev in devices)
                {
                    if (dev.Data == null) continue; // 跳过无 Data 的临时物（掉落由 Vacuum 处理）
                    var st = dev.Info.StateList;
                    int sub = dev.Data.SubType;
                    int mt = dev.Info.MissionType;
                    int id = dev.ID;

                    // 假人：Harvest 可采，或任意设备 MissionType==18
                    if (Want((int)ESchoolMission.ScManikinStart))
                    {
                        bool isHarvest = dev.DeviceType == EDeviceType.Harvest && sub == 0
                            && st != null && st.Count > 0
                            && (st[0] == 1 || mt == (int)ESchoolMission.ScManikinStart);
                        bool byMission = mt == (int)ESchoolMission.ScManikinStart
                            && (dev.DeviceType == EDeviceType.Harvest || st != null && st.Count > 0 && st[0] != 0);
                        if (isHarvest || byMission)
                        {
                            Add(Pri.Generate, $"采人体模型#{id}", (int)ESchoolMission.ScManikinStart, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_HARVEST { HarvestId = id }));
                        }
                    }

                    // 采矿：地上有矿或冷却中则不挖；每 tick 最多 1 次
                    if (!minePlanned
                        && needMineral
                        && !haveNeededOnHand
                        && !haveNeededOnGround
                        && !anyMineralOnGround
                        && _mineralMineCooldown <= 0
                        && dev.DeviceType == EDeviceType.Mineral)
                    {
                        int produced = MineralDataId(sub);
                        if (neededMineralIds.Count == 0 || neededMineralIds.Contains(produced))
                        {
                            int mid = id;
                            int prod = produced;
                            Add(Pri.Generate, $"采矿{prod}#{mid}", (int)ESchoolMission.ScMineralCraft, mid,
                                () =>
                                {
                                    _mineralMineCooldown = 5;
                                    Managers.Network.GameServer.Send(new C_HANDLE_MINERAL
                                    {
                                        MineralId = mid,
                                        IsSuccess = true
                                    });
                                });
                            minePlanned = true;
                        }
                    }

                    // 取满电
                    if (dev.DeviceType == EDeviceType.Charger
                        && st != null && st.Count > 2 && st[0] == 1 && st[2] == 0)
                    {
                        Add(Pri.Generate, $"取满电#{id}", (int)ESchoolMission.ScBattery, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_CHARGER { ChargerId = id }));
                    }

                    // 花：仅缺花时；优先采已长好的、且 SubType 对应的 DataId 在缺口里
                    if (needFlower && dev.DeviceType == EDeviceType.Flower && st != null && st.Count > 1)
                    {
                        // Flower SubType → 花 DataId：常见 0→1021 … 需与地图一致；用 Data.SubType 映射
                        int flowerItem = FlowerItemId(sub);
                        bool thisNeeded = flowerItem > 0 && neededFlowerIds.Contains(flowerItem);
                        // 缺口未映射到具体花时，仍允许操作任意花盆（兼容）
                        if (!thisNeeded && neededFlowerIds.Count > 0 && flowerItem > 0)
                        {
                            // 若所有缺口都不匹配任何已知映射，允许浇任意空盆
                            // 仅当 flowerItem 完全对不上任一缺口时跳过采摘
                        }
                        if (st[0] == 2)
                        {
                            // 可采：手上已有花则先交/丢，不采
                            int handChk = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                            if (handChk > 0) { }
                            else if (thisNeeded || neededFlowerIds.Count == 0)
                            {
                                int fid = id;
                                Add(Pri.Generate, $"采花{flowerItem}#{fid}", (int)ESchoolMission.ScMakeSpray, fid,
                                    () => Managers.Network.GameServer.Send(new C_INTERACT_FLOWER { FlowerId = fid }));
                            }
                        }
                        else if (st[0] == 0 && (thisNeeded || neededFlowerIds.Count > 0))
                        {
                            // 浇水：只浇缺口对应的花盆；若映射失败则仍浇（让花长出来再采）
                            if (thisNeeded || flowerItem == 0)
                            {
                                int fid = id;
                                Add(Pri.TimedStart, $"浇水花{flowerItem}#{fid}", (int)ESchoolMission.ScMakeSpray, fid,
                                    () => Managers.Network.GameServer.Send(new C_INTERACT_FLOWER { FlowerId = fid }));
                            }
                        }
                    }

                    // 炼金取药
                    if (dev.DeviceType == EDeviceType.Alchemist && sub == 2
                        && st != null && st.Count > 1 && st[0] == 1 && st[1] == 2
                        && mt == (int)ESchoolMission.ScPotionAlchemist)
                    {
                        Add(Pri.Generate, $"取炼金药#{id}", (int)ESchoolMission.ScPotionAlchemist, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_ALCHEMIST { AlchemistId = id }));
                    }

                    // 制冰：启动 Timed；可取则 Generate
                    if (dev.DeviceType == EDeviceType.Boiler && sub == (int)EBoilerType.IceMaker)
                    {
                        if (st != null && st[0] == 3)
                            Add(Pri.Generate, $"取热水#{id}", (int)ESchoolMission.ScBoiler, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_BOILER { BoilerId = id }));
                        else if (mt == 14 && st != null && st[0] == 1)
                            Add(Pri.TimedStart, $"制冰启动#{id}", (int)ESchoolMission.ScBoiler, id,
                                () => Managers.Network.GameServer.Send(new C_INTERACT_BOILER { BoilerId = id }));
                    }

                    // 调酒台取 1039（摇酒链）— 低优先
                    if (dev.DeviceType == EDeviceType.Drink && sub == 0
                        && st != null && st.Count > 1 && st[1] == 1
                        && mt == (int)ESchoolMission.ScShakeShaker)
                    {
                        Add(Pri.ShakeLast, $"取酒1039#{id}", (int)ESchoolMission.ScShakeShaker, id,
                            () => Managers.Network.GameServer.Send(new C_INTERACT_DRINK { DrinkId = id }));
                    }

                    // 药剂色台：InteractColor 每次都会进手一瓶且 State 不关，
                    // 必须只在「扫描仪仍缺该色」且空手时取一次
                    if (dev.DeviceType == EDeviceType.Potion && sub >= 1 && sub <= 4
                        && st != null && st.Count > 0 && st[0] == 1)
                    {
                        int handChk = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
                        if (handChk < 0) handChk = 0;
                        if (handChk != 0) { }
                        else
                        {
                            int needColor = ScannerNextNeededColor(devices); // 1..4，0=不需要
                            // SubType: Red=1 Green=2 Blue=3 Yellow=4 与 HandPotionColor 一致
                            if (needColor > 0 && sub == needColor)
                            {
                                int pid = id;
                                Add(Pri.Generate, $"取药色{needColor}←色台#{pid}", (int)ESchoolMission.ScPotion, pid,
                                    () => Managers.Network.GameServer.Send(new C_INTERACT_POTION { PotionId = pid }));
                            }
                        }
                    }
                }
            }

            private static bool RelatedTo(int filterMission, int stepMission)
            {
                // 同一条链上的步骤互认
                int[][] chains =
                {
                    new[] { 18, 1 },
                    new[] { 2, 20 },
                    new[] { 28, 30, 21 },
                    new[] { 23, 24, 25, 26, 27, 3, 13 },
                    new[] { 7, 8 },
                    new[] { 11, 12 },
                    new[] { 15, 16, 22 },
                };
                foreach (var c in chains)
                {
                    if (c.Contains(filterMission) && c.Contains(stepMission))
                        return true;
                }
                return filterMission == stepMission;
            }

            /// <summary>
            /// 扫描仪下一个需要的颜色 1红 2绿 3蓝 4黄；0=任务未开或已放齐。
            /// State[1]/[2]=目标色，State[3]/[4]=已放，State[5]=完成。
            /// </summary>
            private static int ScannerNextNeededColor(List<DeviceBase> devices)
            {
                foreach (var d in devices)
                {
                    if (d.Data == null || d.DeviceType != EDeviceType.Potion || d.Data.SubType != 0)
                        continue;
                    var st = d.Info.StateList;
                    if (st == null || st.Count < 6) continue;
                    if (d.Info.MissionType != (int)ESchoolMission.ScPotion && st[0] != 1)
                        continue;
                    if (st[0] != 1 || st[5] != 0) continue;
                    if (st[3] == 0) return st[1];
                    if (st[4] == 0) return st[2];
                    return 0;
                }
                return 0;
            }

            /// <summary>矿点 SubType → 掉落 DataId（与 Server Mineral.HandleEvent 一致）</summary>
            private static int FlowerItemId(int subType)
            {
                // 与客户端 Flower 产出一致：SubType 0..3 → 1021..1024
                if (subType >= 0 && subType <= 3)
                    return 1021 + subType;
                return 0;
            }

            private static int MineralDataId(int subType)
            {
                switch ((EMineralType)subType)
                {
                    case EMineralType.RedMineral: return 1034;
                    case EMineralType.GreenMineral: return 1033;
                    case EMineralType.BlueMineral: return 1032;
                    default: return 1032;
                }
            }

            private static int MineralDataIdFromType(int type)
            {
                // 0红 1绿 2蓝 — 与 EMineralType / Collector.StartMission 一致
                switch (type)
                {
                    case 0: return 1034;
                    case 1: return 1033;
                    case 2: return 1032;
                    default: return 1032;
                }
            }

            private static int MineralTypeFromDataId(int dataId)
            {
                if (dataId == 1034) return 0;
                if (dataId == 1033) return 1;
                if (dataId == 1032) return 2;
                return -1;
            }

            /// <summary>
            /// 收集柜某色剩余需求。State[1+type]=需求次数；State[4..6]==type 为已交。
            /// </summary>
            private static int CollectorRemain(IList<int> st, int type)
            {
                if (st == null || type < 0 || type > 2 || st.Count < 7) return 0;
                int need = st[1 + type];
                if (need <= 0) return 0;
                int done = 0;
                for (int k = 4; k <= 6 && k < st.Count; k++)
                {
                    if (st[k] == type) done++;
                }
                int remain = need - done;
                return remain > 0 ? remain : 0;
            }

            private static bool IsFish(int id) =>
                id == 1059 || id == 1060 || id == 1061; // Fishing 完成后的普通/稀有/金鱼

            private static bool IsMissionItem(int id) =>
                id == 1008 || id == 1009 || id == 1011 || id == 1015
                || id == 1025 || id == 1026 || id == 1028 || id == 1030
                || (id >= 1021 && id <= 1024)
                || (id >= 1032 && id <= 1034)
                || (id >= 1046 && id <= 1049)
                || id == 1051 || id == 1052 || id == 1039 || id == 1040;
            // 注意：鱼 1059-1061 不是任务道具，不获取、要丢

            private static bool ItemMatchesMission(int itemId, int missionId)
            {
                var m = MissionOfItem(itemId);
                if (!m.HasValue) return true;
                return RelatedTo(missionId, m.Value) || m.Value == missionId;
            }

            private static int? MissionOfItem(int itemId)
            {
                switch (itemId)
                {
                    case 1008: return (int)ESchoolMission.ScFixPc;
                    case 1009: return (int)ESchoolMission.ScManikinStart;
                    case 1011: return (int)ESchoolMission.ScChargeBattery;
                    case 1015: return (int)ESchoolMission.ScBattery;
                    case 1025: return (int)ESchoolMission.ScSprayCancer;
                    case 1026: return (int)ESchoolMission.ScMushroomAlchemist;
                    case 1028: return (int)ESchoolMission.ScBoiler;
                    case 1030: return (int)ESchoolMission.ScPotionAlchemist;
                    case 1032:
                    case 1033:
                    case 1034: return (int)ESchoolMission.ScMineralCraft;
                    case 1039: return (int)ESchoolMission.ScShakeShaker;
                    case 1040: return (int)ESchoolMission.ScShakerDrink;
                    case 1051:
                    case 1052: return (int)ESchoolMission.ScBookRune;
                    default:
                        if (itemId >= 1021 && itemId <= 1024) return (int)ESchoolMission.ScMakeSpray;
                        if (itemId >= 1046 && itemId <= 1049) return (int)ESchoolMission.ScPotion;
                        return null;
                }
            }


            /// <summary>
            /// 手上物品是否应丢弃：鱼；或当前没有任何设备能接收/需要它。
            /// 任务链上的道具（假人/书/矿/花…）在仍有交付目标时保留。
            /// </summary>
            private static bool ShouldDropHand(int hand, List<DeviceBase> devices)
            {
                if (hand <= 0) return false;
                if (IsFish(hand)) return true;

                // 仍有交付目标则保留
                if (HasDeliveryTarget(hand, devices)) return false;

                // 仍被任务需要（如工艺台要的矿还没交）则保留
                if (IsMissionItem(hand) && IsStillNeeded(hand, devices)) return false;

                // 非任务杂物
                if (!IsMissionItem(hand)) return true;

                return false;
            }

            private static bool HasDeliveryTarget(int hand, List<DeviceBase> devices)
            {
                foreach (var dev in devices)
                {
                if (dev.Data == null) continue;
                    var st = dev.Info.StateList;
                    int sub = dev.Data.SubType;
                    int mt = dev.Info.MissionType;
                    int bubble = dev.Info.Bubble;
                    if (hand == 1009 && dev.DeviceType == EDeviceType.Mission
                        && sub == (int)EMissionType.SurgeryMission && st != null && st.Count > 0 && st[0] == 0)
                        return true;
                    if (hand == 1008 && dev.DeviceType == EDeviceType.Computer && st != null && st.Count > 1 && st[1] == 2)
                        return true;
                    if (hand == 1025 && dev.DeviceType == EDeviceType.Mission && sub == (int)EMissionType.CancerMission)
                        return true;
                    if (hand == 1030 && dev.DeviceType == EDeviceType.Mission && bubble == 1030)
                        return true;
                    if (hand == 1011 && dev.DeviceType == EDeviceType.Charger && st != null && st.Count > 2 && st[0] == 0 && st[2] == 10)
                        return true;
                    if (hand == 1015 && ((dev.DeviceType == EDeviceType.Miner && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)
                        || (dev.DeviceType == EDeviceType.Warp && bubble == 1015)
                        || (dev.DeviceType == EDeviceType.Bio && st != null && st.Count > 1 && st[0] != 0 && st[1] == 0)))
                        return true;
                    if (hand == 1026 && dev.DeviceType == EDeviceType.Alchemist && bubble == 1026)
                        return true;
                    if ((hand == 1051 || hand == 1052) && (
                        (dev.DeviceType == EDeviceType.Rune && sub == (int)ERuneType.RuneStand && st != null && st.Count > 4 && st[1] == 0 && st[4] == hand)
                        || (dev.DeviceType == EDeviceType.Occult && sub == (int)EOccultType.OccultFire && bubble == hand)))
                        return true;
                    if (hand >= 1021 && hand <= 1024 && (
                        (dev.DeviceType == EDeviceType.Cancer && mt == (int)ESchoolMission.ScMakeSpray)
                        || (dev.DeviceType == EDeviceType.Drink && mt == (int)ESchoolMission.ScDrink)))
                        return true;
                    if (hand >= 1032 && hand <= 1034)
                    {
                        if (dev.DeviceType == EDeviceType.Craft && mt == (int)ESchoolMission.ScMineralCraft
                            && st != null && st.Count > 0 && st[0] == hand)
                            return true;
                        if (dev.DeviceType == EDeviceType.Collector && st != null && st.Count >= 7 && st[0] == 1
                            && CollectorRemain(st, MineralTypeFromDataId(hand)) > 0)
                            return true;
                    }
                    if (hand == 1028 && dev.DeviceType == EDeviceType.Boiler && sub == (int)EBoilerType.AdjustBoiler && st != null && st[0] == 1)
                        return true;
                    if (hand >= 1046 && hand <= 1049 && dev.DeviceType == EDeviceType.Potion
                        && dev.Data.SubType == 0 && mt == (int)ESchoolMission.ScPotion
                        && st != null && st.Count >= 6 && st[0] == 1 && st[5] == 0)
                    {
                        int color = hand - 1045; // 1046→1
                        int need = st[3] == 0 ? st[1] : (st[4] == 0 ? st[2] : 0);
                        if (color == need) return true;
                    }
                    if (hand == 1040 && dev.DeviceType == EDeviceType.Drink && sub == 1)
                        return true;
                }
                return false;
            }

            private static bool IsStillNeeded(int hand, List<DeviceBase> devices)
            {
                foreach (var d in devices)
                {
                if (d.Data == null) continue;
                    if (d.Info.Bubble == hand) return true;
                    if (d.DeviceType == EDeviceType.Craft && d.Info.MissionType == (int)ESchoolMission.ScMineralCraft
                        && d.Info.StateList != null && d.Info.StateList.Count > 0 && d.Info.StateList[0] == hand)
                        return true;
                    if (d.DeviceType == EDeviceType.Rune && d.Data.SubType == (int)ERuneType.RuneStand
                        && d.Info.StateList != null && d.Info.StateList.Count > 4 && d.Info.StateList[4] == hand)
                        return true;
                }
                return false;
            }

            private static void TryDropHand()
            {
                try
                {
                    var inv = Managers.Player?.MyPlayer?.Inventory;
                    if (inv != null && inv.Hand != null && inv.Hand.DataId != 0)
                    {
                        inv.DropHand();
                        return;
                    }
                    // 回退：直接发包（Item 可能为空，服务端用当前 Hand）
                    Managers.Network.GameServer.Send(new C_DROP_ITEM());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[特工] 丢弃失败: " + ex.Message);
                }
            }

            private static List<int> UnpackCoins(int packed)
            {
                var list = new List<int>();
                for (int i = 0; i < 8; i++)
                {
                    int c = (packed >> (i * 4)) & 0xF;
                    if (c == 0) break;
                    list.Add(c);
                }
                return list;
            }

            private delegate void AddDel(int priority, string label, int? mission, int deviceId, Action send);
        }

        internal struct AgentFilter
        {
            public int? MissionId;
            public int? DeviceId;
            public static AgentFilter ByMission(int id) => new AgentFilter { MissionId = id };
            public static AgentFilter ByDevice(int id) => new AgentFilter { DeviceId = id };
        }

        internal struct AgentStep
        {
            public int Priority;
            public string Label;
            public Action Send;
        }

        private sealed class AgentRunner : MonoBehaviour
        {
            public static AgentRunner Instance { get; private set; }
            private WebConsole _console;
            private AgentFilter _filter;
            private float _nextTick;
            private int _idle, _ticks, _done;

            public static void Start(WebConsole console, AgentFilter filter)
            {
                if (Instance != null)
                {
                    Instance._filter = filter;
                    Instance._idle = 0;
                    Instance._ticks = 0;
                    console.Log("[特工] 已在运行，已更新过滤并继续。", LogLevel.Info);
                    return;
                }
                var go = new GameObject("DT_AgentRunner");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<AgentRunner>();
                Instance._console = console;
                Instance._filter = filter;
                Instance._nextTick = Time.unscaledTime + 0.1f;

                string desc = filter.MissionId.HasValue ? $"任务 ScId={filter.MissionId}"
                    : filter.DeviceId.HasValue ? $"设备 #{filter.DeviceId}"
                    : "全清";
                console.Log($"[特工] 启动（{desc}），tick={TickInterval}s。一步做不了会跳过。/agent stop 停止。",
                    LogLevel.Message);
            }

            public static void Stop(WebConsole console)
            {
                if (Instance == null)
                {
                    console.Log("[特工] 未在运行。", LogLevel.Info);
                    return;
                }
                console.Log($"[特工] 已停止（完成 {Instance._done} 步）。", LogLevel.Message);
                Destroy(Instance.gameObject);
                Instance = null;
            }

            private void Update()
            {
                if (Time.unscaledTime < _nextTick) return;
                _nextTick = Time.unscaledTime + TickInterval;
                _ticks++;

                if (Managers.Game == null || Managers.Game.State != EGameState.Survive)
                {
                    _console?.Log("[特工] 离开生存阶段，停止。", LogLevel.Warning);
                    Stop(_console);
                    return;
                }

                var step = Planner.Next(_filter);
                if (step == null)
                {
                    _idle++;
                    if (_idle >= MaxIdleTicks || _ticks >= MaxTicks)
                    {
                        _console?.Log($"[特工] 结束：空闲 {_idle}，执行 {_done} 步 / {_ticks} tick。", LogLevel.Message);
                        Destroy(gameObject);
                        Instance = null;
                    }
                    return;
                }

                _idle = 0;
                try
                {
                    step.Value.Send();
                    _done++;
                    _console?.Log($"[特工] ({_done}) {step.Value.Label}", LogLevel.Message);
                }
                catch (Exception ex)
                {
                    _console?.Log($"[特工] 异常: {ex.Message}", LogLevel.Error);
                }
            }

            private void OnDestroy()
            {
                if (Instance == this) Instance = null;
            }
        }
    }
}
