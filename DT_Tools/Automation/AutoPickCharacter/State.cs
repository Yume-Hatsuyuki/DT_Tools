using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoPickCharacter
{
    internal static class AutoPickCharacterState
    {
        public static EGameState LastState = EGameState.NoneState;

        public static float PhaseEnterRealtime = -1f;
        public static float NextTryRealtime;
        public static int Attempts;
        public static bool WaitingLogged;
        public static bool GaveUpLogged;

        public static int LastLobbySyncedId = int.MinValue;
        public static float NextLobbyTryRealtime;
        public static int LobbyAttempts;
        public static bool LobbyGaveUpLogged;

        public static void BeginPickPhase()
        {
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            NextTryRealtime = 0f;
            Attempts = 0;
            WaitingLogged = false;
            GaveUpLogged = false;
        }

        public static void ResetPhase()
        {
            PhaseEnterRealtime = -1f;
            NextTryRealtime = 0f;
            Attempts = 0;
            WaitingLogged = false;
            GaveUpLogged = false;
        }

        public static void EnterLobby()
        {
            LastLobbySyncedId = int.MinValue;
            NextLobbyTryRealtime = 0f;
            LobbyAttempts = 0;
            LobbyGaveUpLogged = false;
        }

        public static void Reset()
        {
            LastState = EGameState.NoneState;
            ResetPhase();
            EnterLobby();
        }
    }
}
