using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoAcquireWeapon
{
    /// <summary>跟踪状态：当前阶段、本局是否完成、进入 Survive 的时刻、下次尝试时刻与尝试计数。</summary>
    internal static class AutoAcquireWeaponState
    {
        public static EGameState LastState = EGameState.NoneState;
        public static bool DoneThisRound;
        public static float PhaseEnterRealtime = -1f;
        public static float NextTryRealtime;
        public static int Attempts;

        /// <summary>新进入 Survive：开启新一轮跟踪。</summary>
        public static void BeginRound()
        {
            DoneThisRound = false;
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            NextTryRealtime = 0f;
            Attempts = 0;
        }

        /// <summary>离开 Survive 或本局动作已完成：不再触发。</summary>
        public static void MarkLeft() => DoneThisRound = true;

        /// <summary>模块禁用 / 总开关关闭时清空跟踪。</summary>
        public static void Reset()
        {
            LastState = EGameState.NoneState;
            DoneThisRound = false;
            PhaseEnterRealtime = -1f;
            NextTryRealtime = 0f;
            Attempts = 0;
        }
    }
}
