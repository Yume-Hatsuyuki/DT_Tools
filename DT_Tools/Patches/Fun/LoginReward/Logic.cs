using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// 发放与弹窗流程（0.1.17a）：
    ///   TryGrant → InventoryManager.GrantFreeFromStars（InventoryManager.cs:349）
    ///   → SteamInventorySource.GrantFreeFromStars（:964）折算单位后 FreeCurrencyDrops.Enqueue
    ///   → 串行 SteamInventory.TriggerItemDrop；OnProgress 可能多次中间回调
    ///   → 终态：FreeDropPendingUnits 回落到请求前基线后读 LastRevealEarned → TryShowPopup。
    ///
    /// 终态规则：earned&gt;0 走官方动画；earned≤0 立即视为「当日已满/未发放」，
    /// 不再死等。中间 OnProgress（单次 +10）不得提前裁决。
    ///
    /// 在途竞态：GrantFreeFromStars 入口把 _revealEarned 清 0（InventoryManager.cs:353），
    /// 真终态要等 drop 回调写入（:356）。TryGrant 发请求前把 _revealEarned 置 InFlightEarned(-1)，
    /// 同步 OnChanged 见 -1 即过滤；批次完成判定另看 FreeDropPendingUnits。
    /// </summary>
    internal static class LoginRewardLogic
    {
        /// <summary>
        /// 0.1.17a InventoryManager.cs:14——公开属性 LastRevealEarned 的后备字段；
        /// 写点 :353（入口清 0）与 :356（grant 回调写终值）。游戏升级后按行号复核；
        /// 仅写需要反射（读走公开属性），字段缺失时跳过标记并告警。
        /// </summary>
        // 注意:Patches 命名空间链遮蔽全局 System(AGENTS.md §9 约束 3),须 global:: 全限定
        private static readonly global::System.Reflection.FieldInfo RevealEarnedField =
            AccessTools.Field(typeof(InventoryManager), "_revealEarned");

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

        /// <summary>主线程轮询（Ticker 调用）：到点发起发放 / 推进弹窗等待 / 判定掉落批次完成。</summary>
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

            if (LoginRewardState.WaitingReveal && !LoginRewardState.PopupDone)
            {
                // 掉落批次完成：pending 回落到请求前基线（中间 OnProgress 不在此提前裁决）
                if (!LoginRewardState.RevealResolved && !LoginRewardState.FakeOnly)
                    TryResolveBatch();

                if (LoginRewardState.PopupRetryAt >= 0f && Time.unscaledTime >= LoginRewardState.PopupRetryAt)
                    TryShowPopup();
            }
        }

        /// <summary>InventoryManager.OnChanged 回调：首次库存就绪即尝试；在途过滤同步抢跑。</summary>
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

            if (LoginRewardState.WaitingReveal && !LoginRewardState.PopupDone && !LoginRewardState.FakeOnly)
            {
                // 在途过滤：抢跑的 OnChanged 时 _revealEarned 仍为 -1
                var waitingInv = Managers.Inventory;
                if (waitingInv != null && waitingInv.LastRevealEarned == LoginRewardState.InFlightEarned)
                    return;

                // 不在此立即 RevealResolved——等 pending 回落，避免中间 +10 抢跑
                TryResolveBatch();
                if (LoginRewardState.RevealResolved)
                    TryShowPopup();
            }
        }

        /// <summary>
        /// 判定 FreeCurrencyDrops 批次是否结束。
        /// pending ≤ 请求前基线 且 LastRevealEarned 已离开在途标记 → 终态。
        /// </summary>
        private static void TryResolveBatch()
        {
            if (LoginRewardState.RevealResolved)
                return;

            int pending = 0;
            if (Managers.Save != null)
                pending = Managers.Save.FreeDropPendingUnits;

            if (pending > LoginRewardState.PendingBaseline)
                return;

            var inv = Managers.Inventory;
            if (inv != null && inv.LastRevealEarned == LoginRewardState.InFlightEarned)
                return;

            LoginRewardState.RevealResolved = true;
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

            // DailyEarned → FreeCurrencyDrops.DailyUsed（0.1.17a SteamInventorySource.cs:145）
            // DailyCap：0.1.17a InventoryManager.cs:38（public 属性，原版即硬编码 250）
            int earnedToday = inv.DailyEarned;
            int cap = inv.DailyCap;
            int want = Mathf.Clamp(LoginRewardFeature.GrantAmount, 0, cap);
            bool forceAnim = LoginRewardFeature.AlwaysShowAnimation;

            if (!forceAnim && earnedToday >= cap)
            {
                LoginRewardState.Attempted = true;
                LoginRewardState.PopupDone = true;
                Log.Info<LoginRewardFeature>($"今日已满 {earnedToday}/{cap}，跳过（AlwaysShowAnimation=false）。");
                return;
            }

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
            LoginRewardState.DailyBefore = earnedToday;
            LoginRewardState.PendingBaseline = Managers.Save != null
                ? Managers.Save.FreeDropPendingUnits
                : 0;

            LoginRewardState.FakeBefore = Mathf.Clamp(earnedToday, 0, cap);
            LoginRewardState.FakeEarned = Mathf.Max(1, Mathf.Min(want, Mathf.Max(1, cap - LoginRewardState.FakeBefore)));
            if (LoginRewardState.FakeBefore >= cap)
            {
                LoginRewardState.FakeEarned = Mathf.Clamp(want > 0 ? want : 50, 1, cap);
                LoginRewardState.FakeBefore = Mathf.Max(0, cap - LoginRewardState.FakeEarned);
            }

            if (want > 0)
            {
                AmountToStars(want, cap, out int gold, out int silver);
                Log.Info<LoginRewardFeature>(
                    $"请求掉落发放 amount={want} → 金星={gold} 银星={silver}（今日 {earnedToday}/{cap}，pending基线={LoginRewardState.PendingBaseline}，ForceAnim={forceAnim}）");

                // 在途标记：GrantFreeFromStars 入口会清 0（0.1.17a InventoryManager.cs:353）
                if (RevealEarnedField != null)
                    RevealEarnedField.SetValue(inv, LoginRewardState.InFlightEarned);
                else
                    Log.Warn<LoginRewardFeature>(
                        "InventoryManager._revealEarned 缺失（游戏升级？）——在途标记不可用（0.1.17a InventoryManager.cs:14 核对）。");

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
                LoginRewardState.FakeOnly = true;
                Log.Info<LoginRewardFeature>("GrantAmount=0，仅准备强制动画。");
            }

            LoginRewardTicker.Ensure();
        }

        /// <summary>
        /// 实验数据 → 金星/银星（gold×30+silver×10）。上限读库存组件 DailyCap
        /// （0.1.17a InventoryManager.cs:38，原版即硬编码 250），由调用方传入。
        /// SteamInventorySource 再按 num/10 折成 drop 单位（每单位 10）。
        /// </summary>
        private static void AmountToStars(int amount, int cap, out int gold, out int silver)
        {
            int value = Mathf.Clamp(amount, 0, cap);
            gold = value / 30;
            int rem = value % 30;
            silver = rem / 10;
            if (rem % 10 != 0 && gold * 30 + (silver + 1) * 10 <= cap)
                silver++;
        }

        // ── 弹窗 ──

        private static void TryShowPopup()
        {
            if (!LoginRewardState.WaitingReveal || LoginRewardState.PopupDone)
                return;

            var inv = Managers.Inventory;

            if (LoginRewardState.GrantStartedAt > 0f
                && Time.unscaledTime - LoginRewardState.GrantStartedAt > LoginRewardState.PopupTimeoutSec)
            {
                Log.Warn<LoginRewardFeature>("等待 Steam 掉落结果超时，放弃动画。");
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

            if (LoginRewardState.FakeOnly)
            {
                PlayFakeAnimation(inv, sceneUi);
                EndWaiting();
                return;
            }

            if (LoginRewardState.RevealResolved)
            {
                int earned = inv.LastRevealEarned;
                if (earned > 0)
                {
                    Log.Info<LoginRewardFeature>(
                        $"掉落发放 earned={earned}，播放官方动画（今日 {inv.DailyEarned}/{inv.DailyCap}）。");
                    if (sceneUi != null)
                        sceneUi.TryShowDailyCapPopup();     // 0.1.17a UI_GameScene.cs:2922
                    else
                        PlayDirect(inv.FreeBalance, earned);
                }
                else
                {
                    // 未发放：消费 pending，避免官方 "+0" 弹窗被 TryShowDailyCapPopup 拉出
                    if (inv.HasPendingDailyReveal)                    // InventoryManager.cs:34
                        inv.ConsumeDailyReveal(out _, out _);
                    Log.Info<LoginRewardFeature>(
                        $"掉落未发放（granted=0，今日 {inv.DailyEarned}/{inv.DailyCap}）。");
                    if (LoginRewardFeature.AlwaysShowAnimation)
                        PlayFakeAnimation(inv, sceneUi);
                }
                EndWaiting();
                return;
            }

            LoginRewardState.PopupRetryAt = Time.unscaledTime + 1f;
        }

        private static void PlayFakeAnimation(InventoryManager inv, UI_GameScene sceneUi)
        {
            long balance = inv.FreeBalance;
            int before = LoginRewardState.FakeBefore;
            int earned = LoginRewardState.FakeEarned;
            int cap = inv.DailyCap;

            if (inv.HasPendingDailyReveal)
                inv.ConsumeDailyReveal(out _, out _);

            Log.Info<LoginRewardFeature>(
                $"强制动画 Play(balance={balance}, before={before}, earned={earned}, cap={cap})");
            if (sceneUi != null)
            {
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
