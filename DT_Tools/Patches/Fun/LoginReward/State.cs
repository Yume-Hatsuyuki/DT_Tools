namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>运行期状态：一次会话的发放/弹窗流程记忆与等待时钟。</summary>
    internal static class LoginRewardState
    {
        /// <summary>
        /// 等待掉落批次完成的兜底超时。0.1.17a FreeCurrencyDrops 串行 TriggerItemDrop：
        /// 单次结果 20s、空结果复查 65s、失败指数退避至 60s；25 单位批量可能数分钟。
        /// 正常成功/耗尽都会经 OnProgress + FreeDropPendingUnits 回落终结。
        /// </summary>
        public const float PopupTimeoutSec = 300f;

        /// <summary>
        /// 发放在途标记：TryGrant 发请求前写入 InventoryManager._revealEarned。
        /// 真正的 grant 回调只会写 0（未发放）或正数（累计发放量），见 0.1.17a InventoryManager.cs:354-358，
        /// 因此 -1 可区分「回调未到」——请求刚发出、同步 OnChanged 抢跑时据此过滤。
        /// </summary>
        public const int InFlightEarned = -1;

        /// <summary>已订阅 OnChanged 的库存管理器（Init 可能多次触发，按实例判重）。</summary>
        public static InventoryManager Inv;

        /// <summary>本次会话已发起过发放（含仅动画）。</summary>
        public static bool Attempted;

        /// <summary>等待掉落批次完成 / 等待弹窗条件就绪。</summary>
        public static bool WaitingReveal;

        /// <summary>掉落批次已结束（FreeDropPendingUnits 回落到请求前基线）。</summary>
        public static bool RevealResolved;

        /// <summary>弹窗流程已终结（成功播放、拒绝终态或超时放弃）。</summary>
        public static bool PopupDone;

        /// <summary>GrantAmount=0 仅强制动画（不请求掉落）。</summary>
        public static bool FakeOnly;

        /// <summary>正在执行 GrantFreeFromStars（其内部会同步触发 OnChanged）。</summary>
        public static bool InGrantCall;

        /// <summary>GrantFreeFromStars 调用期间出现过同步 OnChanged（同步终态标志，如 Steam 未初始化）。</summary>
        public static bool SyncChangedDuringGrant;

        /// <summary>
        /// 发起请求前的 FreeDropPendingUnits 基线（0.1.17a SaveManager.cs:116）。
        /// 批次完成时 pending 回落到 ≤ 此值；中间 OnProgress 不得当作终态。
        /// </summary>
        public static int PendingBaseline;

        /// <summary>发起请求前的 DailyEarned，用于日志与伪动画。</summary>
        public static int DailyBefore;

        public static float RetryAt = -1f;
        public static float PopupRetryAt = -1f;
        public static float GrantStartedAt = -1f;

        /// <summary>强制动画用的伪 before/earned（当日已满仍可看演出）。</summary>
        public static int FakeBefore;
        public static int FakeEarned;

        /// <summary>OnDisabled 清理：销毁时钟与等待标志；Attempted/PopupDone 保持会话级不重置。</summary>
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
