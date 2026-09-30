namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>运行期状态：一次会话的发放/弹窗流程记忆与等待时钟。</summary>
    internal static class LoginRewardState
    {
        /// <summary>
        /// 等待后端结果的兜底超时。正常情况下成功/拒绝都会经回调 OnChanged 立即终结；
        /// 仅在 WebApiTicketQueue 排队极端卡住（回调迟迟不到）时防止永久等待。
        /// </summary>
        public const float PopupTimeoutSec = 60f;

        /// <summary>已订阅 OnChanged 的库存管理器（Init 可能多次触发，按实例判重）。</summary>
        public static InventoryManager Inv;

        /// <summary>本次会话已发起过发放（含仅动画）。</summary>
        public static bool Attempted;

        /// <summary>等待后端结果 / 等待弹窗条件就绪。</summary>
        public static bool WaitingReveal;

        /// <summary>后端回调已到达（经 OnChanged 观测到终态 earned）。</summary>
        public static bool RevealResolved;

        /// <summary>弹窗流程已终结（成功播放、拒绝终态或超时放弃）。</summary>
        public static bool PopupDone;

        /// <summary>GrantAmount=0 仅强制动画（不请求后端）。</summary>
        public static bool FakeOnly;

        /// <summary>正在执行 GrantFreeFromStars（其内部会同步触发 OnChanged）。</summary>
        public static bool InGrantCall;

        /// <summary>GrantFreeFromStars 调用期间出现过同步 OnChanged（同步终态标志）。</summary>
        public static bool SyncChangedDuringGrant;

        public static float RetryAt = -1f;
        public static float PopupRetryAt = -1f;
        public static float GrantStartedAt = -1f;

        /// <summary>强制动画用的伪 before/earned（服务端已满仍可看演出）。</summary>
        public static int FakeBefore;
        public static int FakeEarned;

        /// <summary>OnDisabled 清理：销毁时钟与等待标志；Attempted/PopupDone 保持会话级不重置。</summary>
        public static void ResetWaiting()
        {
            WaitingReveal = false;
            RevealResolved = false;
            InGrantCall = false;
            SyncChangedDuringGrant = false;
            RetryAt = -1f;
            PopupRetryAt = -1f;
            GrantStartedAt = -1f;
        }
    }
}
