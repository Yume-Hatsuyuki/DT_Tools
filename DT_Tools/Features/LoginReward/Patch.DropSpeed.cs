using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.LoginReward
{
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

    [HarmonyPatch(typeof(FreeCurrencyDrops), nameof(FreeCurrencyDrops.Update))]
    internal static class LoginRewardDropSpeedUpdatePatch
    {
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
            var keys = new global::System.Collections.Generic.List<int>(waitUntil.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int k = keys[i];
                if (waitUntil.TryGetValue(k, out float until) && until > now)
                    waitUntil[k] = target;
            }
        }
    }

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
            int failures = (int)FailuresField.GetValue(__instance);
            int exp = Mathf.Min(Mathf.Max(failures - 1, 0), 5);
            float delay = Mathf.Min(retryMax, retryMin * (float)(1 << exp));
            NextAtField.SetValue(__instance, Time.unscaledTime + delay);
        }
    }
}
