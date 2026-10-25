using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    internal static class AutoSpawnPointState
    {
        public static EGameState LastState = EGameState.NoneState;
        public static bool DoneThisRound;
        public static float PhaseEnterRealtime = -1f;
        public static bool WaitingLogged;

        public static void BeginRound()
        {
            DoneThisRound = false;
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            WaitingLogged = false;
        }

        public static void MarkLeft() => DoneThisRound = true;

        public static void Reset()
        {
            LastState = EGameState.NoneState;
            DoneThisRound = false;
            PhaseEnterRealtime = -1f;
            WaitingLogged = false;
        }
    }
}
