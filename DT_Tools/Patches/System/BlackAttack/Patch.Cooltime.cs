using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.BlackAttack
{
    /// <summary>
    /// 仅改写原版攻击冷却入参：5（DelayAcquireWeapon，0.1.15b Player.cs:1005）与
    /// 20（ConsumeKillAndRearm，0.1.15b Player.cs:1044）。方法本体（私有）：
    /// 0.1.15b Player.cs:1008。Cooltime=0 时跳过原方法并立即恢复 CanAttack。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Player), "StartWeaponCooltime")]
    internal static class BlackAttackCooltimePatch
    {
        private static bool Prefix(Server.Game.Player __instance, ref int seconds)
        {
            if (!Engine.Enabled<BlackAttackFeature>())
                return true;

            if (seconds != 5 && seconds != 20)
                return true;

            int cd = Math.Max(0, BlackAttackFeature.Cooltime);
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
