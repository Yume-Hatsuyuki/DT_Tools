using System.Collections.Generic;
using DT_Tools.Game;
using Data;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GiveMission
{
    internal static class GiveMissionLogic
    {
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

        private static Dictionary<int, List<int>> _reverseNextMap;
        private static bool _reverseMapBuilt;

        public sealed class Outcome
        {
            public ESchoolMission Mission;
            public List<int> InitializedPrereqs = new List<int>();
            public int PrevType;
            public bool IsEmptyCase;
        }

        public static bool TryGuardBusy(GameRoom room, out string code, out string text)
            => RoomFlow.TryGuardBusy(room, "切换阶段", out code, out text);

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

            MissionData data = Managers.Data.GetMissionData((int)missionType);
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

            if (ContainsMissionType(progressList, (int)missionType))
            {
                code = "mission in progress";
                text = $"任务 {missionType}({(int)missionType}) 已在进行中，不能重复派发。";
                return false;
            }

            var softPrereqs = CollectSoftPrerequisites((int)missionType);
            outcome = new Outcome
            {
                Mission = missionType,
                PrevType = data.PrevType,
                IsEmptyCase = EmptyCaseMissions.Contains(missionType),
            };

            foreach (int pType in softPrereqs)
            {
                if (ContainsMissionType(progressList, pType)) continue;

                MissionData pData = Managers.Data.GetMissionData(pType);
                if (pData == null) continue;

                MissionAccess.RemoveFromWaitQueue(pType);

                if (!MissionAccess.StartMission(pData))
                {
                    code = "prerequisite failed";
                    text = $"软前置任务 {(ESchoolMission)pType}({pType}) 的 StartMission 调用失败，已中止派发。";
                    return false;
                }

                progressList.Remove(pData);
                outcome.InitializedPrereqs.Add(pType);
            }

            MissionAccess.RemoveFromWaitQueue((int)missionType);
            if (!MissionAccess.StartMission(data))
            {
                code = "start failed";
                text = "MissionManager.StartMission 调用失败（实例丢失或游戏版本变更），任务未派发。";
                return false;
            }
            MissionAccess.BroadcastMissionState();

            code = null;
            text = null;
            return true;
        }

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
            if (!visited.Add(type)) return;
            if (_reverseNextMap == null) return;
            if (!_reverseNextMap.TryGetValue(type, out var predecessors)) return;

            foreach (int p in predecessors)
            {
                CollectSoftPrerequisitesCore(p, visited, result);
                result.Add(p);
            }
        }

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
