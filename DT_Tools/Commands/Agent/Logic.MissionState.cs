using System.Collections.Generic;
using DT_Tools.Game;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// 任务激活状态的权威数据源（对应旧 AgentMissionState.cs）。
    /// </summary>
    /// <remarks>
    /// 服务端 Server.Game.MissionManager 维护 ProgressMissionList（进行中）与 WaitMissionQueue（排队），每次变化经 BroadcastMissionState 广播为 S_MISSION_STATE，数据实时、无过期风险。
    /// 可访问性（直接引用会编译失败 CS0122）：MissionManager 是 internal class（0.1.17a MissionManager.cs:10），DT_Tools 为外部编译。
    /// Server.Game.MissionMirror 是 public static class（0.1.17a Server.Game/MissionMirror.cs:5），可编译期引用，但只在非 Host 客户端由 PacketHandler 写入（见 Handle_S_MISSION_STATE），Host 模式下为空。
    /// 因此：非 Host 直接引用 MissionMirror；Host 经 <see cref="MissionAccess"/> 反射访问 MissionManager（反射目标与行号收口在 Game/MissionAccess.cs）。
    /// </remarks>
    internal static class AgentMissionState
    {
        private static bool IsHost => HostGuard.IsHost;

        /// <summary>
        /// 当前是否能读到可靠的任务状态。
        /// </summary>
        /// <remarks>
        /// Host 模式：反射链路完整可用（MissionAccess.Available），且能成功拿到
        /// MissionManager.Instance 实例。
        /// 非 Host 模式：MissionMirror 已经收到过至少一次 S_MISSION_STATE 广播。
        /// </remarks>
        public static bool IsReady
        {
            get
            {
                if (IsHost)
                    return MissionAccess.Available && MissionAccess.Instance != null;
                return Server.Game.MissionMirror.HasState;   // 0.1.17a Server.Game/MissionMirror.cs:11
            }
        }

        /// <summary>
        /// 当前正在进行的任务类型集合（ESchoolMission 底层值）。
        /// </summary>
        public static HashSet<int> ActiveMissionTypes
        {
            get
            {
                if (IsHost)
                    return MissionAccess.CollectTypes(MissionAccess.ProgressList);
                return new HashSet<int>(Server.Game.MissionMirror.ProgressTypes);
            }
        }

        /// <summary>
        /// 某个任务类型当前是否处于"进行中"。
        /// </summary>
        /// <remarks>
        /// 未就绪（IsReady==false）时保守返回 true——避免游戏刚加载、状态还没同步到，
        /// 或反射链路因未来版本更新而失效时，把所有道具误判为"任务不存在"而抢先丢弃。
        /// </remarks>
        public static bool IsActive(int missionType)
        {
            if (!IsReady) return true;
            return ActiveMissionTypes.Contains(missionType);
        }
    }
}
