using System;
using BepInEx.Configuration;
using HarmonyLib;
using Server.Game;
using DT_Tools.Core;
using GamePlayer = Server.Game.Player;
using GameCorpse = Server.Game.Corpse;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// 首具非炸弹尸体出现后，自动进入调查阶段的等待时间。
    /// 原版：Util.GetRandomNumber(50, 71) → 约 50–70 秒（常量名义 60）。
    /// 本功能：可配置区间内随机，默认与原版一致。
    /// 手动报告尸体仍立即进入调查，不受本配置影响。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "CorpseWait",
        description: "尸体自动进入调查的等待时间（秒）。默认与原版一致：约 50–70 随机。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class CorpseWaitFeature
    {
        [ConfigField(50, "等待秒数下限（含）。原版 50。")]
        public static ConfigEntry<int> MinWaitSeconds;

        [ConfigField(71, "等待秒数上限（不含）。原版 71 → 实际最多 70。")]
        public static ConfigEntry<int> MaxWaitSeconds;

        /// <summary>
        /// 在 [min, max) 内随机，与原版 Util.GetRandomNumber 行为一致。
        /// </summary>
        private static int RollWait()
        {
            int min = Math.Max(1, MinWaitSeconds?.Value ?? 50);
            int max = MaxWaitSeconds?.Value ?? 71;
            if (max <= min)
                max = min + 1;
            return Util.GetRandomNumber(min, max);
        }

        private static bool _rewritePush;
        /// <summary>构造期内一次掷出的等待秒数，保证 PushSurvivalJob 与 WaitDetectiveSecond / StateList 一致。</summary>
        private static int _pendingWait;

        // 必须用 GameCorpse：全局命名空间另有客户端 Corpse : DeviceBase
        [HarmonyPatch(typeof(GameCorpse), MethodType.Constructor, new[] { typeof(GamePlayer), typeof(Protocol.PublicPlayerInfo) })]
        [HarmonyPrefix]
        private static void PrefixCorpseCtor()
        {
            if (!FeatureGate.Enabled(typeof(CorpseWaitFeature)))
                return;

            _rewritePush = true;
            _pendingWait = RollWait();
        }

        [HarmonyPatch(typeof(GameCorpse), MethodType.Constructor, new[] { typeof(GamePlayer), typeof(Protocol.PublicPlayerInfo) })]
        [HarmonyPostfix]
        private static void PostfixCorpseCtor(GameCorpse __instance)
        {
            if (!FeatureGate.Enabled(typeof(CorpseWaitFeature)))
                return;

            _rewritePush = false;
            // 仅当原版已为「首具尸体」排程时 WaitDetectiveSecond > 0
            if (__instance == null || __instance.WaitDetectiveSecond <= 0)
                return;

            int wait = _pendingWait;
            Traverse.Create(__instance).Property("WaitDetectiveSecond").SetValue(wait);
            var deviceInfo = __instance.DeviceInfo;
            if (deviceInfo?.StateList != null && deviceInfo.StateList.Count > 5)
            {
                deviceInfo.StateList[5] = TimeManager.Instance.SurviveTime + wait;
            }
        }

        /// <summary>
        /// 尸体构造期内 PushSurvivalJob(..., EndSurvival) 的延迟改为同一随机值。
        /// </summary>
        [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.PushSurvivalJob), new[] { typeof(int), typeof(Action) })]
        [HarmonyPrefix]
        private static void PrefixPushSurvivalJob(ref int secondAfter)
        {
            if (!FeatureGate.Enabled(typeof(CorpseWaitFeature)))
                return;

            if (!_rewritePush)
                return;
            secondAfter = _pendingWait;
        }
    }
}
