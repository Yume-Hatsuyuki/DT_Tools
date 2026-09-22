using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// 任务激活状态的权威数据源。
    ///
    /// 背景：这个游戏采用"某玩家客户端兼任服务器逻辑"的架构（P2P Host 模式）。
    /// 服务端 Server.Game.MissionManager 维护着完整、权威的任务进度：
    ///   - ProgressMissionList：当前正在进行的任务（含 Type/PrevType/NextType/Point）
    ///   - WaitMissionQueue：排队等待、尚未真正开始的任务
    /// 这两份数据由 MissionManager.ClearMission/StartMission 等方法维护，每次任务
    /// 状态变化都会调用 BroadcastMissionState() 重新广播为 S_MISSION_STATE 包，
    /// 因此是实时、无过期风险的权威数据。
    ///
    /// 可访问性说明（重要，此前版本因此编译失败 CS0122）：
    ///   Server.Game.MissionManager 类本身是 `internal class`（成员如 Instance/
    ///   ProgressMissionList 虽标了 public，但因为所在类是 internal，外部程序集
    ///   仍然完全看不到这个类型）。DT_Tools 是独立编译、引用 Assembly-CSharp.dll
    ///   的外部程序集，无法在编译期直接写 `Server.Game.MissionManager.Instance`。
    ///   Server.Game.MissionMirror 则是 `public static class`，可以直接编译期引用，
    ///   但它只在"当前客户端不是 Host"时才会被 PacketHandler 写入（Host 自己不需要
    ///   镜像，见 PacketHandler.Handle_S_MISSION_STATE 的判断），Host 模式下永远为空。
    ///
    /// 因此：非 Host 路径直接编译期引用 MissionMirror；Host 路径必须通过反射访问
    /// internal 的 MissionManager——反射能访问到它的 public 成员（Instance/
    /// ProgressMissionList/WaitMissionQueue 本身都是 public，只是外层类不可见，
    /// 反射拿到 Type 对象后这些 public 成员是可以正常调用的）。
    /// 用 typeof(Managers).Assembly 而非硬编码程序集全名字符串来定位类型，
    /// 因为 Managers 是已知会随游戏一起编译进同一程序集的公开类型，更稳定可靠。
    /// </summary>
    internal static class AgentMissionState
    {
        private static bool IsHost => Managers.Host != null && Managers.Host.IsHost;

        // ── 反射缓存 ─────────────────────────────────────────────────────
        // 反射查找有实际开销，且 Type/PropertyInfo 在同一个游戏进程生命周期内不会变化，
        // 缓存后每次只需要走 GetValue，避免每次读取任务状态都重新做类型查找。

        private static bool s_reflectionInitialized;
        private static bool s_reflectionAvailable;
        private static PropertyInfo s_instanceProp;
        private static PropertyInfo s_progressListProp;
        private static PropertyInfo s_waitQueueProp;
        private static FieldInfo s_typeField;

        /// <summary>
        /// 反射初始化只做一次；失败时记录状态，后续直接短路返回，不重复尝试
        /// （避免游戏更新导致字段/类名变化时，每次调用都产生大量失败的反射尝试）。
        /// </summary>
        private static void EnsureReflectionInit()
        {
            if (s_reflectionInitialized) return;
            s_reflectionInitialized = true;
            try
            {
                var asm = typeof(Managers).Assembly;
                var missionManagerType = asm.GetType("Server.Game.MissionManager");
                var missionDataType = asm.GetType("Data.MissionData");
                if (missionManagerType == null || missionDataType == null) return;

                s_instanceProp = missionManagerType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                s_progressListProp = missionManagerType.GetProperty("ProgressMissionList",
                    BindingFlags.Public | BindingFlags.Instance);
                s_waitQueueProp = missionManagerType.GetProperty("WaitMissionQueue",
                    BindingFlags.Public | BindingFlags.Instance);
                s_typeField = missionDataType.GetField("Type", BindingFlags.Public | BindingFlags.Instance);

                s_reflectionAvailable = s_instanceProp != null && s_progressListProp != null
                    && s_waitQueueProp != null && s_typeField != null;
            }
            catch
            {
                // 反射初始化的任何异常都视为"不可用"，交给 MissionMirror 或保守默认值兜底，
                // 不能让类型查找失败拖垮整个 Agent 功能。
                s_reflectionAvailable = false;
            }
        }

        /// <summary>
        /// 通过反射读取 Host 本地 MissionManager.Instance 的某个 List/Queue 属性，
        /// 提取每个元素的 Type 字段，返回 Type 集合。
        /// 属性类型是 List<MissionData> 或 Queue<MissionData>，两者都实现
        /// IEnumerable，用非泛型 IEnumerable 遍历即可，不需要关心具体是哪一种容器。
        /// </summary>
        private static HashSet<int> ReadTypesViaReflection(PropertyInfo listProp)
        {
            var result = new HashSet<int>();
            try
            {
                var mgrInstance = s_instanceProp.GetValue(null);
                if (mgrInstance == null) return result;
                var list = listProp.GetValue(mgrInstance) as IEnumerable;
                if (list == null) return result;
                foreach (var item in list)
                {
                    if (item == null) continue;
                    var typeValue = s_typeField.GetValue(item);
                    if (typeValue is int i) result.Add(i);
                }
            }
            catch
            {
                // 反射调用期间的任何异常（字段布局变化、空引用等）都不应该向上抛出，
                // 静默返回已收集到的部分结果或空集合，由 IsReady 的判断决定是否可信。
            }
            return result;
        }

        /// <summary>
        /// 当前是否能读到可靠的任务状态。
        ///   Host 模式：反射链路完整可用，且能成功拿到 MissionManager.Instance 实例。
        ///   非 Host 模式：MissionMirror 已经收到过至少一次 S_MISSION_STATE 广播。
        /// </summary>
        public static bool IsReady
        {
            get
            {
                if (IsHost)
                {
                    EnsureReflectionInit();
                    if (!s_reflectionAvailable) return false;
                    try
                    {
                        return s_instanceProp.GetValue(null) != null;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return Server.Game.MissionMirror.HasState;
            }
        }

        /// <summary>当前正在进行的任务类型集合（ESchoolMission 底层值）。</summary>
        public static HashSet<int> ActiveMissionTypes
        {
            get
            {
                if (IsHost)
                {
                    EnsureReflectionInit();
                    if (!s_reflectionAvailable) return new HashSet<int>();
                    return ReadTypesViaReflection(s_progressListProp);
                }
                return new HashSet<int>(Server.Game.MissionMirror.ProgressTypes);
            }
        }

        /// <summary>排队等待、尚未真正开始的任务类型集合。</summary>
        public static HashSet<int> WaitingMissionTypes
        {
            get
            {
                if (IsHost)
                {
                    EnsureReflectionInit();
                    if (!s_reflectionAvailable) return new HashSet<int>();
                    return ReadTypesViaReflection(s_waitQueueProp);
                }
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
