using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Data;
using Protocol;

namespace DT_Tools.Game
{
    /// <summary>
    /// Server.Game.MissionManager 反射访问器（全项目唯一，消费方：FishingPond /
    /// LobbyMaxPlayers / GiveMission / Agent）。该类在发行程序集中是 internal
    /// （0.1.15b Server.Game/MissionManager.cs:10），无法编译期引用，但其成员签名固定，
    /// 反射能访问（Instance/ProgressMissionList/WaitMissionQueue/ClearMission/StartMission()
    /// 均为 public，BroadcastMissionState/RemoveFromWaitQueue/StartMission(MissionData) 为
    /// private——外层类不可见不妨碍反射调用）。Data.MissionData 是 public 类型，直接引用。
    ///
    /// 反射目标（0.1.15b，升级时全文搜索核对）：
    ///   Type 全名 "Server.Game.MissionManager"            → MissionManager.cs:10（internal class）
    ///   Instance（static property getter）                → :14
    ///   ProgressMissionList（public List&lt;MissionData&gt;）    → :26
    ///   WaitMissionQueue（public Queue&lt;MissionData&gt;）      → :28
    ///   BroadcastMissionState()（private void）            → :124
    ///   StartMission()（public void 重载）                 → :355
    ///   ClearMission(ESchoolMission, Player, bool)（public）→ :437
    ///   RemoveFromWaitQueue(int)（private bool）           → :592
    ///   StartMission(Data.MissionData)（private void）     → :701
    /// 定位方式：typeof(Managers).Assembly.GetType(全名)，不硬编码程序集名。
    /// Type/PropertyInfo 进程内不变，static readonly 语义缓存一次；失败短路不重试。
    /// 调用失败一律返回 false/null，由调用方按自己的上下文记日志。
    /// </summary>
    public static class MissionAccess
    {
        private static bool _init;
        private static bool _ok;
        private static PropertyInfo _instance;
        private static PropertyInfo _progressList;
        private static PropertyInfo _waitQueue;
        private static MethodInfo _broadcastMissionState;
        private static MethodInfo _startParameterless;
        private static MethodInfo _startWithData;
        private static MethodInfo _clearMission;
        private static MethodInfo _removeFromWaitQueue;

        private static void EnsureInit()
        {
            if (_init) return;
            _init = true;
            try
            {
                var mm = typeof(Managers).Assembly.GetType("Server.Game.MissionManager");
                if (mm == null) return;

                _instance = mm.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _progressList = mm.GetProperty("ProgressMissionList", BindingFlags.Public | BindingFlags.Instance);
                _waitQueue = mm.GetProperty("WaitMissionQueue", BindingFlags.Public | BindingFlags.Instance);
                _broadcastMissionState = mm.GetMethod("BroadcastMissionState",
                    BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _startParameterless = mm.GetMethod("StartMission",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _startWithData = mm.GetMethod("StartMission",
                    BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(MissionData) }, null);
                _clearMission = mm.GetMethod("ClearMission",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(ESchoolMission), typeof(Server.Game.Player), typeof(bool) }, null);
                _removeFromWaitQueue = mm.GetMethod("RemoveFromWaitQueue",
                    BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);

                _ok = _instance != null && _progressList != null && _waitQueue != null
                      && _broadcastMissionState != null && _startParameterless != null
                      && _startWithData != null && _clearMission != null && _removeFromWaitQueue != null;
            }
            catch
            {
                _ok = false;
            }
        }

        /// <summary>反射链路是否完整（类型定位与全部成员绑定成功）。</summary>
        public static bool Available
        {
            get { EnsureInit(); return _ok; }
        }

        /// <summary>Host 本地权威实例；不可用或未在局内时为 null。</summary>
        public static object Instance
        {
            get { EnsureInit(); return _ok ? _instance.GetValue(null) : null; }
        }

        /// <summary>
        /// 进行中任务列表（List&lt;MissionData&gt;，以 IList 暴露供遍历/移除）；
        /// 不可用或无实例 → null。
        /// </summary>
        public static IList ProgressList
        {
            get
            {
                object inst = Instance;
                return inst == null ? null : _progressList.GetValue(inst) as IList;
            }
        }

        /// <summary>排队任务队列（Queue&lt;MissionData&gt;，以 IEnumerable 暴露）；不可用或无实例 → null。</summary>
        public static IEnumerable WaitQueue
        {
            get
            {
                object inst = Instance;
                return inst == null ? null : _waitQueue.GetValue(inst) as IEnumerable;
            }
        }

        /// <summary>StartMission()：开局启动任务分配（0.1.15b MissionManager.cs:355）。</summary>
        public static bool StartParameterless()
        {
            object inst = Instance;
            if (inst == null) return false;
            try
            {
                _startParameterless.Invoke(inst, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>StartMission(MissionData)：加入进行中列表、递归启动硬前置并激活设备（:701）。</summary>
        public static bool StartMission(MissionData data)
        {
            object inst = Instance;
            if (inst == null || data == null) return false;
            try
            {
                _startWithData.Invoke(inst, new object[] { data });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>ClearMission(ESchoolMission, Player, bool)：任务完成收口（:437）。</summary>
        public static bool ClearMission(ESchoolMission mission, Server.Game.Player completer, bool isInfected)
        {
            object inst = Instance;
            if (inst == null) return false;
            try
            {
                _clearMission.Invoke(inst, new object[] { mission, completer, isInfected });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>RemoveFromWaitQueue(int)：清理排队残留（:592）。</summary>
        public static bool RemoveFromWaitQueue(int missionType)
        {
            object inst = Instance;
            if (inst == null) return false;
            try
            {
                return (bool)_removeFromWaitQueue.Invoke(inst, new object[] { missionType });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>BroadcastMissionState()：把任务状态重新广播为 S_MISSION_STATE（:124）。</summary>
        public static bool BroadcastMissionState()
        {
            object inst = Instance;
            if (inst == null) return false;
            try
            {
                _broadcastMissionState.Invoke(inst, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>遍历队列/列表元素并按 MissionData.Type 收集（元素非 MissionData 时跳过）。</summary>
        public static HashSet<int> CollectTypes(IEnumerable items)
        {
            var types = new HashSet<int>();
            if (items == null) return types;
            foreach (object item in items)
            {
                if (item is MissionData md)
                    types.Add(md.Type);
            }
            return types;
        }
    }
}
