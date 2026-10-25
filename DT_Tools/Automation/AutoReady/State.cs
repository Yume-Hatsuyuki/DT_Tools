using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoReady
{
    internal static class AutoReadyState
    {
        public static EGameState LastState = EGameState.NoneState;
        public static bool Done;
        public static float PhaseEnterRealtime = -1f;
        public static float NextTryRealtime;
        public static int Attempts;

        public static void BeginLobby()
        {
            Done = false;
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            NextTryRealtime = 0f;
            Attempts = 0;
        }

        public static void MarkLeft() => Done = true;

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
