using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoPickCharacter
{
    /// <summary>跟踪状态：当前阶段、选角发包尝试计数与日志去重、大厅换角尝试计数/上限与节流。</summary>
    internal static class AutoPickCharacterState
    {
        public static EGameState LastState = EGameState.NoneState;

        // 选角阶段跟踪
        public static float PhaseEnterRealtime = -1f;
        public static float NextTryRealtime;
        public static int Attempts;
        public static bool WaitingLogged;
        public static bool GaveUpLogged;

        // 大厅换角跟踪（LobbyAttempts 对齐选角 MaxAttempts 上限，防止失败时无限刷包）
        public static int LastLobbySyncedId = int.MinValue;
        public static float NextLobbyTryRealtime;
        public static int LobbyAttempts;
        public static bool LobbyGaveUpLogged;

        /// <summary>新进入选角阶段：开启一轮发包跟踪。</summary>
        public static void BeginPickPhase()
        {
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            NextTryRealtime = 0f;
            Attempts = 0;
            WaitingLogged = false;
            GaveUpLogged = false;
        }

        /// <summary>离开选角阶段：清阶段内跟踪（LastState 由 Trigger 统一回写）。</summary>
        public static void ResetPhase()
        {
            PhaseEnterRealtime = -1f;
            NextTryRealtime = 0f;
            Attempts = 0;
            WaitingLogged = false;
            GaveUpLogged = false;
        }

        /// <summary>新进入大厅：重新跟踪换角同步。</summary>
        public static void EnterLobby()
        {
            LastLobbySyncedId = int.MinValue;
            NextLobbyTryRealtime = 0f;
            LobbyAttempts = 0;
            LobbyGaveUpLogged = false;
        }

        /// <summary>模块禁用 / 总开关关闭时清空全部跟踪。</summary>
        public static void Reset()
        {
            LastState = EGameState.NoneState;
            ResetPhase();
            EnterLobby();
        }
    }
}
