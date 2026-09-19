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
    /// 手动报告尸体仍立即进入调查，不受本配置影响。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "CorpseWait",
        description: "尸体自动进入调查的等待时间（秒）。原版约 50–70 随机。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class CorpseWaitFeature
    {
        [ConfigField(60, "首具尸体出现后自动进入调查的等待秒数。原版约 50–70 随机（名义 60）。")]
        public static ConfigEntry<int> WaitSeconds;

        private static int EffectiveWait =>
            Math.Max(1, WaitSeconds?.Value ?? 60);

        private static bool _rewritePush;

        // 必须用 GameCorpse：全局命名空间另有客户端 Corpse : DeviceBase
        [HarmonyPatch(typeof(GameCorpse), MethodType.Constructor, new[] { typeof(GamePlayer), typeof(Protocol.PublicPlayerInfo) })]
        [HarmonyPrefix]
        private static void PrefixCorpseCtor()
        {
            _rewritePush = true;
        }

        [HarmonyPatch(typeof(GameCorpse), MethodType.Constructor, new[] { typeof(GamePlayer), typeof(Protocol.PublicPlayerInfo) })]
        [HarmonyPostfix]
        private static void PostfixCorpseCtor(GameCorpse __instance)
        {
            _rewritePush = false;
            // 仅当原版已为「首具尸体」排程时 WaitDetectiveSecond > 0
            if (__instance == null || __instance.WaitDetectiveSecond <= 0)
                return;

            int wait = EffectiveWait;
            Traverse.Create(__instance).Property("WaitDetectiveSecond").SetValue(wait);
            var deviceInfo = __instance.DeviceInfo;
            if (deviceInfo?.StateList != null && deviceInfo.StateList.Count > 5)
            {
                deviceInfo.StateList[5] = TimeManager.Instance.SurviveTime + wait;
            }
        }

        /// <summary>
        /// 尸体构造期内 PushSurvivalJob(..., EndSurvival) 的延迟改为配置值。
        /// </summary>
        [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.PushSurvivalJob), new[] { typeof(int), typeof(Action) })]
        [HarmonyPrefix]
        private static void PrefixPushSurvivalJob(ref int secondAfter)
        {
            if (!_rewritePush)
                return;
            secondAfter = EffectiveWait;
        }
    }
}
