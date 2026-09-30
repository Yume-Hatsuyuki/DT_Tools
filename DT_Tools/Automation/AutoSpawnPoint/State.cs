using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    /// <summary>跟踪状态：当前阶段、本局是否已传送、进入 Survive 的时刻、日志去重。</summary>
    internal static class AutoSpawnPointState
    {
        public static EGameState LastState = EGameState.NoneState;
        public static bool DoneThisRound;
        public static float PhaseEnterRealtime = -1f;
        public static bool WaitingLogged;

        /// <summary>新进入 Survive：开启新一轮跟踪。</summary>
        public static void BeginRound()
        {
            DoneThisRound = false;
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            WaitingLogged = false;
        }

        /// <summary>离开 Survive 或动作已完成：本局不再触发。</summary>
        public static void MarkLeft() => DoneThisRound = true;

        /// <summary>模块禁用 / 总开关关闭时清空跟踪。</summary>
        public static void Reset()
        {
            LastState = EGameState.NoneState;
            DoneThisRound = false;
            PhaseEnterRealtime = -1f;
            WaitingLogged = false;
        }
    }
}
