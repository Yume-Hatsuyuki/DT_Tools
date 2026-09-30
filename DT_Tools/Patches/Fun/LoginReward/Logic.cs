using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// 发放与弹窗流程：
    ///   TryGrant → InventoryManager.GrantFreeFromStars（0.1.15b 走官方后端，
    ///   服务端权威 + 每日限额）→ 回调经 OnChanged 到达 → TryShowPopup 终态裁决。
    /// 终态规则：earned&gt;0 走官方动画；earned&lt;=0 立即视为「服务端当日已满或拒绝」，
    /// 不再死等（旧版在此死等 60 秒超时，即 0.1.15b 下登录奖励失效的根因）。
    /// </summary>
    internal static class LoginRewardLogic
    {
        /// <summary>
        /// 订阅库存回调并安排首次发放尝试（Init 后缀与 OnEnabled 热开启共用）。
        /// Init 可能多次触发（每次启动流程），按实例判重。
        /// </summary>
        public static void Attach(InventoryManager inv)
        {
            if (inv == null)
                return;

            if (LoginRewardState.Inv != inv)
            {
                if (LoginRewardState.Inv != null)
                    LoginRewardState.Inv.OnChanged -= OnInventoryChanged;
                LoginRewardState.Inv = inv;
                inv.OnChanged += OnInventoryChanged;
            }

            if (LoginRewardState.Attempted)
                return;

            LoginRewardState.RetryAt = Time.unscaledTime + 3f;
            LoginRewardTicker.Ensure();
        }

        /// <summary>主线程轮询（Ticker 调用）：到点发起发放 / 推进弹窗等待。</summary>
        public static void Tick()
        {
            if (!Engine.Enabled<LoginRewardFeature>())
                return;

            if (!LoginRewardState.Attempted)
            {
                if (LoginRewardState.RetryAt >= 0f && Time.unscaledTime >= LoginRewardState.RetryAt)
                    TryGrant();
                return;
            }

            if (LoginRewardState.WaitingReveal && !LoginRewardState.PopupDone
                && LoginRewardState.PopupRetryAt >= 0f && Time.unscaledTime >= LoginRewardState.PopupRetryAt)
                TryShowPopup();
        }

        /// <summary>InventoryManager.OnChanged 回调：首次库存就绪即尝试；等待中观测终态。</summary>
        public static void OnInventoryChanged()
        {
            if (!Engine.Enabled<LoginRewardFeature>())
                return;

            if (!LoginRewardState.Attempted)
            {
                TryGrant();
                return;
            }

            // GrantFreeFromStars 调用期间的同步 OnChanged（其末尾必触发一次，
            // 失败路径经回调还会再来一次）不算终态到达
            if (LoginRewardState.InGrantCall)
            {
                LoginRewardState.SyncChangedDuringGrant = true;
                return;
            }

            if (LoginRewardState.WaitingReveal && !LoginRewardState.PopupDone)
            {
                LoginRewardState.RevealResolved = true;
                TryShowPopup();
            }
        }

        // ── 发放 ──

        private static void TryGrant()
        {
            if (LoginRewardState.Attempted)
                return;

            var inv = Managers.Inventory;
            if (inv == null)
            {
                LoginRewardState.RetryAt = Time.unscaledTime + 2f;
                return;
            }

            if (Managers.Steam == null || !Managers.Steam.IsInitialized)
            {
                LoginRewardState.RetryAt = Time.unscaledTime + 2f;
                return;
            }

            // DailyEarned → 本地记账 Save.FreeEarnedToday（0.1.15b SteamInventorySource.cs:143）；
            // DailyCap：0.1.15b InventoryManager.cs:38（public 属性，原版即硬编码 250）
            int earnedToday = inv.DailyEarned;
            int cap = inv.DailyCap;
            int want = Mathf.Clamp(LoginRewardFeature.GrantAmount, 0, cap);
            bool forceAnim = LoginRewardFeature.AlwaysShowAnimation;

            // 非强制动画且已达上限 → 跳过
            if (!forceAnim && earnedToday >= cap)
            {
                LoginRewardState.Attempted = true;
                LoginRewardState.PopupDone = true;
                Log.Info<LoginRewardFeature>($"今日已满 {earnedToday}/{cap}，跳过（AlwaysShowAnimation=false）。");
                return;
            }

            // 请求量为 0：仅调试动画
            if (want <= 0 && !forceAnim)
            {
                LoginRewardState.Attempted = true;
                LoginRewardState.PopupDone = true;
                Log.Info<LoginRewardFeature>("GrantAmount=0 且未强制动画，跳过。");
                return;
            }

            LoginRewardState.Attempted = true;
            LoginRewardState.WaitingReveal = true;
            LoginRewardState.PopupDone = false;
            LoginRewardState.RevealResolved = false;
            LoginRewardState.GrantStartedAt = Time.unscaledTime;
            LoginRewardState.PopupRetryAt = Time.unscaledTime + 2f;

            // 伪动画参数（服务端已满/拒绝时用）
            LoginRewardState.FakeBefore = Mathf.Clamp(earnedToday, 0, cap);
            LoginRewardState.FakeEarned = Mathf.Max(1, Mathf.Min(want, Mathf.Max(1, cap - LoginRewardState.FakeBefore)));
            if (LoginRewardState.FakeBefore >= cap)
            {
                // 已满时演示「涨满」：before = cap - fakeEarned
                LoginRewardState.FakeEarned = Mathf.Clamp(want > 0 ? want : 50, 1, cap);
                LoginRewardState.FakeBefore = Mathf.Max(0, cap - LoginRewardState.FakeEarned);
            }

            if (want > 0)
            {
                AmountToStars(want, cap, out int gold, out int silver);
                Log.Info<LoginRewardFeature>(
                    $"请求发放 amount={want} → 金星={gold} 银星={silver}（今日 {earnedToday}/{cap}，ForceAnim={forceAnim}）");

                // 0.1.15b InventoryManager.cs:349：置 pending 后经服务端异步发放，
                // 其末尾会同步触发一次 OnChanged（InventoryManager.cs:360），先标记调用窗口
                LoginRewardState.InGrantCall = true;
                try
                {
                    inv.GrantFreeFromStars(gold, silver);
                }
                finally
                {
                    LoginRewardState.InGrantCall = false;
                }

                // 失败路径（Steam 未初始化 / amount≤0）会在调用内同步回调 → 已是终态
                if (LoginRewardState.SyncChangedDuringGrant)
                    LoginRewardState.RevealResolved = true;
            }
            else
            {
                // 仅动画：不走后端
                LoginRewardState.FakeOnly = true;
                Log.Info<LoginRewardFeature>("GrantAmount=0，仅准备强制动画。");
            }

            LoginRewardTicker.Ensure();
        }

        /// <summary>
        /// 实验数据 → 金星/银星（gold×30+silver×10）。上限读库存组件 DailyCap
        /// （0.1.15b InventoryManager.cs:38，原版即硬编码 250），由调用方传入。
        /// </summary>
        private static void AmountToStars(int amount, int cap, out int gold, out int silver)
        {
            int value = Mathf.Clamp(amount, 0, cap);
            gold = value / 30;
            int rem = value % 30;
            silver = rem / 10;
            // 余数 <10 无法用银星表示，多请求 1 银星再由 min(...,cap) 与服务端钳制
            if (rem % 10 != 0 && gold * 30 + (silver + 1) * 10 <= cap)
                silver++;
        }

        // ── 弹窗 ──

        private static void TryShowPopup()
        {
            if (!LoginRewardState.WaitingReveal || LoginRewardState.PopupDone)
                return;

            var inv = Managers.Inventory;

            // 兜底超时：正常成功/拒绝都会经回调立即终结，仅防极端排队卡死
            if (LoginRewardState.GrantStartedAt > 0f
                && Time.unscaledTime - LoginRewardState.GrantStartedAt > LoginRewardState.PopupTimeoutSec)
            {
                Log.Warn<LoginRewardFeature>("等待后端结果超时，放弃动画。");
                EndWaiting();
                return;
            }

            if (inv == null)
            {
                LoginRewardState.PopupRetryAt = Time.unscaledTime + 1f;
                return;
            }

            if (Managers.UI == null || Managers.UI.IsLoading)
            {
                LoginRewardState.PopupRetryAt = Time.unscaledTime + 0.5f;
                return;
            }

            if (Managers.UI.FindOpenKeyUI<UI_ShopPopup>() != null
                || Managers.UI.FindOpenKeyUI<UI_DailyCapPopup>() != null)
            {
                LoginRewardState.PopupRetryAt = Time.unscaledTime + 1f;
                return;
            }

            var sceneUi = Managers.UI.SceneUI as UI_GameScene;

            // 仅动画（GrantAmount=0 + AlwaysShowAnimation）：不等后端，直接演出
            if (LoginRewardState.FakeOnly)
            {
                PlayFakeAnimation(inv, sceneUi);
                EndWaiting();
                return;
            }

            // 后端已回包 → 终态裁决
            if (LoginRewardState.RevealResolved)
            {
                int earned = inv.LastRevealEarned;
                if (earned > 0)
                {
                    // 官方路径：仅真实发放。游戏自己进入大厅时也会调 TryShowDailyCapPopup
                    //（0.1.15b UIManager.cs:208）；pending 已被消费时该方法为安全 no-op
                    Log.Info<LoginRewardFeature>($"服务端发放 earned={earned}，播放官方动画。");
                    if (sceneUi != null)
                        sceneUi.TryShowDailyCapPopup();     // 0.1.15b UI_GameScene.cs:2914
                    else
                        PlayDirect(inv.FreeBalance, earned);
                }
                else
                {
                    // 终态：服务端当日已满或拒绝发放（granted=0）。
                    // 拒绝路径同样要消费 pending——GrantFreeFromStars 先置位
                    // _pendingDailyReveal（0.1.15b InventoryManager.cs:351），仅
                    // ConsumeDailyReveal 复位（InventoryManager.cs:363-368）；不消费的话
                    // 官方 "+0" 弹窗仍会被 UI_GameScene.TryShowDailyCapPopup
                    // （0.1.15b UI_GameScene.cs:2914）拉出，与"不播放官方动画"的文档不符
                    if (inv.HasPendingDailyReveal)                    // InventoryManager.cs:34
                        inv.ConsumeDailyReveal(out _, out _);
                    Log.Info<LoginRewardFeature>(
                        $"服务端当日已满或拒绝发放（granted=0，今日 {inv.DailyEarned}/{inv.DailyCap}）。");
                    if (LoginRewardFeature.AlwaysShowAnimation)
                        PlayFakeAnimation(inv, sceneUi);   // 此时 pending 已清，内部消费为幂等
                }
                EndWaiting();
                return;
            }

            // 仍在等后端回包：失败/成功路径都会回调，短暂重试即可（不靠超时兜底）
            LoginRewardState.PopupRetryAt = Time.unscaledTime + 1f;
        }

        private static void PlayFakeAnimation(InventoryManager inv, UI_GameScene sceneUi)
        {
            long balance = inv.FreeBalance;
            int before = LoginRewardState.FakeBefore;
            int earned = LoginRewardState.FakeEarned;
            int cap = inv.DailyCap;

            // 有 pending（被拒绝后未消费）先 Consume 清掉，避免卡住后续流程
            if (inv.HasPendingDailyReveal)
                inv.ConsumeDailyReveal(out _, out _);

            Log.Info<LoginRewardFeature>(
                $"强制动画 Play(balance={balance}, before={before}, earned={earned}, cap={cap})");
            if (sceneUi != null)
            {
                // 已 Consume 则不能走官方 TryShowDailyCapPopup，直接 Play（0.1.15b UI_DailyCapPopup.cs:95）
                Managers.UI.ShowKeyUI<UI_DailyCapPopup>().Play(balance, before, earned, cap);
            }
            else
            {
                PlayDirect(balance, earned, before);
            }
        }

        private static void PlayDirect(long balance, int earned, int before = -1)
        {
            var inv = Managers.Inventory;
            int cap = inv != null ? inv.DailyCap : 250;
            int beforeVal = before >= 0
                ? before
                : (inv != null ? Mathf.Max(0, inv.DailyEarned - earned) : 0);
            int earnedVal = earned;
            if (inv != null && inv.HasPendingDailyReveal)
                inv.ConsumeDailyReveal(out beforeVal, out earnedVal);
            Managers.UI.ShowKeyUI<UI_DailyCapPopup>().Play(balance, beforeVal, earnedVal, cap);
        }

        private static void EndWaiting()
        {
            LoginRewardState.WaitingReveal = false;
            LoginRewardState.PopupDone = true;
            LoginRewardState.PopupRetryAt = -1f;
        }
    }
}
