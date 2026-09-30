using System.Collections.Generic;
using DT_Tools.Game;
using Data;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GiveMission
{
    /// <summary>
    /// /givemission 业务：MissionManager 访问守卫、软前置联动初始化、任务派发。
    ///
    /// MissionManager 在发行程序集中是 internal 类（0.1.15b Server.Game/MissionManager.cs:10），
    /// 所有访问统一走 Game.MissionAccess 反射访问器（反射目标行号与缓存策略见
    /// MissionAccess.cs 头注释，本文件不再持有任何反射代码）。
    /// StartMission 会把任务加入 ProgressMissionList、递归启动硬前置（PrevType）并按
    /// ESchoolMission switch 激活设备，但不广播 S_MISSION_STATE，所以派发后额外调用
    /// BroadcastMissionState() 让客户端立即刷新任务面板。
    ///
    /// 软前置联动初始化：MissionData.PrevType 只记录硬前置，但很多任务的设备初始化依赖
    /// 另一条已完成的任务链（如 ScRune(12)←ScBookRune(11)、ScSurgery(1)←ScManikinStart(18)、
    /// ScSprayCancer(20)←ScMakeSpray(2)、ScShakerDrink(22)←ScShakeShaker(16)←ScDrink(15)）。
    /// 这些关系通过 NextType 表达。本命令构建反向 NextType 表，派发 T 时沿链收集软前置 P，
    /// 逐个 StartMission(P) 跑设备初始化后立即从 ProgressMissionList 移除 P：
    /// P 的设备状态被正确初始化，但玩家完成 P 时 ClearMission(P) 直接 return（不计分、
    /// 不触发 NextType 链），只有 T 计分。派发前先 RemoveFromWaitQueue 清理队列残留，
    /// 避免下次 StartNextFromQueue 把任务再次启动。
    /// </summary>
    internal static class GiveMissionLogic
    {
        /// <summary>switch 中无对应 Start_ScXXX 分支的占位任务类型
        /// （0.1.15b MissionManager.cs:807-814 空分组，另含枚举外的裸值 5/19）。</summary>
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

        // 反向 NextType 表（NextType → [Type]），首次派发时构建
        private static Dictionary<int, List<int>> _reverseNextMap;
        private static bool _reverseMapBuilt;

        /// <summary>派发结果（供 Format 输出）。</summary>
        public sealed class Outcome
        {
            public ESchoolMission Mission;
            public List<int> InitializedPrereqs = new List<int>();
            public int PrevType;
            public bool IsEmptyCase;
        }

        /// <summary>阶段守卫：切换中 / 迁移中拒绝派发（原 PhaseJumpHelper.EnsureNotBusy 文案）。</summary>
        public static bool TryGuardBusy(GameRoom room, out string code, out string text)
        {
            if (room.IsTransitioning)
            {
                code = "transitioning";
                text = "阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。";
                return false;
            }
            if (room.IsMigrating)
            {
                code = "migrating";
                text = "正在进行主机迁移，无法切换阶段。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }

        /// <summary>
        /// 派发前置守卫：MissionManager 反射访问器是否可用。
        /// 反射链路一旦初始化失败会永久短路（见 MissionAccess.EnsureInit），此时提示
        /// 与旧"成员定位失败"文案等价的诊断。
        /// </summary>
        public static bool EnsureMissionAccess(out string text)
        {
            if (MissionAccess.Available)
            {
                text = null;
                return true;
            }
            text = "反射失败：MissionManager 成员定位失败，游戏版本可能与插件不匹配（重启游戏可重试）。";
            return false;
        }

        /// <summary>
        /// 派发任务：软前置联动初始化 → 清队列 → StartMission(T) → BroadcastMissionState。
        /// 失败返回 false（错误码与提示已给出）。
        /// </summary>
        public static bool TryDispatch(ESchoolMission missionType, out Outcome outcome, out string code, out string text)
        {
            outcome = null;

            object mmInstance = MissionAccess.Instance;
            if (mmInstance == null)
            {
                code = "mission manager null";
                text = "MissionManager.Instance 为 null，任务系统未初始化。";
                return false;
            }

            MissionData data = Managers.Data.GetMissionData((int)missionType);   // 0.1.15b DataManager.cs:113
            if (data == null)
            {
                code = "no mission data";
                text = $"任务类型 {missionType}({(int)missionType}) 没有对应 MissionData。";
                return false;
            }

            var progressList = MissionAccess.ProgressList;
            if (progressList == null)
            {
                code = "no progress list";
                text = "无法读取 MissionManager.ProgressMissionList。";
                return false;
            }

            // 防重：ProgressMissionList 中已有该类型则拒绝
            if (ContainsMissionType(progressList, (int)missionType))
            {
                code = "mission in progress";
                text = $"任务 {missionType}({(int)missionType}) 已在进行中，不能重复派发。";
                return false;
            }

            // ── 软前置联动初始化：沿反向 NextType 链收集（链头→链尾顺序）──
            var softPrereqs = CollectSoftPrerequisites((int)missionType);
            outcome = new Outcome
            {
                Mission = missionType,
                PrevType = data.PrevType,
                IsEmptyCase = EmptyCaseMissions.Contains(missionType),
            };

            foreach (int pType in softPrereqs)
            {
                // 前置已在 ProgressMissionList → 设备已初始化，跳过
                if (ContainsMissionType(progressList, pType)) continue;

                MissionData pData = Managers.Data.GetMissionData(pType);
                if (pData == null) continue;

                // 从队列清理残留（尽力而为，队列本就无该任务时返回 false 不影响主流程）
                MissionAccess.RemoveFromWaitQueue(pType);

                // 调用 StartMission(P) 跑设备初始化（会 Add 到 ProgressMissionList）。
                // MissionAccess 吞掉反射异常改返 false——设备初始化不完整时不能再继续
                // 派发主任务（会得到半初始化的链），直接中止并给出诊断。
                if (!MissionAccess.StartMission(pData))
                {
                    code = "prerequisite failed";
                    text = $"软前置任务 {(ESchoolMission)pType}({pType}) 的 StartMission 调用失败，已中止派发。";
                    return false;
                }

                // 立即从 ProgressMissionList 移除 P：仅做设备初始化，不计分。
                // 玩家完成 P 的设备交互时 ClearMission(P) 找不到任务直接 return，
                // 不会触发 NextType 链重复启动 T。
                // 注意：若未来游戏 P 链（StartMission(MissionData) 递归前置链）引入异步初始化，
                // 此同步移除需复核。
                progressList.Remove(pData);
                outcome.InitializedPrereqs.Add(pType);
            }

            // ── 派发主任务 ──
            MissionAccess.RemoveFromWaitQueue((int)missionType);
            if (!MissionAccess.StartMission(data))
            {
                code = "start failed";
                text = "MissionManager.StartMission 调用失败（实例丢失或游戏版本变更），任务未派发。";
                return false;
            }
            // 尽力广播；失败只影响客户端面板即时刷新，不影响派发结果（与旧版不检查一致）
            MissionAccess.BroadcastMissionState();

            code = null;
            text = null;
            return true;
        }

        /// <summary>
        /// 沿反向 NextType 链递归收集所有软前置，返回链头→链尾顺序的列表。
        /// 例如派发 ScShakerDrink(22): 22←16←15，返回 [15, 16]。
        /// </summary>
        private static List<int> CollectSoftPrerequisites(int type)
        {
            EnsureReverseNextMapBuilt();
            var result = new List<int>();
            if (_reverseNextMap == null) return result;

            var visited = new HashSet<int>();
            CollectSoftPrerequisitesCore(type, visited, result);
            return result;
        }

        private static void CollectSoftPrerequisitesCore(int type, HashSet<int> visited, List<int> result)
        {
            if (!visited.Add(type)) return;   // 防环
            if (_reverseNextMap == null) return;
            if (!_reverseNextMap.TryGetValue(type, out var predecessors)) return;

            foreach (int p in predecessors)
            {
                CollectSoftPrerequisitesCore(p, visited, result);   // 先递归上游（链头优先）
                result.Add(p);
            }
        }

        /// <summary>
        /// 构建反向 NextType 表: NextType → [Type]。
        /// 例: MissionData{Type=11, NextType=12} → map[12] = [11]。
        /// 数据源 Managers.Data.MissionList（0.1.15b DataManager.cs:26）。
        /// </summary>
        private static void EnsureReverseNextMapBuilt()
        {
            if (_reverseMapBuilt) return;
            _reverseMapBuilt = true;

            try
            {
                var missionList = Managers.Data.MissionList;
                if (missionList == null) return;

                var map = new Dictionary<int, List<int>>();
                foreach (MissionData md in missionList)
                {
                    if (md.NextType == 0) continue;
                    if (!map.TryGetValue(md.NextType, out var list))
                    {
                        list = new List<int>();
                        map[md.NextType] = list;
                    }
                    list.Add(md.Type);
                }
                _reverseNextMap = map;
            }
            catch
            {
                _reverseNextMap = null;
            }
        }

        /// <summary>检查 ProgressMissionList 中是否已包含某 Type 的任务。</summary>
        private static bool ContainsMissionType(System.Collections.IList progressList, int type)
        {
            foreach (var item in progressList)
            {
                if (item is MissionData md && md.Type == type) return true;
            }
            return false;
        }
    }
}
