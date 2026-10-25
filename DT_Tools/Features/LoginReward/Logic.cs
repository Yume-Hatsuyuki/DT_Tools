using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.LoginReward
{
    internal static class LoginRewardLogic
    {
        private static readonly global::System.Reflection.FieldInfo RevealEarnedField =
            AccessTools.Field(typeof(InventoryManager), "_revealEarned");

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
                if (!LoginRewardState.RevealResolved && !LoginRewardState.FakeOnly)
                    TryResolveBatch();

                if (LoginRewardState.PopupRetryAt >= 0f && Time.unscaledTime >= LoginRewardState.PopupRetryAt)
                    TryShowPopup();
            }
        }

        public static void OnInventoryChanged()
        {
            if (!Engine.Enabled<LoginRewardFeature>())
                return;

            if (!LoginRewardState.Attempted)
            {
                TryGrant();
                return;
            }

            if (LoginRewardState.InGrantCall)
            {
                LoginRewardState.SyncChangedDuringGrant = true;
                return;
            }

            if (LoginRewardState.WaitingReveal && !LoginRewardState.PopupDone && !LoginRewardState.FakeOnly)
            {
                var waitingInv = Managers.Inventory;
                if (waitingInv != null && waitingInv.LastRevealEarned == LoginRewardState.InFlightEarned)
                    return;

                TryResolveBatch();
                if (LoginRewardState.RevealResolved)
                    TryShowPopup();
            }
        }

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

                if (RevealEarnedField != null)
                    RevealEarnedField.SetValue(inv, LoginRewardState.InFlightEarned);
                else
                    Log.Warn<LoginRewardFeature>(
                        "InventoryManager._revealEarned 缺失（游戏升级？）——在途标记不可用（0.1.18c InventoryManager.cs:14 核对）。");

                LoginRewardState.InGrantCall = true;
                try
                {
                    inv.GrantFreeFromStars(gold, silver);
                }
                finally
                {
                    LoginRewardState.InGrantCall = false;
                }

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

        private static void AmountToStars(int amount, int cap, out int gold, out int silver)
        {
            int value = Mathf.Clamp(amount, 0, cap);
            gold = value / 30;
            int rem = value % 30;
            silver = rem / 10;
            if (rem % 10 != 0 && gold * 30 + (silver + 1) * 10 <= cap)
                silver++;
        }

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
                        sceneUi.TryShowDailyCapPopup();
                    else
                        PlayDirect(inv.FreeBalance, earned);
                }
                else
                {
                    if (inv.HasPendingDailyReveal)
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
