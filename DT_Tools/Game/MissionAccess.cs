using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Data;
using Protocol;

namespace DT_Tools.Game
{
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

        public static bool Available
        {
            get { EnsureInit(); return _ok; }
        }

        public static object Instance
        {
            get { EnsureInit(); return _ok ? _instance.GetValue(null) : null; }
        }

        public static IList ProgressList
        {
            get
            {
                object inst = Instance;
                return inst == null ? null : _progressList.GetValue(inst) as IList;
            }
        }

        public static IEnumerable WaitQueue
        {
            get
            {
                object inst = Instance;
                return inst == null ? null : _waitQueue.GetValue(inst) as IEnumerable;
            }
        }

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
