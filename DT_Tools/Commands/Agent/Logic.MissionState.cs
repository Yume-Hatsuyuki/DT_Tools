using System.Collections.Generic;
using DT_Tools.Game;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// 任务激活状态的权威数据源（对应旧 AgentMissionState.cs）。
    ///
    /// 背景：这个游戏采用"某玩家客户端兼任服务器逻辑"的架构（P2P Host 模式）。
    /// 服务端 Server.Game.MissionManager 维护着完整、权威的任务进度：
    ///   - ProgressMissionList：当前正在进行的任务（含 Type/PrevType/NextType/Point）
    ///   - WaitMissionQueue：排队等待、尚未真正开始的任务
    /// 这两份数据由 MissionManager.ClearMission/StartMission 等方法维护，每次任务
    /// 状态变化都会调用 BroadcastMissionState() 重新广播为 S_MISSION_STATE 包，
    /// 因此是实时、无过期风险的权威数据。
    ///
    /// 可访问性说明（重要，直接引用会编译失败 CS0122）：
    ///   Server.Game.MissionManager 类本身是 `internal class`（0.1.15b MissionManager.cs:10），
    ///   DT_Tools 是独立编译、引用 Assembly-CSharp.dll 的外部程序集，无法在编译期直接写
    ///   `Server.Game.MissionManager.Instance`。Server.Game.MissionMirror 则是
    ///   `public static class`（0.1.15b MissionMirror.cs:4，HasState:10/ProgressTypes:6/WaitTypes:8），
    ///   可以直接编译期引用，但它只在"当前客户端不是 Host"时才会被 PacketHandler 写入
    ///   （Host 自己不需要镜像，见 PacketHandler.Handle_S_MISSION_STATE 的判断），
    ///   Host 模式下永远为空。
    ///
    /// 因此：非 Host 路径直接编译期引用 MissionMirror；Host 路径经
    /// <see cref="MissionAccess"/> 反射访问 internal 的 MissionManager——
    /// 反射目标定位与行号注释统一收口在 Game/MissionAccess.cs 头注释，
    /// 本文件不再持有任何反射代码。
    /// </summary>
    internal static class AgentMissionState
    {
        private static bool IsHost => HostGuard.IsHost;

        /// <summary>
        /// 当前是否能读到可靠的任务状态。
        ///   Host 模式：反射链路完整可用（MissionAccess.Available），且能成功拿到
        ///   MissionManager.Instance 实例。
        ///   非 Host 模式：MissionMirror 已经收到过至少一次 S_MISSION_STATE 广播。
        /// </summary>
        public static bool IsReady
        {
            get
            {
                if (IsHost)
                    return MissionAccess.Available && MissionAccess.Instance != null;
                return Server.Game.MissionMirror.HasState;   // 0.1.15b MissionMirror.cs:10
            }
        }

        /// <summary>当前正在进行的任务类型集合（ESchoolMission 底层值）。</summary>
        public static HashSet<int> ActiveMissionTypes
        {
            get
            {
                if (IsHost)
                    return MissionAccess.CollectTypes(MissionAccess.ProgressList);
                return new HashSet<int>(Server.Game.MissionMirror.ProgressTypes);
            }
        }

        /// <summary>排队等待、尚未真正开始的任务类型集合。</summary>
        public static HashSet<int> WaitingMissionTypes
        {
            get
            {
                if (IsHost)
                    return MissionAccess.CollectTypes(MissionAccess.WaitQueue);
                return new HashSet<int>(Server.Game.MissionMirror.WaitTypes);
            }
        }

        /// <summary>
        /// 某个任务类型当前是否处于"进行中"。
        /// 未就绪（IsReady==false）时保守返回 true——避免游戏刚加载、状态还没同步到，
        /// 或反射链路因未来版本更新而失效时，把所有道具误判为"任务不存在"而抢先丢弃。
        /// </summary>
        public static bool IsActive(int missionType)
        {
            if (!IsReady) return true;
            return ActiveMissionTypes.Contains(missionType);
        }

        /// <summary>
        /// 某个任务类型当前是否"进行中或排队中"（用于不想漏掉即将开始的任务的场景）。
        /// </summary>
        public static bool IsActiveOrWaiting(int missionType)
        {
            if (!IsReady) return true;
            return ActiveMissionTypes.Contains(missionType) || WaitingMissionTypes.Contains(missionType);
        }
    }
}
