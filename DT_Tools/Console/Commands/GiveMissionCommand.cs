using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using Data;
using DT_Tools.Console.Commands.Phase;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /givemission &lt;任务类型&gt;
    ///
    /// 给所有玩家派发特定任务。仅房主、且游戏处于生存阶段（Survive）时可用。
    ///
    /// 实现原理：
    ///   原版任务派发由 MissionManager.MissionAllocator 在游戏开局时一次性触发，
    ///   运行中只能通过 ClearMission 完成任务后由链式 NextType 或队列自动推进。
    ///   本命令直接反射调用 private MissionManager.StartMission(MissionData)，
    ///   该方法会：
    ///     1. 把任务加入 ProgressMissionList
    ///     2. 若 PrevType!=0 递归启动硬前置（保证链完整）
    ///     3. 按 ESchoolMission switch 调用对应 Start_ScXXX()，激活设备 + BroadcastState
    ///   StartMission 本身不广播 S_MISSION_STATE（仅 ClearMission / MissionAllocator 末尾
    ///   才广播），所以命令额外反射调用 private BroadcastMissionState() 让客户端立即刷新
    ///   任务面板。
    ///
    /// 软前置联动初始化（智能派发）:
    ///   MissionData 的 PrevType 只记录"硬前置"（递归启动），但很多任务的设备初始化
    ///   依赖另一条已完成的任务链。例如:
    ///     ScRune(12)        需要 ScBookRune(11) 先跑 Start_ScBookRune 生成符文书
    ///     ScSurgery(1)      需要 ScManikinStart(18) 先跑 Start_ScManikinStart
    ///     ScSprayCancer(20) 需要 ScMakeSpray(2) 先跑 Start_ScMakeSpray 初始化 Compounder
    ///     ScShakerDrink(22) 需要 ScShakeShaker(16) 先跑，16 又需要 ScDrink(15)
    ///   这些关系在 MissionData 中通过 NextType 表达(11.NextType=12 表示完成 11 后启动 12)，
    ///   但 PrevType 字段并不记录(12.PrevType=0)，因为正常流程是玩家完成 11 后由
    ///   ClearMission 自动启动 12，不需要递归。
    ///
    ///   本命令构建反向 NextType 表(NextType→Type)，派发任务 T 时沿反向链收集所有
    ///   "软前置"，按链头→链尾顺序逐个调用 StartMission(P) 跑设备初始化，然后立即
    ///   从 ProgressMissionList 移除 P。这样:
    ///     - P 的 Start_ScXXX() 被调用，设备状态被正确初始化(生成物品/设置 StateList)
    ///     - P 不在 ProgressMissionList，玩家完成 P 的设备交互时 ClearMission(P) 找不到
    ///       任务直接 return(不计分、不触发 NextType 链)，避免 T 被重复 Add
    ///     - T 在 ProgressMissionList，玩家完成 T 时正常 ClearMission(T) 计分
    ///   达到"至少 50% 任务预期"——设备可达交互状态，计分走用户指定任务。
    ///
    ///   为避免队列残留导致下次 StartNextFromQueue 把任务再次启动，派发前先反射
    ///   调用 private RemoveFromWaitQueue(type) 清理队列。
    ///
    ///   MissionManager 在发行程序集中是 internal 类，所有访问(Instance、方法、
    ///   ProgressMissionList 属性)都必须走反射(与 Patch_FishingRandomItem 一致)。
    ///
    /// 任务类型输入支持:
    ///   - 枚举名(大小写不敏感): ScFixPc / scfixpc
    ///   - 数字: 17
    ///   - 中文别名: 修电脑、钓鱼 等
    ///
    /// 注意:
    ///   - ScNone(0) 不允许派发
    ///   - ScBattery(24) / ScMushroom(29) / ScMushroomBio(31) / ScBatteryMiner2(32) /
    ///     ScMiner2(33) / ScFusebox(38) / ScWeapon(39) 在原版 switch 中是空 case 分支，
    ///     派发后只会进入 ProgressMissionList 不会激活任何设备，仅供占位/调试
    ///   - 同一任务类型若已在 ProgressMissionList 中将拒绝重复派发
    ///
    /// 示例:
    ///   /givemission              ← 列出全部任务
    ///   /givemission ScFixPc
    ///   /givemission 17
    ///   /givemission 修电脑
    ///   /givemission ScRune       ← 自动联动初始化 ScBookRune(生成符文书)，只计 ScRune 分
    /// </summary>
    internal sealed class GiveMissionCommand : IConsoleCommand
    {
        public string   Name        => "givemission";
        public string[] Aliases     => new[] { "mission", "派发任务" };
        public string   Usage       => "givemission [任务类型]";
        public string   Description => "给所有玩家派发特定任务，自动联动初始化前置设备（仅房主、生存阶段可用）。不带参数时显示任务列表。";
        public string   Author      => "梦初雪";

        private const string MissionManagerTypeName = "Server.Game.MissionManager";

        /// <summary>switch 中无对应 Start_ScXXX 分支的占位任务类型。</summary>
        private static readonly HashSet<ESchoolMission> EmptyCaseMissions =
            new HashSet<ESchoolMission>
            {
                ESchoolMission.ScBattery,
                ESchoolMission.ScMushroom,
                ESchoolMission.ScMushroomBio,
                ESchoolMission.ScBatteryMiner2,
                ESchoolMission.ScMiner2,
                ESchoolMission.ScFusebox,
                ESchoolMission.ScWeapon,
            };

        private static readonly Dictionary<string, ESchoolMission> MissionAliases =
            new Dictionary<string, ESchoolMission>(StringComparer.OrdinalIgnoreCase)
            {
                { "手术",       ESchoolMission.ScSurgery },
                { "制喷雾",     ESchoolMission.ScMakeSpray },
                { "矿工",       ESchoolMission.ScMiner },
                { "蜡烛",       ESchoolMission.ScCandle },
                { "收集器",     ESchoolMission.ScCollector },
                { "矿物合成",   ESchoolMission.ScMineralCraft },
                { "合成",       ESchoolMission.ScCraft },
                { "精华",       ESchoolMission.ScEssence },
                { "显微镜",     ESchoolMission.ScMicroscope },
                { "符文之书",   ESchoolMission.ScBookRune },
                { "符文",       ESchoolMission.ScRune },
                { "传送",       ESchoolMission.ScWarp },
                { "锅炉",       ESchoolMission.ScBoiler },
                { "饮品",       ESchoolMission.ScDrink },
                { "摇摇杯",     ESchoolMission.ScShakeShaker },
                { "修电脑",     ESchoolMission.ScFixPc },
                { "假人启动",   ESchoolMission.ScManikinStart },
                { "喷癌症",     ESchoolMission.ScSprayCancer },
                { "药剂炼金",   ESchoolMission.ScPotionAlchemist },
                { "摇杯饮品",   ESchoolMission.ScShakerDrink },
                { "充电",       ESchoolMission.ScChargeBattery },
                { "电池矿工",   ESchoolMission.ScBatteryMiner },
                { "电池传送",   ESchoolMission.ScBatteryWarp },
                { "电池生物",   ESchoolMission.ScBatteryBio },
                { "制蘑菇",     ESchoolMission.ScMakeMushroom },
                { "蘑菇炼金",   ESchoolMission.ScMushroomAlchemist },
                { "任天堂",     ESchoolMission.ScNintendo },
                { "药剂",       ESchoolMission.ScPotion },
                { "火",         ESchoolMission.ScFire },
                { "摩斯密码",   ESchoolMission.ScMorseCode },
                { "钓鱼",       ESchoolMission.ScAquaticCapture },
            };

        private static readonly string MissionList = BuildMissionList();

        // 反射缓存
        private static Type _mmType;
        private static MethodInfo _getInstanceMethod;
        private static MethodInfo _startMissionMethod;
        private static MethodInfo _removeFromWaitQueueMethod;
        private static MethodInfo _broadcastMissionStateMethod;
        private static PropertyInfo _progressMissionListProperty;
        private static bool _methodLookupFailed;

        // 反向 NextType 表（NextType → [Type]），首次派发时构建
        private static Dictionary<int, List<int>> _reverseNextMap;
        private static bool _reverseMapBuilt;

        public void Execute(string[] args, WebConsole console)
        {
            if (args.Length == 0)
            {
                console.Log(MissionList, LogLevel.Info);
                return;
            }

            var room = PhaseJumpHelper.ValidateRoom(console);
            if (room == null) return;
            if (!PhaseJumpHelper.EnsureNotBusy(room, console)) return;

            if (room.State != EGameState.Survive)
            {
                console.Log($"只能在生存阶段（Survive）派发任务，当前状态: {room.State}。", LogLevel.Warning);
                return;
            }

            if (!EnsureMethodsCached(console))
                return;

            object mmInstance = _getInstanceMethod.Invoke(null, null);
            if (mmInstance == null)
            {
                console.Log("MissionManager.Instance 为 null，任务系统未初始化。", LogLevel.Warning);
                return;
            }

            string input = args[0];
            if (!TryParseMission(input, out ESchoolMission missionType))
            {
                console.Log($"未知任务: {input}（直接输入 /givemission 查看列表）", LogLevel.Warning);
                return;
            }

            if (missionType == ESchoolMission.ScNone)
            {
                console.Log("ScNone(0) 不是有效任务，不能派发。", LogLevel.Warning);
                return;
            }

            MissionData data = Managers.Data.GetMissionData((int)missionType);
            if (data == null)
            {
                console.Log($"任务类型 {missionType}({(int)missionType}) 没有对应 MissionData。", LogLevel.Warning);
                return;
            }

            var progressList = _progressMissionListProperty.GetValue(mmInstance) as IList;
            if (progressList == null)
            {
                console.Log("无法读取 MissionManager.ProgressMissionList。", LogLevel.Warning);
                return;
            }

            // 防重：ProgressMissionList 中已有该类型则拒绝
            if (ContainsMissionType(progressList, (int)missionType))
            {
                console.Log($"任务 {missionType}({(int)missionType}) 已在进行中，不能重复派发。", LogLevel.Warning);
                return;
            }

            // ── 软前置联动初始化 ──────────────────────────────
            // 沿反向 NextType 链收集所有软前置（链头→链尾顺序）
            var softPrereqs = CollectSoftPrerequisites((int)missionType);
            var initializedPrereqs = new List<int>();

            foreach (var pType in softPrereqs)
            {
                // 前置已在 ProgressMissionList → 设备已初始化，跳过
                if (ContainsMissionType(progressList, pType)) continue;

                MissionData pData = Managers.Data.GetMissionData(pType);
                if (pData == null) continue;

                // 从队列清理残留
                _removeFromWaitQueueMethod.Invoke(mmInstance, new object[] { pType });

                // 调用 StartMission(P) 跑设备初始化（会 Add 到 ProgressMissionList）
                _startMissionMethod.Invoke(mmInstance, new object[] { pData });

                // 立即从 ProgressMissionList 移除 P：仅做设备初始化，不计分
                // 玩家完成 P 的设备交互时 ClearMission(P) 找不到任务直接 return，
                // 不会触发 NextType 链重复启动 T
                progressList.Remove(pData);
                initializedPrereqs.Add(pType);
            }

            // ── 派发主任务 ────────────────────────────────────
            _removeFromWaitQueueMethod.Invoke(mmInstance, new object[] { (int)missionType });
            _startMissionMethod.Invoke(mmInstance, new object[] { data });
            _broadcastMissionStateMethod.Invoke(mmInstance, null);

            // ── 输出 ───────────────────────────────────────────
            var sb = new StringBuilder();
            sb.Append($"已派发任务 {missionType}({(int)missionType})");
            if (initializedPrereqs.Count > 0)
            {
                var names = initializedPrereqs.Select(t => $"{(ESchoolMission)t}({t})");
                sb.Append($"（已联动初始化前置设备: {string.Join(" → ", names)}）");
            }
            if (data.PrevType != 0) sb.Append($"（已联动启动硬前置 Type={data.PrevType}）");
            if (EmptyCaseMissions.Contains(missionType))
                sb.Append("。注意：该任务在原版 switch 中无 Start_ScXXX 分支，派发后无设备激活，仅占位");
            sb.Append("。");
            console.Log(sb.ToString(), LogLevel.Message);
        }

        /// <summary>
        /// 沿反向 NextType 链递归收集所有软前置，返回链头→链尾顺序的列表。
        /// 例如派发 ScShakerDrink(22): 22←16←15，返回 [15, 16]。
        /// </summary>
        private static List<int> CollectSoftPrerequisites(int type)
        {
            EnsureReverseNextMapBuilt();
            var result = new List<int>();
            var visited = new HashSet<int>();
            CollectSoftPrerequisitesCore(type, visited, result);
            return result;
        }

        private static void CollectSoftPrerequisitesCore(int type, HashSet<int> visited, List<int> result)
        {
            if (!visited.Add(type)) return;  // 防环

            if (_reverseNextMap == null) return;
            if (!_reverseNextMap.TryGetValue(type, out var predecessors)) return;

            foreach (var p in predecessors)
            {
                // 先递归收集 p 的上游前置（链头优先）
                CollectSoftPrerequisitesCore(p, visited, result);
                result.Add(p);
            }
        }

        /// <summary>
        /// 构建反向 NextType 表: NextType → [Type]。
        /// 例: MissionData{Type=11, NextType=12} → map[12] = [11]。
        /// 派发 12 时查 map[12] 得到 11（11 是 12 的软前置）。
        /// </summary>
        private static void EnsureReverseNextMapBuilt()
        {
            if (_reverseMapBuilt) return;
            _reverseMapBuilt = true;
            _reverseNextMap = new Dictionary<int, List<int>>();

            try
            {
                var missionList = Managers.Data.MissionList;
                if (missionList == null) return;

                foreach (var md in missionList)
                {
                    if (md.NextType == 0) continue;
                    if (!_reverseNextMap.TryGetValue(md.NextType, out var list))
                    {
                        list = new List<int>();
                        _reverseNextMap[md.NextType] = list;
                    }
                    list.Add(md.Type);
                }
            }
            catch
            {
                _reverseNextMap = null;
            }
        }

        /// <summary>检查 ProgressMissionList 中是否已包含某 Type 的任务。</summary>
        private static bool ContainsMissionType(IList progressList, int type)
        {
            foreach (var item in progressList)
            {
                if (item is MissionData md && md.Type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// 反射缓存 MissionManager 的 Type 与方法。MissionManager 是 internal 类，
        /// 必须用 AccessTools.TypeByName 按全名查找。首次调用时查找，失败时输出诊断并标记。
        /// </summary>
        private static bool EnsureMethodsCached(WebConsole console)
        {
            if (_methodLookupFailed) return false;
            if (_mmType != null && _getInstanceMethod != null) return true;

            _mmType = AccessTools.TypeByName(MissionManagerTypeName);
            if (_mmType == null)
            {
                console.Log($"反射失败：找不到类型 {MissionManagerTypeName}，游戏版本可能已更新。", LogLevel.Warning);
                _methodLookupFailed = true;
                return false;
            }

            _getInstanceMethod = AccessTools.PropertyGetter(_mmType, "Instance");
            _startMissionMethod = AccessTools.Method(_mmType, "StartMission", new[] { typeof(MissionData) });
            _removeFromWaitQueueMethod = AccessTools.Method(_mmType, "RemoveFromWaitQueue", new[] { typeof(int) });
            _broadcastMissionStateMethod = AccessTools.Method(_mmType, "BroadcastMissionState");
            _progressMissionListProperty = AccessTools.Property(_mmType, "ProgressMissionList");

            if (_getInstanceMethod == null
                || _startMissionMethod == null
                || _removeFromWaitQueueMethod == null
                || _broadcastMissionStateMethod == null
                || _progressMissionListProperty == null)
            {
                console.Log(
                    "反射失败：无法定位 MissionManager.Instance / StartMission / " +
                    "RemoveFromWaitQueue / BroadcastMissionState / ProgressMissionList 之一，" +
                    "游戏版本可能与补丁不匹配。", LogLevel.Warning);
                _methodLookupFailed = true;
                return false;
            }

            return true;
        }

        private static bool TryParseMission(string s, out ESchoolMission missionType)
        {
            if (MissionAliases.TryGetValue(s, out missionType)) return true;
            return Enum.TryParse(s, ignoreCase: true, out missionType)
                && Enum.IsDefined(typeof(ESchoolMission), missionType);
        }

        private static string BuildMissionList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 可派发任务（对照 Protocol.ESchoolMission 全量收录） ━━━");
            sb.AppendLine(" 说明: 派发链尾任务会自动联动初始化前置设备(不计前置分)，");
            sb.AppendLine("       只对用户指定任务计分。建议直接派发链尾任务。");
            sb.AppendLine(" 【手术/医疗线】");
            sb.AppendLine("  ScManikinStart(18)     假人启动（链头，ShouldEnqueue）");
            sb.AppendLine("  ScSurgery(1)          手术（←联动 18）");
            sb.AppendLine("  ScMakeSpray(2)         制喷雾（链头，ShouldEnqueue）");
            sb.AppendLine("  ScSprayCancer(20)     喷癌症（←联动 2）");
            sb.AppendLine("  ScMakeMushroom(28)    制蘑菇（链头，ShouldEnqueue）");
            sb.AppendLine("  ScMushroomAlchemist(30) 蘑菇炼金（←联动 28）");
            sb.AppendLine("  ScPotionAlchemist(21) 药剂炼金（←联动 30→28）");
            sb.AppendLine("  ScPotion(35)           药剂（PotionScanner 启动）");
            sb.AppendLine(" 【电气/电池链】");
            sb.AppendLine("  ScChargeBattery(23)   充电（链头，扣分任务 Point=-3）");
            sb.AppendLine("  ScBatteryMiner(25)    电池矿工（硬前置 23，NextType=3）");
            sb.AppendLine("  ScMiner(3)             矿工（←联动 25→23）");
            sb.AppendLine("  ScBatteryWarp(26)     电池传送（硬前置 23，NextType=13）");
            sb.AppendLine("  ScWarp(13)             传送（←联动 26→23）");
            sb.AppendLine("  ScBatteryBio(27)      电池生物（硬前置 23）");
            sb.AppendLine("  ScBoiler(14)          锅炉（全部 Boiler.StartMission）");
            sb.AppendLine(" 【矿物/合成线】");
            sb.AppendLine("  ScMineralCraft(7)     矿物合成（链头，ShouldEnqueue）");
            sb.AppendLine("  ScCraft(8)            合成（←联动 7）");
            sb.AppendLine("  ScCollector(6)        收集器（Collector.StartMission）");
            sb.AppendLine("  ScEssence(9)          精华（SubType==0 的 Sample 启动）");
            sb.AppendLine("  ScMicroscope(10)     显微镜（SubType==1 的 Sample 启动）");
            sb.AppendLine(" 【神秘学线】");
            sb.AppendLine("  ScBookRune(11)        符文之书（链头，ShouldEnqueue，生成符文书）");
            sb.AppendLine("  ScRune(12)            符文（←联动 11，自动生成符文书）");
            sb.AppendLine("  ScCandle(4)           蜡烛（启动 OccultList + 设置书本提示）");
            sb.AppendLine("  ScFire(36)            火（OccultFire.StartFireMission）");
            sb.AppendLine(" 【饮品类】");
            sb.AppendLine("  ScDrink(15)           饮品（链头，ShouldEnqueue）");
            sb.AppendLine("  ScShakeShaker(16)    摇摇杯（←联动 15）");
            sb.AppendLine("  ScShakerDrink(22)    摇杯饮品（←联动 16→15）");
            sb.AppendLine(" 【其他】");
            sb.AppendLine("  ScFixPc(17)           修电脑（插入 1008 物品 + Computer 状态变更）");
            sb.AppendLine("  ScNintendo(34)       任天堂（任天堂游戏机 + 洗牌 CoinOrder）");
            sb.AppendLine("  ScMorseCode(37)      摩斯密码（随机 Morse 设备启动）");
            sb.AppendLine("  ScAquaticCapture(40) 钓鱼（随机 Fishing 设备启动）");
            sb.AppendLine(" 【占位任务（switch 无分支，派发后不激活设备）】");
            sb.AppendLine("  ScBattery(24) / ScMushroom(29) / ScMushroomBio(31) / ScBatteryMiner2(32)");
            sb.AppendLine("  ScMiner2(33) / ScFusebox(38) / ScWeapon(39)");
            sb.AppendLine("【派发示例】");
            sb.AppendLine("  /givemission ScRune        ← 自动联动 ScBookRune 生成符文书");
            sb.AppendLine("  /givemission 12             ← 数字");
            sb.AppendLine("  /givemission 符文            ← 中文别名");
            sb.AppendLine("  /givemission ScShakerDrink  ← 联动 15→16 初始化整条饮品线");
            return sb.ToString();
        }
    }
}
