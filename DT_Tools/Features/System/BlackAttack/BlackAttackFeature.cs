using System;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using Server.Game;
using DT_Tools.Core;
using GamePlayer = Server.Game.Player;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// 黑方击杀次数与武器冷却（房主权威）。
    /// KillLimit 覆盖 GameRoom.BlackKillLimit（原版：开局人数&lt;6 为 1，否则 2）。
    /// Cooltime 覆盖 StartWeaponCooltime 中的首次 5s 与再装填 20s；其它调用（如 LockKnifeForSeconds）不改。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "BlackAttack",
        description: "黑方攻击：可自定义击杀次数上限与攻击冷却（秒）。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class BlackAttackFeature
    {
        [ConfigField(2, "黑方每次持刀可击杀次数上限。原版：开局人数≥6 为 2，否则 1。")]
        public static ConfigEntry<int> KillLimit;

        [ConfigField(20, "攻击冷却（秒）。覆盖变黑后首次可攻击延迟与击杀后再装填；原版分别为 5 / 20。0 表示无冷却。")]
        public static ConfigEntry<int> Cooltime;

        // 无上限钳制：服务端未限制这些数值。仅拒绝负数，避免 TimeManager 任务异常。
        private static int EffectiveKillLimit =>
            Math.Max(0, KillLimit?.Value ?? 2);

        private static int EffectiveCooltime =>
            Math.Max(0, Cooltime?.Value ?? 20);

        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.BlackKillLimit), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrefixBlackKillLimit(ref int __result)
        {
            __result = EffectiveKillLimit;
            return false;
        }

        /// <summary>
        /// 仅改写原版攻击冷却入参：5（DelayAcquireWeapon）与 20（ConsumeKillAndRearm）。
        /// Cooltime=0 时跳过原方法并立即恢复 CanAttack。
        /// </summary>
        [HarmonyPatch(typeof(GamePlayer), "StartWeaponCooltime")]
        [HarmonyPrefix]
        private static bool PrefixStartWeaponCooltime(GamePlayer __instance, ref int seconds)
        {
            if (seconds != 5 && seconds != 20)
                return true;

            int cd = EffectiveCooltime;
            if (cd <= 0)
            {
                __instance.CanAttack = true;
                __instance.Session?.Send(new S_COOLTIME_WEAPON { Cooltime = 0 });
                return false;
            }

            seconds = cd;
            return true;
        }
    }
}
