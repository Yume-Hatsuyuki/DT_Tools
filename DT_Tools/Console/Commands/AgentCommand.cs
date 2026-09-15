using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;
using Protocol;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /agent [任务名|任务ID|all]
    ///
    /// 特工：完成任务，快速精准（跟随控制台 / 客户端）。
    ///
    /// 实现原理（按《分析MOD客户端功能数据包》第 7 节"任务"伪造路径）：
    ///   服务端 MissionManager.ClearMission 只校验任务在 ProgressMissionList 中，
    ///   不验证小游戏结果 / 操作者身份 / 距离。以下 6 类任务可单包 / 多包直清：
    ///
    ///     - ScMorseCode(37)：Mission(SubType=18=MorseMission) 设备，
    ///       StateList[0]!=0 时直接 InteractMorse → ClearMission（无需道具、无需输码）。
    ///       发包：C_INTERACT_MISSION{MissionId}。
    ///       见 0.1.14b/Server.Game/Mission.cs:219-236。
    ///
    ///     - ScSurgery(1)：Mission(SubType=4=SurgeryMission) 设备，
    ///       StateList[0]==2 且 MissionType>0 时直接 InteractSurgeryMission 第二阶段 → ClearMission（无需道具）。
    ///       发包：C_INTERACT_MISSION{MissionId}。
    ///       见 0.1.14b/Server.Game/Mission.cs:97-118。
    ///
    ///     - ScWarp(13)：Warp(SubType=0=WarpScience) 设备，
    ///       StateList[2]==1 且 MissionType>0 时 InteractMain → ClearMission（无需完成编程小游戏）。
    ///       发包：C_INTERACT_WARP{WarpId, Index=0}。
    ///       见 0.1.14b/Server.Game/Warp.cs:73-100。
    ///
    ///     - ScNintendo(34)：Nintendo 设备 MissionType==34 且 StateList[7]!=1，
    ///       服务端 PackmanHandleEvent 不校验发包者是否为 PlayingPlayer，
    ///       金币顺序被打包在 StateList[1]（每 4 bit 一枚，低位在前，范围 2..5）。
    ///       按 CoinOrder 顺序连发 C_HANDLE_NINTENDO 即可远程完成。
    ///       见 0.1.14b/Server.Game/Nintendo.cs:106-168。
    ///
    ///     - ScMicroscope(10)【二阶段】：Sample(SubType=1=Microscope) 设备，
    ///       StateList[0]!=0 且 MissionType==10 时 InteractMicroscope → ClearMission。
    ///       服务端不校验 player.Hand，也不校验客户端是否真的看过显微镜弹窗。
    ///       发包：C_INTERACT_SAMPLE{SampleId}。
    ///       见 0.1.14b/Server.Game/Sample.cs:93-108。
    ///       前提：一阶段 ScEssence（Separator 分离机，需 10s JobTimer）已清。
    ///
    ///     - ScCraft(8)【二阶段】：Craft 设备 MissionType==8 且 StateList[0]==0（一阶段已清），
    ///       服务端 HandleEvent 只看包里的 IsSuccess 字段，不校验小游戏真的成功。
    ///       发包：C_HANDLE_CRAFT{CraftId, IsSuccess=true}。
    ///       见 0.1.14b/Server.Game/Craft.cs:60-79。
    ///       前提：一阶段 ScMineralCraft（需手持对应矿物道具）已清。
    ///
    /// 服务端 InteractLock（500ms）：
    ///   DeviceManager.Interact 完成一次交互后 player.InteractLock=true，
    ///   由 PushAfter(500) 自动复位。C_INTERACT_MISSION / C_INTERACT_WARP / C_INTERACT_SAMPLE
    ///   走 Interact 入口，故"扫荡全部"时这些任务每个间隔 0.6s。
    ///   C_HANDLE_NINTENDO / C_HANDLE_CRAFT 走 HandleEvent 入口（无锁），可连发。
    ///
    /// 权限 / 前置条件（跟随控制台 / 客户端）：
    ///   - 本机已进入对局，且 Managers.Network.GameServer 链路可用
    ///   - 游戏阶段 == Survive（DeviceManager.Interact / HandleEvent 均拒绝非生存阶段）
    ///   - 本机非观战（Managers.Game.IsSpectator）
    ///
    /// 示例:
    ///   /agent              列出当前可完成的任务
    ///   /agent morse        完成摩斯密码
    ///   /agent 37           按 ESchoolMission ID 完成摩斯密码
    ///   /agent nintendo     完成任天堂吃豆人
    ///   /agent microscope   完成显微镜二阶段
    ///   /agent craft        完成合成二阶段
    ///   /agent all          扫荡全部可完成任务
    /// </summary>
    internal sealed class AgentCommand : IConsoleCommand
    {
        public string   Name        => "agent";
        public string[] Aliases     => new[] { "特工", "清任务", "complete_mission" };
        public string   Usage       => "agent [任务名|任务ID|all]";
        public string   Description => "特工：完成任务，快速精准。无参数列出可完成任务，all 扫荡全部。";
        public string   Author      => "梦初雪";

        // 服务端 InteractLock 500ms，留 100ms 余量
        private const float InteractLockSpacing = 0.6f;

        // 中文 / 英文别名 → ESchoolMission ID
        private static readonly Dictionary<string, int> AliasMap =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "morse",     37 }, { "摩斯",     37 }, { "摩斯密码",   37 }, { "scmorsecode", 37 },
                { "surgery",    1 }, { "手术",      1 }, { "scsurgery",   1 },
                { "warp",      13 }, { "传送",     13 }, { "scwarp",     13 },
                { "nintendo",  34 }, { "任天堂",   34 }, { "packman",    34 }, { "吃豆人", 34 }, { "scnintendo", 34 },
                { "microscope", 10 }, { "显微镜", 10 }, { "scmicroscope", 10 },
                { "craft",       8 }, { "合成",    8 }, { "合成二阶段", 8 }, { "sccraft",      8 }
            };

        public void Execute(string[] args, WebConsole console)
        {
            // 1. 校验本机玩家 + 网络链路
            if (WeaponPacketHelper.RequireLocalPlayer(console) == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return;
            }
            // 2. 校验生存阶段
            if (Managers.Game == null || Managers.Game.State != EGameState.Survive)
            {
                console.Log($"仅生存阶段可用，当前状态: {(Managers.Game == null ? "(null)" : Managers.Game.State.ToString())}。",
                    LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not survive\"}");
                return;
            }
            // 3. 观战不可（DeviceManager.Interact/HandleEvent 拒绝 IsSpectator 或非存活）
            if (Managers.Game.IsSpectator)
            {
                console.Log("观战玩家无法完成任务（服务端会拒绝）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"spectator\"}");
                return;
            }
            // 4. 设备缓存
            var deviceMgr = Managers.Device;
            if (deviceMgr == null || deviceMgr.Cache == null || deviceMgr.Cache.Count == 0)
            {
                console.Log("地图尚未加载或没有设备数据（请进入对局后再试）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no device cache\"}");
                return;
            }

            // 5. 扫描可完成任务
            var completables = ScanCompletableMissions();

            // 6. 无参数 → 列表 + 帮助
            if (args.Length == 0)
            {
                console.Log(BuildHelpAndList(completables), LogLevel.Info);
                console.SetResult("{\"ok\":true,\"completable\":" + completables.Count + "}");
                return;
            }

            string input = args[0];

            // 7. all / 全部 / 全清 → 扫荡
            if (string.Equals(input, "all", StringComparison.OrdinalIgnoreCase)
                || input == "全部" || input == "全清")
            {
                ScheduleSweepAll(completables, console);
                return;
            }

            // 8. 解析任务名 / ID
            if (completables.Count == 0)
            {
                console.Log("当前没有可完成的任务（任务未激活 / 已完成 / 状态不满足）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no completable\"}");
                return;
            }
            if (!TryParseMission(input, out int missionType))
            {
                console.Log($"未知任务: {input}（输入 /agent 查看可完成任务列表）", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"unknown mission\"}");
                return;
            }
            var target = completables.FirstOrDefault(m => m.MissionType == missionType);
            if (target == null)
            {
                console.Log($"任务 {missionType} 当前不可完成（未激活 / 已完成 / 状态不满足）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not completable\"}");
                return;
            }

            // 9. 立即完成单个任务（无前序发包，InteractLock 已复位）
            SendPackets(target);
            console.Log($"[特工] 已对 {target.DisplayName}#{target.DeviceId} 发包完成（{target.Method}）。",
                LogLevel.Message);
            console.SetResult("{\"ok\":true,\"completed\":" + target.MissionType + "}");
        }

        // ── 扫描当前可完成的任务 ─────────────────────────────
        private static List<CompletableMission> ScanCompletableMissions()
        {
            var result = new List<CompletableMission>();
            foreach (var dev in Managers.Device.Cache.Values)
            {
                if (dev == null || dev.Info == null || dev.Data == null) continue;
                var info = dev.Info;
                var states = info.StateList;
                if (states == null || states.Count == 0) continue;

                // Mission 设备（Surgery / Morse / Cancer / Alchemist）
                if (dev.DeviceType == EDeviceType.Mission)
                {
                    int subType = dev.Data.SubType;

                    // Surgery: SubType==4(SurgeryMission), MissionType>0(=1), StateList[0]==2
                    if (subType == (int)EMissionType.SurgeryMission
                        && info.MissionType > 0
                        && states[0] == 2)
                    {
                        result.Add(new CompletableMission
                        {
                            DeviceId    = dev.ID,
                            MissionType = (int)ESchoolMission.ScSurgery,    // 1
                            DisplayName = "手术",
                            Method      = CompletionMethod.InteractMission
                        });
                    }
                    // Morse: SubType==18(MorseMission), MissionType==37, StateList[0]!=0
                    else if (subType == (int)EMissionType.MorseMission
                             && info.MissionType == (int)ESchoolMission.ScMorseCode
                             && states[0] != 0)
                    {
                        result.Add(new CompletableMission
                        {
                            DeviceId    = dev.ID,
                            MissionType = (int)ESchoolMission.ScMorseCode,  // 37
                            DisplayName = "摩斯密码",
                            Method      = CompletionMethod.InteractMission
                        });
                    }
                }
                // Warp 设备（仅 WarpScience 可清任务）
                else if (dev.DeviceType == EDeviceType.Warp)
                {
                    if (dev.Data.SubType == (int)EWarpType.WarpScience
                        && info.MissionType == (int)ESchoolMission.ScWarp
                        && states.Count > 2 && states[2] == 1)
                    {
                        result.Add(new CompletableMission
                        {
                            DeviceId    = dev.ID,
                            MissionType = (int)ESchoolMission.ScWarp,      // 13
                            DisplayName = "传送",
                            Method      = CompletionMethod.InteractWarp
                        });
                    }
                }
                // Nintendo 设备
                else if (dev.DeviceType == EDeviceType.Nintendo)
                {
                    if (info.MissionType == (int)ESchoolMission.ScNintendo
                        && states.Count > 7 && states[7] != 1)
                    {
                        // CoinOrder 打包在 StateList[1]：每 4 bit 一枚，低位在前，范围 2..5
                        var coinOrder = UnpackCoinOrder(states.Count > 1 ? states[1] : 0);
                        if (coinOrder.Count > 0)
                        {
                            result.Add(new CompletableMission
                            {
                                DeviceId    = dev.ID,
                                MissionType = (int)ESchoolMission.ScNintendo, // 34
                                DisplayName = "任天堂",
                                Method      = CompletionMethod.HandleNintendo,
                                CoinOrder   = coinOrder
                            });
                        }
                    }
                }
                // Sample 设备（仅显微镜二阶段 ScMicroscope 可直清；分离机 ScEssence 需 10s JobTimer）
                else if (dev.DeviceType == EDeviceType.Sample)
                {
                    if (dev.Data.SubType == (int)ESampleType.Microscope    // 1
                        && info.MissionType == (int)ESchoolMission.ScMicroscope  // 10
                        && states[0] != 0)
                    {
                        result.Add(new CompletableMission
                        {
                            DeviceId    = dev.ID,
                            MissionType = (int)ESchoolMission.ScMicroscope, // 10
                            DisplayName = "显微镜",
                            Method      = CompletionMethod.InteractSample
                        });
                    }
                }
                // Craft 设备（仅二阶段 ScCraft 可直清；一阶段 ScMineralCraft 需手持矿物道具）
                else if (dev.DeviceType == EDeviceType.Craft)
                {
                    if (info.MissionType == (int)ESchoolMission.ScCraft    // 8
                        && states.Count > 0 && states[0] == 0)             // 一阶段已清
                    {
                        result.Add(new CompletableMission
                        {
                            DeviceId    = dev.ID,
                            MissionType = (int)ESchoolMission.ScCraft,    // 8
                            DisplayName = "合成",
                            Method      = CompletionMethod.HandleCraft
                        });
                    }
                }
            }
            return result.OrderBy(m => m.MissionType).ToList();
        }

        // ── 解包 Nintendo CoinOrder ──────────────────────────
        // 服务端 PackCoinOrder：num |= (coinOrder[i] & 0xF) << i*4
        // 反向解包：coin = (packed >> i*4) & 0xF，coin==0 表示结束
        private static List<int> UnpackCoinOrder(int packed)
        {
            var list = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                int coin = (packed >> (i * 4)) & 0xF;
                if (coin == 0) break;
                list.Add(coin);
            }
            return list;
        }

        // ── 按完成方式发包 ───────────────────────────────────
        private static void SendPackets(CompletableMission m)
        {
            switch (m.Method)
            {
                case CompletionMethod.InteractMission:
                    Managers.Network.GameServer.Send(new C_INTERACT_MISSION { MissionId = m.DeviceId });
                    break;

                case CompletionMethod.InteractWarp:
                    Managers.Network.GameServer.Send(new C_INTERACT_WARP { WarpId = m.DeviceId, Index = 0 });
                    break;

                case CompletionMethod.InteractSample:
                    Managers.Network.GameServer.Send(new C_INTERACT_SAMPLE { SampleId = m.DeviceId });
                    break;

                case CompletionMethod.HandleNintendo:
                    // 4 个金币连发：HandleEvent 路径无 InteractLock，服务端按 _currentIndex 顺序校验
                    foreach (var coin in m.CoinOrder)
                    {
                        Managers.Network.GameServer.Send(new C_HANDLE_NINTENDO
                        {
                            NintendoId = m.DeviceId,
                            Coin       = coin
                        });
                    }
                    break;

                case CompletionMethod.HandleCraft:
                    // HandleEvent 路径无 InteractLock；服务端只看 IsSuccess 字段，不校验小游戏结果
                    Managers.Network.GameServer.Send(new C_HANDLE_CRAFT
                    {
                        CraftId    = m.DeviceId,
                        IsSuccess  = true
                    });
                    break;
            }
        }

        // ── 扫荡全部（按 InteractLock 间隔调度） ─────────────
        private void ScheduleSweepAll(List<CompletableMission> missions, WebConsole console)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"【特工·扫荡】开始调度 {missions.Count} 个任务：");
            for (int i = 0; i < missions.Count; i++)
            {
                sb.AppendLine($"  [{i + 1}] {missions[i].DisplayName}#{missions[i].DeviceId} ({missions[i].Method})");
            }
            sb.Append($"每个任务间隔 {InteractLockSpacing}s 以避开服务端 500ms InteractLock。");
            console.Log(sb.ToString(), LogLevel.Info);

            for (int i = 0; i < missions.Count; i++)
            {
                int idx = i;            // 闭包捕获
                var m = missions[i];
                float delay = i * InteractLockSpacing;
                Managers.Instance.DoActionAfter(delay, () =>
                {
                    try
                    {
                        SendPackets(m);
                        console.Log($"[特工·扫荡] ({idx + 1}/{missions.Count}) 已完成 {m.DisplayName}#{m.DeviceId}",
                            LogLevel.Message);
                    }
                    catch (Exception ex)
                    {
                        console.Log($"[特工·扫荡] #{m.DeviceId} 异常: {ex.Message}", LogLevel.Error);
                    }
                });
            }
            console.SetResult("{\"ok\":true,\"scheduled\":" + missions.Count + "}");
        }

        // ── 帮助 + 列表 ─────────────────────────────────────
        private static string BuildHelpAndList(List<CompletableMission> missions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 特工 · 任务清单 ━━━");
            sb.AppendLine("说明：直接发包完成任务，无需到达现场、无需小游戏结果。");
            sb.AppendLine("支持 6 类任务（含 4 个二阶段），无道具门禁：");
            sb.AppendLine("  摩斯密码 / 手术二阶段 / 传送 / 任天堂 / 显微镜二阶段 / 合成二阶段");
            sb.AppendLine();
            sb.AppendLine("【当前可完成的任务】");
            if (missions.Count == 0)
            {
                sb.AppendLine("  （无）— 任务未激活 / 已完成 / 不在生存阶段 / 一阶段未清");
            }
            else
            {
                for (int i = 0; i < missions.Count; i++)
                {
                    var m = missions[i];
                    sb.AppendLine($"  #{i + 1,-2} {m.DisplayName,-10} ScId={m.MissionType,-3} 设备 #{m.DeviceId,-4} 方式={m.Method}");
                }
            }
            sb.AppendLine();
            sb.AppendLine("【用法】");
            sb.AppendLine("  /agent                列出当前可完成的任务");
            sb.AppendLine("  /agent <任务名>       完成指定任务");
            sb.AppendLine("  /agent all            扫荡全部可完成任务");
            sb.AppendLine();
            sb.AppendLine("【任务别名】");
            sb.AppendLine("  摩斯密码 / morse / 37          → ScMorseCode");
            sb.AppendLine("  手术 / surgery / 1             → ScSurgery (二阶段)");
            sb.AppendLine("  传送 / warp / 13               → ScWarp");
            sb.AppendLine("  任天堂 / nintendo / 34        → ScNintendo");
            sb.AppendLine("  显微镜 / microscope / 10      → ScMicroscope (二阶段)");
            sb.AppendLine("  合成 / craft / 8               → ScCraft (二阶段)");
            sb.AppendLine("  all / 全部 / 全清              → 扫荡全部");
            return sb.ToString();
        }

        // ── 解析任务输入 ────────────────────────────────────
        private static bool TryParseMission(string s, out int missionType)
        {
            if (AliasMap.TryGetValue(s, out missionType)) return true;
            if (int.TryParse(s, out int n)
                && (n == 1 || n == 8 || n == 10 || n == 13 || n == 34 || n == 37))
            {
                missionType = n;
                return true;
            }
            if (Enum.TryParse(s, ignoreCase: true, out ESchoolMission e)
                && Enum.IsDefined(typeof(ESchoolMission), e)
                && (e == ESchoolMission.ScMorseCode || e == ESchoolMission.ScSurgery
                    || e == ESchoolMission.ScWarp || e == ESchoolMission.ScNintendo
                    || e == ESchoolMission.ScMicroscope || e == ESchoolMission.ScCraft))
            {
                missionType = (int)e;
                return true;
            }
            missionType = 0;
            return false;
        }

        // ── 内部数据类型 ───────────────────────────────────
        private enum CompletionMethod
        {
            InteractMission,   // C_INTERACT_MISSION（受 InteractLock 限制）
            InteractWarp,       // C_INTERACT_WARP（受 InteractLock 限制）
            InteractSample,     // C_INTERACT_SAMPLE（受 InteractLock 限制）
            HandleNintendo,     // 4x C_HANDLE_NINTENDO（无 InteractLock）
            HandleCraft         // C_HANDLE_CRAFT{IsSuccess=true}（无 InteractLock）
        }

        private sealed class CompletableMission
        {
            public int              DeviceId;
            public int              MissionType;     // ESchoolMission
            public string           DisplayName;
            public CompletionMethod Method;
            public List<int>        CoinOrder;        // 仅 Nintendo 用
        }
    }
}
