using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoReady
{
    /// 跟踪状态：当前阶段、本次进入大厅是否完成、进入时刻、下次尝试时刻与尝试计数。
    internal static class AutoReadyState
    {
        public static EGameState LastState = EGameState.NoneState;
        public static bool Done;
        public static float PhaseEnterRealtime = -1f;
        public static float NextTryRealtime;
        public static int Attempts;

        /// 新进入大厅：开启一轮跟踪。
        public static void BeginLobby()
        {
            Done = false;
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            NextTryRealtime = 0f;
            Attempts = 0;
        }

        /// 离开大厅 / 已就绪 / 房主 / 次数耗尽：不再触发。
        public static void MarkLeft() => Done = true;

        /// 模块禁用 / 总开关关闭时清空跟踪（再启用时按新进入大厅处理）。
        public static void Reset()
        {
            LastState = EGameState.NoneState;
            Done = false;
            PhaseEnterRealtime = -1f;
            NextTryRealtime = 0f;
            Attempts = 0;
        }
    }
}
