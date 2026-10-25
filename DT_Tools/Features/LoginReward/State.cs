namespace DT_Tools.Features.LoginReward
{
    internal static class LoginRewardState
    {
        public const float PopupTimeoutSec = 300f;

        public const int InFlightEarned = -1;

        public static InventoryManager Inv;

        public static bool Attempted;

        public static bool WaitingReveal;

        public static bool RevealResolved;

        public static bool PopupDone;

        public static bool FakeOnly;

        public static bool InGrantCall;

        public static bool SyncChangedDuringGrant;

        public static int PendingBaseline;

        public static int DailyBefore;

        public static float RetryAt = -1f;
        public static float PopupRetryAt = -1f;
        public static float GrantStartedAt = -1f;

        public static int FakeBefore;
        public static int FakeEarned;

        public static void ResetWaiting()
        {
            WaitingReveal = false;
            RevealResolved = false;
            InGrantCall = false;
            SyncChangedDuringGrant = false;
            PendingBaseline = 0;
            DailyBefore = 0;
            RetryAt = -1f;
            PopupRetryAt = -1f;
            GrantStartedAt = -1f;
        }
    }
}
