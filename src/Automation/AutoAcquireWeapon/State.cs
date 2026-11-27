// 作用：维护自动取武器的自动化状态；不负责动作执行与配置声明。
using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoAcquireWeapon
{
    internal static class AutoAcquireWeaponState
    {
        public static EGameState LastState = EGameState.NoneState;
        public static bool DoneThisRound;
        public static float PhaseEnterRealtime = -1f;
        public static float NextTryRealtime;
        public static int Attempts;

        public static void BeginRound()
        {
            DoneThisRound = false;
            PhaseEnterRealtime = Time.realtimeSinceStartup;
            NextTryRealtime = 0f;
            Attempts = 0;
        }

        public static void MarkLeft() => DoneThisRound = true;

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
