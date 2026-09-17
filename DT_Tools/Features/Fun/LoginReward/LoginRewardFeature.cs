using HarmonyLib;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Fun
{
    /// <summary>
    /// 登录奖励（趣味）：进入游戏并完成库存初始化后，自动领取当日剩余实验数据至每日上限 250，
    /// 并弹出原版 UI_DailyCapPopup 进度动画。
    /// 入账走 InventoryManager.GrantFreeFromStars → TiamatPayClient（后端按 dailyTotal 钳制）。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "LoginReward",
        description: "登录奖励：进入游戏后自动领取当日剩余实验数据至每日上限（250），并播放获取动画。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class LoginRewardFeature
    {
        private static bool _attemptedThisSession;
        private static bool _subscribed;
        private static bool _waitingReveal;
        private static float _retryAt = -1f;
        private static float _popupRetryAt = -1f;

        /// <summary>请求发放量用金星等价（一颗金星=30币）。
        /// 9×30=270，经 GrantFreeFromStars 再 min(250)，后端再按当日剩余钳制。</summary>
        private const int RequestGoldStars = 9;

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.Init))]
        [HarmonyPostfix]
        private static void AfterInventoryInit(InventoryManager __instance)
        {
            if (!_subscribed)
            {
                _subscribed = true;
                __instance.OnChanged += OnInventoryChanged;
            }

            if (_attemptedThisSession) return;

            _retryAt = Time.unscaledTime + 3f;
            EnsureTicker();
        }

        private static void OnInventoryChanged()
        {
            if (!_attemptedThisSession)
            {
                TryGrant();
                return;
            }

            // 发放回调后 OnChanged：尝试弹出日限动画
            if (_waitingReveal)
                TryShowPopup();
        }

        private static void EnsureTicker()
        {
            if (LoginRewardTicker.Instance != null) return;
            var go = new GameObject("DT_LoginRewardTicker");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<LoginRewardTicker>();
        }

        internal static void Tick()
        {
            if (!_attemptedThisSession)
            {
                if (_retryAt >= 0f && Time.unscaledTime >= _retryAt)
                    TryGrant();
                return;
            }

            if (_waitingReveal && _popupRetryAt >= 0f && Time.unscaledTime >= _popupRetryAt)
                TryShowPopup();
        }

        private static void TryGrant()
        {
            if (_attemptedThisSession) return;

            if (Managers.Inventory == null)
            {
                _retryAt = Time.unscaledTime + 2f;
                return;
            }

            if (Managers.Steam == null || !Managers.Steam.IsInitialized)
            {
                _retryAt = Time.unscaledTime + 2f;
                return;
            }

            int earned = Managers.Inventory.DailyEarned;
            int cap = Managers.Inventory.DailyCap;
            if (earned >= cap)
            {
                _attemptedThisSession = true;
                Debug.Log($"[DT_Tools][LoginReward] 今日实验数据已达上限 {earned}/{cap}，跳过发放。");
                return;
            }

            _attemptedThisSession = true;
            _waitingReveal = true;
            _popupRetryAt = Time.unscaledTime + 1.5f; // 后端异步回包后再弹
            int remain = cap - earned;
            Debug.Log($"[DT_Tools][LoginReward] 登录奖励：请求发放实验数据（今日已领 {earned}/{cap}，目标补满剩余约 {remain}）……");

            Managers.Inventory.GrantFreeFromStars(RequestGoldStars, 0);
            EnsureTicker();
        }

        /// <summary>
        /// 使用原版 UI_DailyCapPopup.Play 播放余额/进度条动画（与对局结算日限演出相同）。
        /// </summary>
        private static void TryShowPopup()
        {
            if (!_waitingReveal) return;

            var inv = Managers.Inventory;
            if (inv == null || !inv.HasPendingDailyReveal)
            {
                // 后端可能尚未回包，稍后再试
                if (Time.unscaledTime - _popupRetryAt < 15f)
                {
                    _popupRetryAt = Time.unscaledTime + 1f;
                    return;
                }
                _waitingReveal = false;
                Debug.LogWarning("[DT_Tools][LoginReward] 等待发放结果超时，跳过动画。");
                return;
            }

            // 避免与商店等 KeyUI 冲突
            if (Managers.UI != null)
            {
                if (Managers.UI.FindOpenKeyUI<UI_ShopPopup>() != null ||
                    Managers.UI.FindOpenKeyUI<UI_DailyCapPopup>() != null)
                {
                    _popupRetryAt = Time.unscaledTime + 1f;
                    return;
                }

                // 加载中稍等
                if (Managers.UI.IsLoading)
                {
                    _popupRetryAt = Time.unscaledTime + 0.5f;
                    return;
                }
            }

            inv.ConsumeDailyReveal(out int before, out int earned);
            _waitingReveal = false;

            if (earned <= 0)
            {
                Debug.Log("[DT_Tools][LoginReward] 后端 granted=0（已满或失败），不播放动画。");
                return;
            }

            Debug.Log($"[DT_Tools][LoginReward] 播放日限获取动画 — before:{before} earned:{earned} balance:{inv.FreeBalance}");
            Managers.UI.ShowKeyUI<UI_DailyCapPopup>().Play(inv.FreeBalance, before, earned, inv.DailyCap);
        }

        private sealed class LoginRewardTicker : MonoBehaviour
        {
            public static LoginRewardTicker Instance { get; private set; }

            private void Awake()
            {
                if (Instance != null)
                {
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
            }

            private void Update()
            {
                Tick();
                if (_attemptedThisSession && !_waitingReveal)
                    enabled = false;
            }

            private void OnDestroy()
            {
                if (Instance == this)
                    Instance = null;
            }
        }
    }
}
