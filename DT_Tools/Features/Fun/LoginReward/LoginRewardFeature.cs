using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Fun
{
    /// <summary>
    /// 登录奖励（趣味）：库存初始化后按配置请求实验数据；
    /// 动画在 UI_GameScene 就绪且（有真实发放 或 强制播动画）时播放。
    ///
    /// 换算：GrantFreeFromStars → min(gold×30 + silver×10, 250)，后端再按当日剩余钳制。
    /// 默认 250 = 8 金星 + 1 银星（8×30+1×10）。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "LoginReward",
        description: "每日登录奖励：进入游戏后按配置发放实验数据，并在房间 UI 就绪后播放获取动画。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class LoginRewardFeature
    {
        // ── 配置（BepInEx Config，节名 LoginReward）──
        internal static ConfigEntry<bool> AlwaysShowAnimation;
        internal static ConfigEntry<int> GrantAmount;

        private static bool _configBound;
        private static bool _attemptedThisSession;
        private static bool _subscribed;
        private static bool _waitingReveal;
        private static bool _popupDone;
        private static float _retryAt = -1f;
        private static float _popupRetryAt = -1f;
        private static float _grantStartedAt = -1f;

        /// <summary>强制动画时用的伪 before/earned（后端已满仍可看演出）。</summary>
        private static int _fakeBefore;
        private static int _fakeEarned;

        private const float PopupTimeoutSec = 60f;

        /// <summary>
        /// 由插件 / Feature 加载器在 Awake 时调用；若未调用，首次使用时用 Plugin 全局 Config 自绑定。
        /// </summary>
        internal static void BindConfig(ConfigFile cfg)
        {
            if (_configBound || cfg == null) return;
            AlwaysShowAnimation = cfg.Bind(
                "LoginReward",
                "AlwaysShowAnimation",
                false,
                "每次登录都播放获取动画（即使今日已达上限）—— 你就那么爱看动画哦？");
            GrantAmount = cfg.Bind(
                "LoginReward",
                "GrantAmount",
                250,
                new ConfigDescription(
                    "每次登录请求发放的实验数据量（建议为10的倍数）。\n换算为金星=30 银星=10；后端仍按当日剩余钳制。\n默认 250 = 8 金星 + 1 银星。",
                    new AcceptableValueRange<int>(0, 250)));
            _configBound = true;
        }

        private static void EnsureConfig()
        {
            if (_configBound) return;
            try
            {
                // 尝试从已加载的 BepInEx 插件取 Config（DT_Tools 主插件）
                foreach (var p in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                {
                    if (p?.Instance == null) continue;
                    var name = p.Metadata?.GUID ?? "";
                    if (name.IndexOf("DT_Tools", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("DeadlyTrick", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (p.Instance.Config != null)
                    {
                        BindConfig(p.Instance.Config);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DT_Tools][LoginReward] 自动绑定配置失败: " + e.Message);
            }

            // 回退：内存默认值（无写盘）
            if (!_configBound)
            {
                var mem = new ConfigFile(global::System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "DT_Tools.LoginReward.cfg"), true);
                BindConfig(mem);
            }
        }

        private static bool ForceAnim => AlwaysShowAnimation != null && AlwaysShowAnimation.Value;
        private static int RequestAmount => GrantAmount != null ? Mathf.Clamp(GrantAmount.Value, 0, 250) : 250;

        /// <summary>实验数据 → 金星/银星（gold×30+silver×10）。</summary>
        private static void AmountToStars(int amount, out int gold, out int silver)
        {
            int value = Mathf.Clamp(amount, 0, 250);
            gold = value / 30;
            int rem = value % 30;
            silver = rem / 10;
            // 余数 <10 无法用银星表示，多请求 1 银星再由 min(...,250) 与后端钳制
            if (rem % 10 != 0 && gold * 30 + (silver + 1) * 10 <= 250)
                silver++;
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.Init))]
        [HarmonyPostfix]
        private static void AfterInventoryInit(InventoryManager __instance)
        {
            EnsureConfig();

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

            if (_waitingReveal && !_popupDone)
                TryShowPopup();
        }

        private static void EnsureTicker()
        {
            if (LoginRewardTicker.Instance != null)
            {
                LoginRewardTicker.Instance.enabled = true;
                return;
            }
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

            if (_waitingReveal && !_popupDone && _popupRetryAt >= 0f && Time.unscaledTime >= _popupRetryAt)
                TryShowPopup();
        }

        private static void TryGrant()
        {
            if (_attemptedThisSession) return;
            EnsureConfig();

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

            int earnedToday = Managers.Inventory.DailyEarned;
            int cap = Managers.Inventory.DailyCap;
            int want = RequestAmount;

            // 非强制动画且已达上限 → 跳过
            if (!ForceAnim && earnedToday >= cap)
            {
                _attemptedThisSession = true;
                _popupDone = true;
                Debug.Log($"[DT_Tools][LoginReward] 今日已满 {earnedToday}/{cap}，跳过（AlwaysShowAnimation=false）。");
                return;
            }

            // 请求量为 0：仅调试动画
            if (want <= 0 && !ForceAnim)
            {
                _attemptedThisSession = true;
                _popupDone = true;
                Debug.Log("[DT_Tools][LoginReward] GrantAmount=0 且未强制动画，跳过。");
                return;
            }

            _attemptedThisSession = true;
            _waitingReveal = true;
            _popupDone = false;
            _grantStartedAt = Time.unscaledTime;
            _popupRetryAt = Time.unscaledTime + 2f;

            // 伪动画参数（后端已满时用）
            _fakeBefore = Mathf.Clamp(earnedToday, 0, cap);
            _fakeEarned = Mathf.Max(1, Mathf.Min(want, Mathf.Max(1, cap - _fakeBefore)));
            if (_fakeBefore >= cap)
            {
                // 已满时演示「涨满」：before = cap - fakeEarned
                _fakeEarned = Mathf.Clamp(want > 0 ? want : 50, 1, cap);
                _fakeBefore = Mathf.Max(0, cap - _fakeEarned);
            }

            AmountToStars(want, out int gold, out int silver);
            Debug.Log($"[DT_Tools][LoginReward] 请求发放 amount={want} → 金星={gold} 银星={silver}（今日 {earnedToday}/{cap}，ForceAnim={ForceAnim}）");

            if (want > 0)
                Managers.Inventory.GrantFreeFromStars(gold, silver);
            else
            {
                // 仅动画：不走后端，直接等弹窗路径用 fake
                Debug.Log("[DT_Tools][LoginReward] GrantAmount=0，仅准备强制动画。");
            }

            EnsureTicker();
        }

        private static void TryShowPopup()
        {
            if (!_waitingReveal || _popupDone) return;
            EnsureConfig();

            var inv = Managers.Inventory;
            if (inv == null)
            {
                _popupRetryAt = Time.unscaledTime + 1f;
                return;
            }

            if (_grantStartedAt > 0f && Time.unscaledTime - _grantStartedAt > PopupTimeoutSec)
            {
                _waitingReveal = false;
                _popupDone = true;
                Debug.LogWarning("[DT_Tools][LoginReward] 等待超时，放弃动画。");
                return;
            }

            if (Managers.UI == null || Managers.UI.IsLoading)
            {
                _popupRetryAt = Time.unscaledTime + 0.5f;
                return;
            }

            if (Managers.UI.FindOpenKeyUI<UI_ShopPopup>() != null
                || Managers.UI.FindOpenKeyUI<UI_DailyCapPopup>() != null)
            {
                _popupRetryAt = Time.unscaledTime + 1f;
                return;
            }

            var sceneUi = Managers.UI.SceneUI as UI_GameScene;
            if (sceneUi == null)
            {
                // 强制动画时：无 GameScene 也尝试直接 ShowKeyUI（主页调试）
                if (!ForceAnim)
                {
                    _popupRetryAt = Time.unscaledTime + 2f;
                    EnsureTicker();
                    return;
                }
            }

            // 真实发放可用 → 官方路径
            if (inv.HasPendingDailyReveal && inv.LastRevealEarned > 0)
            {
                Debug.Log($"[DT_Tools][LoginReward] 真实发放动画 earned={inv.LastRevealEarned}");
                if (sceneUi != null)
                    sceneUi.TryShowDailyCapPopup();
                else
                    PlayDirect(inv.FreeBalance, inv.LastRevealEarned);
                _waitingReveal = false;
                _popupDone = true;
                return;
            }

            // 仍在等后端回包
            if (RequestAmount > 0 && inv.HasPendingDailyReveal && inv.LastRevealEarned <= 0)
            {
                _popupRetryAt = Time.unscaledTime + 1f;
                return;
            }

            // 强制动画：后端 0 或未请求发放
            if (ForceAnim)
            {
                long balance = inv.FreeBalance;
                int before = _fakeBefore;
                int earned = _fakeEarned;
                // 若有 pending 且 earned=0，先 Consume 清掉，避免卡住
                if (inv.HasPendingDailyReveal)
                    inv.ConsumeDailyReveal(out _, out _);

                Debug.Log($"[DT_Tools][LoginReward] 强制动画 Play(balance={balance}, before={before}, earned={earned}, cap={inv.DailyCap})");
                if (sceneUi != null)
                {
                    // 官方方法要求 HasPendingDailyReveal，已 Consume 则直接 Play
                    Managers.UI.ShowKeyUI<UI_DailyCapPopup>().Play(balance, before, earned, inv.DailyCap);
                }
                else
                {
                    PlayDirect(balance, earned, before);
                }
                _waitingReveal = false;
                _popupDone = true;
                return;
            }

            // 非强制且无发放 → 结束
            if (!inv.HasPendingDailyReveal)
            {
                _waitingReveal = false;
                _popupDone = true;
                Debug.Log("[DT_Tools][LoginReward] 无 pending 且未强制动画，结束。");
            }
            else
            {
                _popupRetryAt = Time.unscaledTime + 1f;
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
                if (_attemptedThisSession && _popupDone)
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
