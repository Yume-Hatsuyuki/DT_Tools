using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Patches.Fun.LoginReward
{
    /// <summary>
    /// 按玩家自设秒数改写 FreeCurrencyDrops 客户端等待（0.1.17a FreeCurrencyDrops.cs）。
    /// 官方硬编码：RESULT_TIMEOUT=20、RECHECK_WAIT=65、RETRY_MIN=2、RETRY_MAX=60。
    /// 功能开启时读取 LoginRewardFeature 的 ResultTimeoutSec / EmptyRecheckSec /
    /// RetryMinSec / RetryMaxSec（默认即官方值）：
    ///   1. Update Prefix：结果超时短于官方时提前结束 in-flight 并 ScheduleRetry；
    ///   2. TryHandleResult Postfix：空结果/Prime 的 _waitUntil 改为配置的复查秒数；
    ///   3. ScheduleRetry Postfix：按配置的下限/上限重写指数退避。
    /// 不改 Steam TriggerItemDrop 本身，也不能突破生成器冷却与每日次数上限。
    /// </summary>
    internal static class LoginRewardDropSpeed
    {
        internal const float VanillaResultTimeout = 20f;

        public static void Resolve(out float resultTimeout, out float recheck, out float retryMin, out float retryMax)
        {
            resultTimeout = Mathf.Max(1f, LoginRewardFeature.ResultTimeoutSec);
            recheck = Mathf.Max(1f, LoginRewardFeature.EmptyRecheckSec);
            retryMin = Mathf.Max(0.5f, LoginRewardFeature.RetryMinSec);
            retryMax = Mathf.Max(retryMin, LoginRewardFeature.RetryMaxSec);
        }

        public static bool ShouldApply()
        {
            return Engine.Enabled<LoginRewardFeature>();
        }
    }

    /// <summary>
    /// FreeCurrencyDrops.Update（public）：0.1.17a FreeCurrencyDrops.cs:131。
    /// Prefix：配置的结果超时短于官方 20s 时，提前结束 in-flight 并重试。
    /// 配置 ≥20 时不干预，由原版 20s 逻辑处理（无法单靠 Prefix 延长）。
    /// </summary>
    [HarmonyPatch(typeof(FreeCurrencyDrops), nameof(FreeCurrencyDrops.Update))]
    internal static class LoginRewardDropSpeedUpdatePatch
    {
        // 注意:Patches 命名空间链遮蔽全局 System(AGENTS.md §9 约束 3),须 global:: 全限定
        private static readonly global::System.Reflection.FieldInfo InFlightField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_inFlight");
        private static readonly global::System.Reflection.FieldInfo InFlightAtField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_inFlightAt");
        private static readonly global::System.Reflection.FieldInfo InFlightHandleField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_inFlightHandle");
        private static readonly global::System.Reflection.FieldInfo InFlightGenField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_inFlightGen");
        private static readonly global::System.Reflection.FieldInfo TimedOutField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_timedOut");
        private static readonly global::System.Reflection.MethodInfo ScheduleRetryMethod =
            AccessTools.Method(typeof(FreeCurrencyDrops), "ScheduleRetry");

        private static void Prefix(FreeCurrencyDrops __instance)
        {
            if (!LoginRewardDropSpeed.ShouldApply())
                return;
            if (InFlightField == null || InFlightAtField == null)
                return;
            if (!(bool)InFlightField.GetValue(__instance))
                return;

            LoginRewardDropSpeed.Resolve(out float resultTimeout, out _, out _, out _);
            // 仅当配置比官方更短时提前触发；≥20 交给原版
            if (resultTimeout >= LoginRewardDropSpeed.VanillaResultTimeout)
                return;

            float inFlightAt = (float)InFlightAtField.GetValue(__instance);
            if (Time.unscaledTime - inFlightAt < resultTimeout)
                return;

            int handle = (int)InFlightHandleField.GetValue(__instance);
            int gen = (int)InFlightGenField.GetValue(__instance);
            InFlightField.SetValue(__instance, false);

            var timedOut = TimedOutField?.GetValue(__instance) as global::System.Collections.Generic.Dictionary<int, int>;
            if (timedOut != null)
                timedOut[handle] = gen;

            Log.Info<LoginRewardFeature>(
                $"掉落等待：生成器 {gen} 结果 {resultTimeout:0.#}s 未到 — 提前重试（官方 20s）");
            ScheduleRetryMethod?.Invoke(__instance, null);
        }
    }

    /// <summary>
    /// FreeCurrencyDrops.TryHandleResult（public）：0.1.17a FreeCurrencyDrops.cs:180。
    /// Postfix：把刚写入的空结果/Prime _waitUntil 改为配置的复查秒数。
    /// </summary>
    [HarmonyPatch(typeof(FreeCurrencyDrops), nameof(FreeCurrencyDrops.TryHandleResult))]
    internal static class LoginRewardDropSpeedTryHandleResultPatch
    {
        private static readonly global::System.Reflection.FieldInfo WaitUntilField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_waitUntil");

        private static void Postfix(FreeCurrencyDrops __instance)
        {
            if (!LoginRewardDropSpeed.ShouldApply())
                return;
            if (WaitUntilField == null)
                return;

            LoginRewardDropSpeed.Resolve(out _, out float recheck, out _, out _);

            var waitUntil = WaitUntilField.GetValue(__instance) as global::System.Collections.Generic.Dictionary<int, float>;
            if (waitUntil == null || waitUntil.Count == 0)
                return;

            float now = Time.unscaledTime;
            float target = now + recheck;
            // 原版刚写入的是 now+65；把仍在等待的条目统一改为配置值（可短可长）
            var keys = new global::System.Collections.Generic.List<int>(waitUntil.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int k = keys[i];
                if (waitUntil.TryGetValue(k, out float until) && until > now)
                    waitUntil[k] = target;
            }
        }
    }

    /// <summary>
    /// FreeCurrencyDrops.ScheduleRetry（private）：0.1.17a FreeCurrencyDrops.cs:248。
    /// Postfix：按配置的下限/上限重写 _nextAt 指数退避。
    /// </summary>
    [HarmonyPatch(typeof(FreeCurrencyDrops), "ScheduleRetry")]
    internal static class LoginRewardDropSpeedScheduleRetryPatch
    {
        private static readonly global::System.Reflection.FieldInfo NextAtField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_nextAt");
        private static readonly global::System.Reflection.FieldInfo FailuresField =
            AccessTools.Field(typeof(FreeCurrencyDrops), "_failures");

        private static void Postfix(FreeCurrencyDrops __instance)
        {
            if (!LoginRewardDropSpeed.ShouldApply())
                return;
            if (NextAtField == null || FailuresField == null)
                return;

            LoginRewardDropSpeed.Resolve(out _, out _, out float retryMin, out float retryMax);
            // ScheduleRetry 已执行 failures++；退避用递增后的值减 1 对齐原版公式
            int failures = (int)FailuresField.GetValue(__instance);
            int exp = Mathf.Min(Mathf.Max(failures - 1, 0), 5);
            float delay = Mathf.Min(retryMax, retryMin * (float)(1 << exp));
            NextAtField.SetValue(__instance, Time.unscaledTime + delay);
        }
    }
}
