namespace DT_Tools.Patches.Fun.AutoRejoin
{
    /// <summary>重连状态：是否被踢/被移除（此时绝不自动重连）+ 待重连房间码 + 协程运行标记。</summary>
    internal static class AutoRejoinState
    {
        /// <summary>收到房主踢出包（S_KICKED）：本次不自动重连。</summary>
        public static bool SuppressedByKick;

        /// <summary>被房主移出房间（S_LEAVE_GAME 且是自己）：本次不自动重连。</summary>
        public static bool SuppressedByRemoval;

        /// <summary>待重连的房间码（null = 无待重连）。</summary>
        internal static string PendingCode;

        /// <summary>重连协程是否在跑（防重入）。</summary>
        internal static bool RoutineRunning;
    }
}
