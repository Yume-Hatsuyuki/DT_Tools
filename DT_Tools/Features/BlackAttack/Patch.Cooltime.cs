using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.BlackAttack
{
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
